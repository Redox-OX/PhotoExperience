using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CustomExposureController : MonoBehaviour
{
    [Header("References")]
    public CameraController camController;   // 摄影相机控制器
    public Volume postVolume;               // 挂有 ColorAdjustments / DepthOfField / MotionBlur 的 Volume

    // Volume 覆写组件
    private ColorAdjustments colorAdj;
    private DepthOfField dof;
    private MotionBlur motionBlur;

    [Header("参考曝光（三元组）")]
    [Tooltip("你认为“正常曝光”的那组参数（默认 8 / 0.05 / 3200）")]
    public float referenceAperture = 8f;     // F8
    public float referenceShutter  = 0.05f;  // 1/20 秒
    public float referenceISO      = 3200f;

    [Header("亮度 stops → postExposure")]
    [Tooltip("亮度差（stop）乘以这个系数再写入 postExposure，<1 = 更柔和")]
    public float brightnessToPostScale = 1.0f;
    [Tooltip("最终 postExposure 的下限和上限")]
    public float minPostExposure = -5f;
    public float maxPostExposure =  5f;

    [Header("整体偏移（可微调全局明暗）")]
    [Tooltip("在参考亮度基础上额外加的 stop 偏移（>0 整体更亮，<0 整体更暗）")]
    public float globalOffsetStops = 0f;

    private float referenceBrightnessStops;  // 参考亮度（以 stop 计）
    private float basePostExposure;          // Volume 中原始 postExposure

    void Start()
    {
        if (camController == null || postVolume == null || postVolume.profile == null)
        {
            Debug.LogWarning("CustomExposureController: 需要设置 camController 和 postVolume。");
            enabled = false;
            return;
        }

        if (!postVolume.profile.TryGet(out colorAdj))
        {
            Debug.LogWarning("CustomExposureController: Volume 中缺少 ColorAdjustments 覆写。");
            enabled = false;
            return;
        }

        postVolume.profile.TryGet(out dof);
        postVolume.profile.TryGet(out motionBlur);

        // 记录原始 postExposure 作为基准（通常是 0）
        basePostExposure = colorAdj.postExposure.value;

        // 计算参考亮度 stops（并加上全局偏移）
        referenceBrightnessStops = CalculateBrightnessStops(
            referenceAperture,
            referenceShutter,
            referenceISO
        ) + globalOffsetStops;

        // 设置景深模式
        if (dof != null)
        {
            dof.mode.value = DepthOfFieldMode.Bokeh;
        }
    }

    void Update()
    {
        var cam = camController.photographyCamera;
        if (cam == null || colorAdj == null) return;

        // -------- 1. 当前亮度（以 stop 计）--------
        float currentStops = CalculateBrightnessStops(
            cam.aperture,
            cam.shutterSpeed,
            cam.iso
        );

        // -------- 2. 与参考亮度做差：大于 0 = 更亮，小于 0 = 更暗 --------
        float deltaStops = currentStops - referenceBrightnessStops;

        // -------- 3. 映射到 postExposure --------
        float compensated = deltaStops * brightnessToPostScale;
        float finalPost   = Mathf.Clamp(basePostExposure + compensated, minPostExposure, maxPostExposure);

        colorAdj.postExposure.overrideState = true;
        colorAdj.postExposure.value = finalPost;

        // -------- 4. 景深 & 运动模糊同步 --------
        UpdateDof(cam);
        UpdateMotionBlur(cam.shutterSpeed);
    }

    /// <summary>
    /// 计算“亮度 stops”：log2(ISO * t / N^2)
    /// ISO 越高 → 值越大；快门越慢（t 越大）→ 值越大；光圈 F 越大（越小光圈）→ 值越小
    /// </summary>
    float CalculateBrightnessStops(float aperture, float shutter, float iso)
    {
        aperture = Mathf.Max(0.1f, aperture);
        shutter  = Mathf.Max(0.0001f, shutter);
        iso      = Mathf.Max(1f, iso);

        // 亮度 ∝ ISO * t / N^2
        float numerator   = iso * shutter;
        float denominator = aperture * aperture;
        float value       = numerator / denominator;

        // log2(value)
        return Mathf.Log(value, 2f);
    }

    void UpdateDof(Camera cam)
    {
        if (dof == null) return;

        dof.active = true;

        dof.aperture.overrideState      = true;
        dof.focalLength.overrideState   = true;
        dof.focusDistance.overrideState = true;

        dof.aperture.value      = Mathf.Clamp(cam.aperture, 1f, 16f);
        dof.focalLength.value   = cam.focalLength;
        dof.focusDistance.value = Mathf.Max(0.1f, cam.focusDistance);
    }

    public void UpdateMotionBlur(float shutterSpeed)
    {
        if (motionBlur == null) return;

        shutterSpeed = Mathf.Clamp(shutterSpeed, 0.0001f, 0.1f);
        float t = Mathf.InverseLerp(0.001f, 0.1f, shutterSpeed); // 越慢越模糊

        motionBlur.active = true;
        motionBlur.intensity.overrideState = true;
        motionBlur.intensity.value = t;
    }
}
