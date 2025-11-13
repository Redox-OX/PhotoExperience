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
    public InputActionReference buttonA;               // A：对焦距离
    public InputActionReference buttonB;               // B：ISO
    public InputActionReference buttonX;               // X：光圈
    public InputActionReference buttonY;               // Y：快门

    [Header("Grip Response")]
    [Range(0f, 0.5f)] public float gripDeadZone = 0.15f;
    public AnimationCurve gripCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("物理调节速度（参数单位/秒）")]
    public float focalStepPerSec    = 40f;     // mm / s (焦距 8–120)
    public float focusStepPerSec    = 2.0f;    // m / s  (对焦距离 8–120)
    public float isoStepPerSec      = 400f;    // ISO / s (64–12800)
    public float apertureStepPerSec = 2.0f;    // f-stop / s (1–16)
    public float shutterStepPerSec  = 0.02f;   // 秒 / s (0.0001–0.1)

    [Header("Trigger 拍照")]
    [Range(0f, 1f)] public float triggerThreshold = 0.55f;

    // ========= UI 高亮 =========
    public enum Mode { Focal, Focus, ISO, Aperture, Shutter }

    [System.Serializable]
    public class ParamUI
    {
        public Image bg;
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

    // ========= UI Sliders =========
    [Header("UI Sliders（Slider 范围请与物理范围一致）")]
    public Slider focalSlider;      // 8–120
    public Slider focusSlider;      // 8–120
    public Slider isoSlider;        // 64–12800
    public Slider apertureSlider;   // 1–16
    public Slider shutterSlider;    // 0.0001–0.1

    [Header("按住加速设置")]
    [Tooltip("按住多久之后开始加速（秒）")]
    public float accelerateDelay = 0.25f;
    [Tooltip("从基础速度加速到最大速度需要的时间（秒）")]
    public float timeToMaxSpeed = 1.0f;
    [Tooltip("最大加速倍率，例如 3 = 最高 3 倍速度")]
    public float fastMultiplier = 3.0f;

    float lastTrigger;
    float[] modeHoldTimes = new float[5];
    float[] modeLastSigns = new float[5];

    void OnEnable()
    {
        EnableRef(leftGrip);  EnableRef(rightGrip);  EnableRef(rightTrigger);
        EnableRef(buttonA);   EnableRef(buttonB);    EnableRef(buttonX); EnableRef(buttonY);
    }
    void OnDisable()
    {
        DisableRef(leftGrip); DisableRef(rightGrip); DisableRef(rightTrigger);
        DisableRef(buttonA);  DisableRef(buttonB);   DisableRef(buttonX); DisableRef(buttonY);
    }
    void EnableRef(InputActionReference r)  { if (r) r.action.Enable(); }
    void DisableRef(InputActionReference r) { if (r) r.action.Disable(); }

    void Update()
    {
        if (!cameraController) return;

        // --- Grip 输入 ---
        float rg = Mathf.Clamp01(ReadFloat(rightGrip));
        float lg = Mathf.Clamp01(ReadFloat(leftGrip));
        float rawDelta = Mathf.Clamp(EvalGrip(rg) - EvalGrip(lg), -1f, 1f);
        bool isAdjusting = Mathf.Abs(rawDelta) > 0.01f;

        // --- 模式判定 ---
        bool a = ReadBool(buttonA);
        bool b = ReadBool(buttonB);
        bool x = ReadBool(buttonX);
        bool y = ReadBool(buttonY);
        bool anyModeButton = a || b || x || y;

        Mode mode = Mode.Focal;
        if      (y) mode = Mode.Shutter;
        else if (x) mode = Mode.Aperture;
        else if (b) mode = Mode.ISO;
        else if (a) mode = Mode.Focus;

        // 应用按住加速
        float delta = ApplyAcceleration(mode, rawDelta);

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

        if (a.type == InputActionType.Button)
            return a.IsPressed();

        var ctrl = a.activeControl != null ? a.activeControl : (a.controls.Count > 0 ? a.controls[0] : null);
        if (ctrl != null && ctrl.valueType == typeof(float))
            return a.ReadValue<float>() > 0.5f;

        return a.IsPressed();
    }

    float EvalGrip(float v)
    {
        if (v <= gripDeadZone) return 0f;
        float t = (v - gripDeadZone) / Mathf.Max(0.0001f, 1f - gripDeadZone);
        return Mathf.Clamp01(gripCurve.Evaluate(t));
    }

    // ========= 按住加速 =========
    float ApplyAcceleration(Mode mode, float input)
    {
        int i = (int)mode;

        if (Mathf.Abs(input) < 0.01f)
        {
            modeHoldTimes[i] = 0f;
            modeLastSigns[i] = 0f;
            return 0f;
        }

        float sign = Mathf.Sign(input);

        if (Mathf.Sign(modeLastSigns[i]) != sign)
        {
            modeHoldTimes[i] = 0f;
        }

        modeHoldTimes[i] += Time.deltaTime;
        modeLastSigns[i] = sign;

        float t = Mathf.Clamp01(
            Mathf.Max(0f, modeHoldTimes[i] - accelerateDelay) /
            Mathf.Max(0.0001f, timeToMaxSpeed)
        );
        float mul = Mathf.Lerp(1f, fastMultiplier, t);

        return input * mul; // 输入强度 * 加速系数
    }

    // ====================== 参数调整（优先通过 Slider） ======================

    void AdjustFocalLength(float dir)
    {
        if (Mathf.Approximately(dir, 0)) return;

        // Slider 驱动：dir(±) * 物理速度 / 范围 * dt
        if (focalSlider != null)
        {
            float v = focalSlider.value;
            float min = focalSlider.minValue;   // 建议 8
            float max = focalSlider.maxValue;   // 建议 120
            float range = max - min;
            float physicalDelta = dir * focalStepPerSec * Time.deltaTime; // mm
            v = Mathf.Clamp(v + physicalDelta, min, max);
            focalSlider.value = v;
            return;
        }

        // fallback：直接改相机
        var c = cameraController.photographyCamera;
        c.usePhysicalProperties = true;
        c.focalLength = Mathf.Clamp(c.focalLength + dir * focalStepPerSec * Time.deltaTime, 8f, 120f);
        cameraController.OnParameterChanged();
    }

    void AdjustFocusDistance(float dir)
    {
        if (Mathf.Approximately(dir, 0)) return;

        if (focusSlider != null)
        {
            float v = focusSlider.value;
            float min = focusSlider.minValue;   // 建议 8
            float max = focusSlider.maxValue;   // 建议 120
            float physicalDelta = dir * focusStepPerSec * Time.deltaTime; // m
            v = Mathf.Clamp(v + physicalDelta, min, max);
            focusSlider.value = v;
            return;
        }

        var c = cameraController.photographyCamera;
        c.focusDistance = Mathf.Clamp(c.focusDistance + dir * focusStepPerSec * Time.deltaTime, 8f, 120f);
        cameraController.OnParameterChanged();
    }

    void AdjustISO(float dir)
    {
        if (Mathf.Approximately(dir, 0)) return;

        if (isoSlider != null)
        {
            float v = isoSlider.value;
            float min = isoSlider.minValue;   // 建议 64
            float max = isoSlider.maxValue;   // 建议 12800
            float physicalDelta = dir * isoStepPerSec * Time.deltaTime; // ISO
            v = Mathf.Clamp(v + physicalDelta, min, max);
            isoSlider.value = v;
            return;
        }

        var c = cameraController.photographyCamera;
        c.iso = Mathf.Clamp(Mathf.RoundToInt(c.iso + dir * isoStepPerSec * Time.deltaTime), 64, 12800);
        cameraController.OnParameterChanged();
    }

    void AdjustAperture(float dir)
    {
        if (Mathf.Approximately(dir, 0)) return;

        if (apertureSlider != null)
        {
            float v = apertureSlider.value;
            float min = apertureSlider.minValue;   // 建议 1
            float max = apertureSlider.maxValue;   // 建议 16
            float physicalDelta = dir * apertureStepPerSec * Time.deltaTime; // f
            v = Mathf.Clamp(v + physicalDelta, min, max);
            apertureSlider.value = v;
            return;
        }

        var c = cameraController.photographyCamera;
        c.aperture = Mathf.Clamp(c.aperture + dir * apertureStepPerSec * Time.deltaTime, 1f, 16f);
        cameraController.OnParameterChanged();
    }

    void AdjustShutter(float dir)
    {
        if (Mathf.Approximately(dir, 0)) return;

        if (shutterSlider != null)
        {
            float v = shutterSlider.value;
            float min = shutterSlider.minValue;   // 建议 0.0001f
            float max = shutterSlider.maxValue;   // 建议 0.1f
            float physicalDelta = dir * shutterStepPerSec * Time.deltaTime; // 秒
            v = Mathf.Clamp(v + physicalDelta, min, max);
            shutterSlider.value = v;
            return;
        }

        var c = cameraController.photographyCamera;
        c.shutterSpeed = Mathf.Clamp(
            c.shutterSpeed + dir * shutterStepPerSec * Time.deltaTime,
            0.0001f, 0.1f
        );
        cameraController.OnParameterChanged();
    }

    // ====================== UI 高亮 ======================
    void UpdateUIHighlight(Mode current, bool isAdjusting, bool anyModeButton)
    {
        float tfocal    = (current == Mode.Focal && !anyModeButton) ? 1f : 0f;
        float tfocus    = current == Mode.Focus    ? 1f : 0f;
        float tiso      = current == Mode.ISO      ? 1f : 0f;
        float taperture = current == Mode.Aperture ? 1f : 0f;
        float tshutter  = current == Mode.Shutter  ? 1f : 0f;

        float s = highlightSpeed * Time.unscaledDeltaTime;
        focalUI.weight    = Mathf.MoveTowards(focalUI.weight,    tfocal,    s);
        focusUI.weight    = Mathf.MoveTowards(focusUI.weight,    tfocus,    s);
        isoUI.weight      = Mathf.MoveTowards(isoUI.weight,      tiso,      s);
        apertureUI.weight = Mathf.MoveTowards(apertureUI.weight, taperture, s);
        shutterUI.weight  = Mathf.MoveTowards(shutterUI.weight,  tshutter,  s);

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
