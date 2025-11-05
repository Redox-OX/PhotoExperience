using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class PhotoFrameDisplayFade : MonoBehaviour
{
    [Tooltip("子文件夹名称（位于 Application.persistentDataPath 下）")]
    public string folderName = "CapturedPhotos";
    [Tooltip("每张照片展示时间（秒）")]
    public float switchInterval = 5f;
    [Tooltip("淡出→切换→淡入 总时间（秒）")]
    public float fadeDuration = 1f;

    private List<Texture2D> photos = new List<Texture2D>();
    private int currentIndex = 0;
    private Renderer frameRenderer;
    private Material frameMaterialInstance;

    private void Start()
    {
        frameRenderer = GetComponent<Renderer>();
        if (frameRenderer == null)
        {
            Debug.LogError("PhotoFrameDisplayFade: 需要挂载在有 Renderer 的 GameObject 上。");
            enabled = false;
            return;
        }

        // 使用材质实例，避免修改共享材质
        frameMaterialInstance = frameRenderer.material;

        LoadPhotos();

        if (photos.Count == 0)
        {
            Debug.LogWarning("PhotoFrameDisplayFade: 未发现照片。路径 = " + Path.Combine(Application.persistentDataPath, folderName));
            return;
        }

        // 随机选择起始图片
        currentIndex = Random.Range(0, photos.Count);
        SetTexture(photos[currentIndex]);

        // 确保材质开始完全不透明（alpha = 1）
        SetMaterialAlpha(1f);

        // 启动切换协程
        StartCoroutine(SwitchLoop());
    }

    private void LoadPhotos()
    {
        string dir = Path.Combine(Application.persistentDataPath, folderName);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string[] files = Directory.GetFiles(dir, "*.jpg");
        foreach (var file in files)
        {
            try
            {
                byte[] data = File.ReadAllBytes(file);
                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(data))
                {
                    photos.Add(tex);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("PhotoFrameDisplayFade: 读取照片失败 " + file + " — " + ex.Message);
            }
        }
    }

    private IEnumerator SwitchLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(switchInterval);

            // 淡出
            yield return StartCoroutine(FadeAlpha(1f, 0f, fadeDuration));

            // 切换图片
            currentIndex = (currentIndex + 1) % photos.Count;
            SetTexture(photos[currentIndex]);

            // 淡入
            yield return StartCoroutine(FadeAlpha(0f, 1f, fadeDuration));
        }
    }

    private void SetTexture(Texture2D tex)
    {
        if (frameMaterialInstance.HasProperty("_BaseMap"))
        {
            frameMaterialInstance.SetTexture("_BaseMap", tex);
        }
        else
        {
            frameMaterialInstance.mainTexture = tex;
        }
    }

    private IEnumerator FadeAlpha(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float alpha = Mathf.Lerp(from, to, t);
            SetMaterialAlpha(alpha);
            elapsed += Time.deltaTime;
            yield return null;
        }
        SetMaterialAlpha(to);
    }

    private void SetMaterialAlpha(float alpha)
    {
        if (frameMaterialInstance.HasProperty("_BaseColor")) // URP Lit shader uses _BaseColor
        {
            Color c = frameMaterialInstance.GetColor("_BaseColor");
            c.a = alpha;
            frameMaterialInstance.SetColor("_BaseColor", c);
        }
        else
        {
            Color c = frameMaterialInstance.color;
            c.a = alpha;
            frameMaterialInstance.color = c;
        }
    }
}
