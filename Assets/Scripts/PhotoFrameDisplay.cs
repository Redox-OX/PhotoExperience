using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class PhotoFrameDisplayFade : MonoBehaviour
{
    public string folderName = "CapturedPhotos";
    public float switchInterval = 5f;
    public float fadeDuration = 1.5f;
    public Material frameMaterialInstance;

    private List<Texture2D> photos = new();
    private int currentIndex = 0;
    private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    private static readonly int NextMap = Shader.PropertyToID("_NextMap");
    private static readonly int Blend = Shader.PropertyToID("_Blend");

    void Start()
    {
        LoadPhotos();
        if (photos.Count == 0)
        {
            Debug.LogWarning($"未发现照片。路径 = {Path.Combine(Application.persistentDataPath, folderName)}");
            return;
        }

        // 确保我们有材质实例
        frameMaterialInstance = GetComponent<MeshRenderer>().material;
        frameMaterialInstance.SetTexture(BaseMap, photos[0]);
        frameMaterialInstance.SetFloat(Blend, 0f);

        StartCoroutine(SwitchLoop());
    }

    void LoadPhotos()
    {
        string dir = Path.Combine(Application.persistentDataPath, folderName);
        if (!Directory.Exists(dir)) return;

        foreach (string file in Directory.GetFiles(dir))
        {
            if (!(file.EndsWith(".jpg") || file.EndsWith(".png"))) continue;
            byte[] bytes = File.ReadAllBytes(file);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);
            photos.Add(tex);
        }
    }

    IEnumerator SwitchLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(switchInterval);

            int nextIndex = (currentIndex + 1) % photos.Count;
            Texture2D nextTex = photos[nextIndex];

            frameMaterialInstance.SetTexture(NextMap, nextTex);
            yield return StartCoroutine(Crossfade());
            
            // 完成后更新主贴图
            frameMaterialInstance.SetTexture(BaseMap, nextTex);
            frameMaterialInstance.SetFloat(Blend, 0f);
            currentIndex = nextIndex;
        }
    }

    IEnumerator Crossfade()
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            float t = elapsed / fadeDuration;
            frameMaterialInstance.SetFloat(Blend, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        frameMaterialInstance.SetFloat(Blend, 1f);
    }
}
