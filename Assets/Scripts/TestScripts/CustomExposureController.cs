using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CustomExposureController : MonoBehaviour
{
    public CameraController camController; // 拍照控制脚本，提供 iso/aperture/shutter
    public Volume postVolume;

    private ColorAdjustments colorAdj;
    private float baseEV; // 基准曝光，启动时计算
    
    private DepthOfField dof;
    private MotionBlur motionBlur;

    void Start()
    {
        // 获取 Volume 覆写组件
        if (postVolume.profile.TryGet(out colorAdj) &&
            postVolume.profile.TryGet(out dof) &&
            postVolume.profile.TryGet(out motionBlur))
        {
            // 记录启动时的曝光值作为基准
            // baseEV = CalculateEV(
            //     camController.photographyCamera.aperture,
            //     camController.photographyCamera.shutterSpeed,
            //     camController.photographyCamera.iso
            // );

            baseEV = CalculateEV(8f, 0.005f, 3200f);

            // 可为景深设置模式和默认值
            dof.mode.value = DepthOfFieldMode.Bokeh; // 或 Gaussian
        }
        else
        {
            Debug.LogWarning("CustomExposureController: 需要在 Volume 中添加 ColorAdjustments、DepthOfField 和 MotionBlur 覆写。");
        }
    }

    void Update()
    {
        // 计算当前EV
        float currentEV = CalculateEV(camController.photographyCamera.aperture,
            camController.photographyCamera.shutterSpeed,
            camController.photographyCamera.iso);
        // 差值调整postExposure（负差值增加亮度，正差值降低亮度）
        // colorAdj.postExposure.value = baseEV - currentEV;
        
        float evDifference = baseEV - currentEV;

// 缩放差值，避免过度影响。例如乘以 0.5
        evDifference *= 0.5f;

// 限制曝光调整范围，防止一调整就“炸白”
        evDifference = Mathf.Clamp(evDifference, -3f, 3f);

        colorAdj.postExposure.value = evDifference;
        
        UpdateMotionBlur(camController.photographyCamera.shutterSpeed);
        UpdateDof(camController.photographyCamera.aperture);
    }

    float CalculateEV(float aperture, float shutter, float iso)
    {
        float ev = Mathf.Log((aperture * aperture) / shutter * 100f / iso, 2f);
        return ev;
    }
    
    void UpdateDof(float aperture) 
    {
        if (dof != null) {
            dof.aperture.value = aperture;
            dof.focalLength.value   = camController.photographyCamera.focalLength;
            dof.focusDistance.value = Mathf.Max(0.5f, camController.photographyCamera.focusDistance);
        }
    }
    
    public void UpdateMotionBlur(float shutterSpeed) 
    {
        if (motionBlur != null) {
            // 将快门速度映射到0-1强度范围（需自行决定映射方式）
            motionBlur.intensity.value = Mathf.InverseLerp(0.001f, 0.1f, shutterSpeed);
        }
    }
}