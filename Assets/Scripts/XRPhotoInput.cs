using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class XRPhotoInput : MonoBehaviour
{
    [Header("References")]
    public CameraController cameraController;          // 相机控制脚本
    public CustomExposureController exposure;          // 可选

    [Header("Float Inputs (0..1)")]
    public InputActionReference leftGrip;
    public InputActionReference rightGrip;
    public InputActionReference rightTrigger;

    [Header("Mode Buttons (bool or float)")]
    public InputActionReference buttonA;               // A：对焦
    public InputActionReference buttonB;               // B：ISO
    public InputActionReference buttonX;               // X：光圈
    public InputActionReference buttonY;               // Y：快门

    [Header("Response")]
    [Range(0f, 0.5f)] public float gripDeadZone = 0.15f;
    public AnimationCurve gripCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Rates (per second)")]
    public float focalStepPerSec = 40f;
    public float focusStepPerSec = 2.0f;
    public float isoStepPerSec = 400f;
    public float apertureStepPerSec = 2.0f;
    public float shutterStepPerSec = 0.02f;

    [Header("Trigger")]
    [Range(0f, 1f)] public float triggerThreshold = 0.55f;

    // ========= UI 高亮 =========
    public enum Mode { Focal, Focus, ISO, Aperture, Shutter }

    [System.Serializable]
    public class ParamUI
    {
        public Image bg;                 // 背景 Image
        public Color themeColor = Color.white;
        [HideInInspector] public float weight;
    }

    [Header("UI Backgrounds & Colors")]
    public ParamUI focalUI;
    public ParamUI focusUI;
    public ParamUI isoUI;
    public ParamUI apertureUI;
    public ParamUI shutterUI;

    [Header("UI Visual Settings")]
    public Color normalColor = new Color(1, 1, 1, 0.25f);
    public float highlightSpeed = 6f;
    public bool pulseWhileAdjusting = true;
    [Range(0.1f, 8f)] public float pulseSpeed = 2.5f;
    [Range(1f, 2f)] public float pulseIntensity = 1.25f;

    float lastTrigger;

    void OnEnable()
    {
        EnableRef(leftGrip); EnableRef(rightGrip); EnableRef(rightTrigger);
        EnableRef(buttonA); EnableRef(buttonB); EnableRef(buttonX); EnableRef(buttonY);
    }
    void OnDisable()
    {
        DisableRef(leftGrip); DisableRef(rightGrip); DisableRef(rightTrigger);
        DisableRef(buttonA); DisableRef(buttonB); DisableRef(buttonX); DisableRef(buttonY);
    }
    void EnableRef(InputActionReference r) { if (r) r.action.Enable(); }
    void DisableRef(InputActionReference r) { if (r) r.action.Disable(); }

    void Update()
    {
        if (!cameraController) return;

        // --- Grip 输入 ---
        float rg = Mathf.Clamp01(ReadFloat(rightGrip));
        float lg = Mathf.Clamp01(ReadFloat(leftGrip));
        float delta = Mathf.Clamp(EvalGrip(rg) - EvalGrip(lg), -1f, 1f);
        bool isAdjusting = Mathf.Abs(delta) > 0.01f;

        // --- 模式判定 ---
        bool a = ReadBool(buttonA);
        bool b = ReadBool(buttonB);
        bool x = ReadBool(buttonX);
        bool y = ReadBool(buttonY);
        bool anyModeButton = a || b || x || y;

        Mode mode = Mode.Focal;
        if (y) mode = Mode.Shutter;
        else if (x) mode = Mode.Aperture;
        else if (b) mode = Mode.ISO;
        else if (a) mode = Mode.Focus;

        // --- 参数调整 ---
        switch (mode)
        {
            case Mode.Shutter:   AdjustShutter(delta);        break;
            case Mode.Aperture:  AdjustAperture(delta);       break;
            case Mode.ISO:       AdjustISO(delta);            break;
            case Mode.Focus:     AdjustFocusDistance(delta);  break;
            default:             AdjustFocalLength(delta);    break;
        }

        // --- UI 高亮 ---
        UpdateUIHighlight(mode, isAdjusting, anyModeButton);

        // --- Trigger 拍照 ---
        float t = Mathf.Clamp01(ReadFloat(rightTrigger));
        if (lastTrigger < triggerThreshold && t >= triggerThreshold)
            cameraController.CapturePhoto();
        lastTrigger = t;
    }

    // ====================== 输入处理 ======================
    float ReadFloat(InputActionReference r) => r ? r.action.ReadValue<float>() : 0f;

    bool ReadBool(InputActionReference r)
    {
        if (!r) return false;
        var a = r.action;
        if (a == null) return false;

        // 1) 明确是 Button，就用 IsPressed（处理死区/交互型按钮最稳）
        if (a.type == InputActionType.Button)
            return a.IsPressed();

        // 2) 尝试根据绑定的 Control 类型判断是否为 float
        var ctrl = a.activeControl != null ? a.activeControl : (a.controls.Count > 0 ? a.controls[0] : null);
        if (ctrl != null && ctrl.valueType == typeof(float))
            return a.ReadValue<float>() > 0.5f;

        // 3) 回退方案：仍用 IsPressed
        return a.IsPressed();
    }

    float EvalGrip(float v)
    {
        if (v <= gripDeadZone) return 0f;
        float t = (v - gripDeadZone) / Mathf.Max(0.0001f, 1f - gripDeadZone);
        return Mathf.Clamp01(gripCurve.Evaluate(t));
    }

    // ====================== 相机控制 ======================
    void AdjustFocalLength(float dir)
    {
        if (Mathf.Approximately(dir, 0)) return;
        var c = cameraController.photographyCamera;
        c.usePhysicalProperties = true;
        c.focalLength = Mathf.Clamp(c.focalLength + dir * focalStepPerSec * Time.deltaTime, 1f, 300f);
        cameraController.OnParameterChanged();
    }
    void AdjustFocusDistance(float dir)
    {
        if (Mathf.Approximately(dir, 0)) return;
        var c = cameraController.photographyCamera;
        c.focusDistance = Mathf.Clamp(c.focusDistance + dir * focusStepPerSec * Time.deltaTime, 0.05f, 200f);
        cameraController.OnParameterChanged();
    }
    void AdjustISO(float dir)
    {
        if (Mathf.Approximately(dir, 0)) return;
        var c = cameraController.photographyCamera;
        c.iso = Mathf.Clamp(Mathf.RoundToInt(c.iso + dir * isoStepPerSec * Time.deltaTime), 50, 25600);
        cameraController.OnParameterChanged();
    }
    void AdjustAperture(float dir)
    {
        if (Mathf.Approximately(dir, 0)) return;
        var c = cameraController.photographyCamera;
        c.aperture = Mathf.Clamp(c.aperture + dir * apertureStepPerSec * Time.deltaTime, 1.0f, 22f);
        cameraController.OnParameterChanged();
    }
    void AdjustShutter(float dir)
    {
        if (Mathf.Approximately(dir, 0)) return;
        var c = cameraController.photographyCamera;
        c.shutterSpeed = Mathf.Clamp(c.shutterSpeed + dir * shutterStepPerSec * Time.deltaTime, 1f / 8000f, 1f);
        cameraController.OnParameterChanged();
    }

    // ====================== UI 高亮控制 ======================
    void UpdateUIHighlight(Mode current, bool isAdjusting, bool anyModeButton)
    {
        // 焦距常亮，其它根据模式变化
        float tfocal = (current == Mode.Focal && !anyModeButton) ? 1f : 0f;
        float tfocus = current == Mode.Focus ? 1f : 0f;
        float tiso = current == Mode.ISO ? 1f : 0f;
        float taperture = current == Mode.Aperture ? 1f : 0f;
        float tshutter = current == Mode.Shutter ? 1f : 0f;

        float s = highlightSpeed * Time.unscaledDeltaTime;
        focalUI.weight = Mathf.MoveTowards(focalUI.weight, tfocal, s);
        focusUI.weight = Mathf.MoveTowards(focusUI.weight, tfocus, s);
        isoUI.weight = Mathf.MoveTowards(isoUI.weight, tiso, s);
        apertureUI.weight = Mathf.MoveTowards(apertureUI.weight, taperture, s);
        shutterUI.weight = Mathf.MoveTowards(shutterUI.weight, tshutter, s);

        // 脉动亮度（当前模式 + 正在调整）
        float pulseMul = 1f;
        if (pulseWhileAdjusting && isAdjusting)
        {
            pulseMul = Mathf.Lerp(1f, pulseIntensity,
                0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * pulseSpeed));
        }

        ApplyColor(focalUI,    current == Mode.Focal    ? pulseMul : 1f);
        ApplyColor(focusUI,    current == Mode.Focus    ? pulseMul : 1f);
        ApplyColor(isoUI,      current == Mode.ISO      ? pulseMul : 1f);
        ApplyColor(apertureUI, current == Mode.Aperture ? pulseMul : 1f);
        ApplyColor(shutterUI,  current == Mode.Shutter  ? pulseMul : 1f);
    }

    void ApplyColor(ParamUI ui, float pulseMul)
    {
        if (!ui.bg) return;
        Color baseColor = Color.Lerp(normalColor, ui.themeColor, ui.weight);
        if (pulseMul > 1f)
        {
            Color bright = baseColor * pulseMul;
            bright.a = baseColor.a;
            baseColor = bright;
        }
        ui.bg.color = baseColor;
    }
}
