using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

public class XRPhotoInput : MonoBehaviour
{
    [Header("References")]
    public CameraController cameraController;     // 指到你的 CameraController（同一相机）
    public CustomExposureController exposure;     // 可选：指到你的 CustomExposureController

    [Header("Grip Inputs (float 0..1)")]
    public InputActionReference leftGrip;         // XRI LeftHand/Grip（或 GripValue）
    public InputActionReference rightGrip;        // XRI RightHand/Grip
                                                  // 若你只有 GripPressed（bool），可改用0/1并调step更小

    [Header("Mode Buttons (bool)")]
    public InputActionReference buttonA;          // 右手 A / Primary Button
    public InputActionReference buttonB;          // 右手 B / Secondary Button
    public InputActionReference buttonX;          // 左手 X / Primary Button
    public InputActionReference buttonY;          // 左手 Y / Secondary Button

    [Header("Shutter (bool)")]
    public InputActionReference rightTrigger;     // 右手 Trigger（Button 或 value>阈值）

    [Header("Tuning")]
    public float focalStepPerSec = 40f;           // 焦距 每秒 mm
    public float focusStepPerSec = 2.0f;          // 对焦距离 每秒 m
    public float isoStepPerSec   = 400f;          // ISO 每秒
    public float fStepPerSec     = 2.0f;          // 光圈 f 值 每秒
    public float shutterStepPerSec = 0.02f;       // 快门 每秒 (s)
    public float gripDeadZone = 0.15f;            // Grip 死区
    public float triggerThreshold = 0.5f;         // 触发阈值（如果是浮点）

    float lastTrigger;                             // 上一帧触发值（用于沿用浮点触发）
    bool shutterDown;

    void OnEnable()
    {
        EnableRef(leftGrip); EnableRef(rightGrip);
        EnableRef(buttonA);  EnableRef(buttonB);
        EnableRef(buttonX);  EnableRef(buttonY);
        EnableRef(rightTrigger);
    }
    void OnDisable()
    {
        DisableRef(leftGrip); DisableRef(rightGrip);
        DisableRef(buttonA);  DisableRef(buttonB);
        DisableRef(buttonX);  DisableRef(buttonY);
        DisableRef(rightTrigger);
    }
    void EnableRef(InputActionReference r){ if (r!=null) r.action.Enable(); }
    void DisableRef(InputActionReference r){ if (r!=null) r.action.Disable(); }

    void Update()
    {
        if (cameraController == null) return;

        // 1) 读取 Grip 差值（右减左），正值 → 增；负值 → 减
        float lg = ReadFloat(leftGrip);
        float rg = ReadFloat(rightGrip);
        float delta = SignedDelta(rg, lg, gripDeadZone); // [-1,1]

        // 2) 判断模式（A/B/X/Y 决定控制哪一个参数）
        bool a = ReadBool(buttonA);
        bool b = ReadBool(buttonB);
        bool x = ReadBool(buttonX);
        bool y = ReadBool(buttonY);

        // 优先级：Y > X > B > A > 默认（焦距）
        if      (y) AdjustShutter(delta);
        else if (x) AdjustAperture(delta);
        else if (b) AdjustISO(delta);
        else if (a) AdjustFocusDistance(delta);
        else        AdjustFocalLength(delta);

        // 3) 右手 Trigger 拍照（支持 bool 或 float）
        float tr = ReadFloat(rightTrigger);
        bool triggerPressed = (tr >= triggerThreshold) || ReadBool(rightTrigger);
        if (triggerPressed && !shutterDown)
        {
            shutterDown = true;
            cameraController.CapturePhoto();  // 直接走你的保存逻辑
        }
        else if (!triggerPressed)
        {
            shutterDown = false;
        }
        lastTrigger = tr;
    }

    float ReadFloat(InputActionReference r)
      => (r!=null) ? r.action.ReadValue<float>() : 0f;
    bool ReadBool(InputActionReference r)
      => (r!=null) && r.action.triggered || ((r!=null) && r.action.ReadValue<float>()>0.5f);

    static float SignedDelta(float inc, float dec, float dead)
    {
        float i = Mathf.Max(0f, inc - dead) / Mathf.Max(0.0001f, 1f - dead);
        float d = Mathf.Max(0f, dec - dead) / Mathf.Max(0.0001f, 1f - dead);
        return Mathf.Clamp(i - d, -1f, 1f);
    }

    // ---- 参数调整（直接改相机或滑块） ----
    void AdjustFocalLength(float dir)
    {
        if (Mathf.Approximately(dir, 0f)) return;
        var c = cameraController.photographyCamera;
        c.usePhysicalProperties = true;
        c.focalLength = Mathf.Clamp(c.focalLength + dir * focalStepPerSec * Time.deltaTime, 1f, 300f);
        cameraController.OnParameterChanged();
    }
    void AdjustFocusDistance(float dir)
    {
        if (Mathf.Approximately(dir, 0f)) return;
        var c = cameraController.photographyCamera;
        c.focusDistance = Mathf.Clamp(c.focusDistance + dir * focusStepPerSec * Time.deltaTime, 0.1f, 100f);
        cameraController.OnParameterChanged();
    }
    void AdjustISO(float dir)
    {
        if (Mathf.Approximately(dir, 0f)) return;
        var c = cameraController.photographyCamera;
        c.iso = Mathf.Clamp(Mathf.RoundToInt(c.iso + dir * isoStepPerSec * Time.deltaTime), 50, 12800);
        cameraController.OnParameterChanged();
    }
    void AdjustAperture(float dir)
    {
        if (Mathf.Approximately(dir, 0f)) return;
        var c = cameraController.photographyCamera;
        c.aperture = Mathf.Clamp(c.aperture + dir * fStepPerSec * Time.deltaTime, 1.0f, 22f);
        cameraController.OnParameterChanged();
    }
    void AdjustShutter(float dir)
    {
        if (Mathf.Approximately(dir, 0f)) return;
        var c = cameraController.photographyCamera;
        c.shutterSpeed = Mathf.Clamp(c.shutterSpeed + dir * shutterStepPerSec * Time.deltaTime, 1f/8000f, 1f);
        cameraController.OnParameterChanged();
    }
}
