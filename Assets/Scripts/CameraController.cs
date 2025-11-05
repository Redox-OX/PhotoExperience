using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class CameraController : MonoBehaviour
{
    [Header("相机与预览")]
    public Camera photographyCamera;    // 摄影相机
    public RawImage previewUI;          // 用于显示预览的 RawImage

    [Header("曝光参数滑块")]
    public Slider isoSlider;
    public Slider apertureSlider;        // f 值滑块
    public Slider shutterSlider;         // 秒
    public Slider focalLengthSlider;     // mm
    public Slider focusDistanceSlider;

    [Header("保存设置")]
    public int captureWidth = 1920;
    public int captureHeight = 1080;
    public string folderName = "CapturedPhotos";

    private RenderTexture previewRT;
    private string saveDirectory;

    void Start()
    {
        // 创建保存目录
        saveDirectory = Path.Combine(Application.persistentDataPath, folderName);
        if (!Directory.Exists(saveDirectory))
            Directory.CreateDirectory(saveDirectory);

        // 创建用于实时预览的 RenderTexture
        previewRT = new RenderTexture(captureWidth, captureHeight, 16, RenderTextureFormat.ARGB32);
        photographyCamera.targetTexture = previewRT;
        previewUI.texture = previewRT;

        // 确保启用物理相机属性
        photographyCamera.usePhysicalProperties = true;

        // 初始化滑块默认值
        UpdateCameraParameters();
    }

    // 在UI滑块数值变动时调用
    public void OnParameterChanged()
    {
        UpdateCameraParameters();
    }

    private void UpdateCameraParameters()
    {
        // 从滑块读取值并设置相机属性
        int isoValue = Mathf.Max(50, Mathf.RoundToInt(isoSlider.value));
        photographyCamera.iso = isoValue;                       // ISO感光度
        
        photographyCamera.aperture = Mathf.Max(1.0f, apertureSlider.value);              // 光圈f值
        photographyCamera.shutterSpeed = Mathf.Max(0.0001f, shutterSlider.value);        // 快门速度(秒)
        photographyCamera.focalLength = Mathf.Max(1.0f, focalLengthSlider.value);        // 焦距(毫米)
        
        photographyCamera.focusDistance = Mathf.Max(0.1f, focusDistanceSlider.value);
    }

    // 按钮点击事件：拍照并保存
    public void CapturePhoto()
    {
        string imageName = "photo_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        CaptureAndSave(imageName);
    }

    // 拍照并保存到磁盘
    private void CaptureAndSave(string imageName)
    {
        // 使用临时RenderTexture捕捉画面
        RenderTexture tempRT = new RenderTexture(captureWidth, captureHeight, 16, RenderTextureFormat.ARGB32);
        photographyCamera.targetTexture = tempRT;
        photographyCamera.Render();

        RenderTexture.active = tempRT;
        Texture2D image = new Texture2D(captureWidth, captureHeight, TextureFormat.ARGB32, false);
        image.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
        image.Apply();

        // 恢复实时预览
        photographyCamera.targetTexture = previewRT;
        RenderTexture.active = null;
        tempRT.Release();

        // 保存图片
        byte[] bytes = image.EncodeToJPG();
        if (bytes != null)
        {
            string savePath = Path.Combine(saveDirectory, imageName + ".jpg");
            File.WriteAllBytes(savePath, bytes);
            Debug.Log("Saved photo to: " + savePath);
        }
        else
        {
            Debug.LogError("Failed to encode image.");
        }

        // 释放临时贴图
        Destroy(image);
    }
}
