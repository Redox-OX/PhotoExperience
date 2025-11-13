using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneFader : MonoBehaviour
{
    public static SceneFader Instance { get; private set; }

    [Header("Style")]
    [SerializeField] private Color fadeColor = Color.black;

    Canvas canvas;
    CanvasGroup canvasGroup;
    Image image;
    bool isFading;
    bool isQuitting;

    void Awake()
    {
        // 单例 & 常驻
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildUIIfNeeded();
    }

    void OnApplicationQuit()
    {
        isQuitting = true;
    }

    void BuildUIIfNeeded()
    {
        if (canvasGroup != null) return;

        // Canvas
        var canvasGO = new GameObject("SceneFaderCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        canvasGroup = canvasGO.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 0f;

        // 全屏 Image
        var imgGO = new GameObject("Fade");
        imgGO.transform.SetParent(canvasGO.transform, false);
        image = imgGO.AddComponent<Image>();
        image.color = fadeColor;
        image.raycastTarget = true;

        var rt = image.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// 对外调用：淡出→切场→淡入
    /// </summary>
    public void FadeAndLoad(string sceneName, float fadeOutSeconds = 0.25f, float fadeInSeconds = 0.25f)
    {
        if (isFading || isQuitting) return;
        BuildUIIfNeeded();
        StartCoroutine(FadeAndLoadRoutine(sceneName, fadeOutSeconds, fadeInSeconds));
    }

    IEnumerator FadeAndLoadRoutine(string sceneName, float fadeOutSeconds, float fadeInSeconds)
    {
        isFading = true;
        BuildUIIfNeeded();

        if (canvasGroup == null) { isFading = false; yield break; }

        // 1) 淡到黑
        float t = 0f;
        while (t < fadeOutSeconds)
        {
            if (canvasGroup == null) { isFading = false; yield break; }
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / Mathf.Max(0.0001f, fadeOutSeconds));
            canvasGroup.alpha = a;
            yield return null;
        }
        if (canvasGroup != null) canvasGroup.alpha = 1f;

        // 2) 异步加载
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone)
        {
            if (canvasGroup == null) { isFading = false; yield break; }
            yield return null;
        }

        // 3) 等一帧稳定
        yield return null;

        // 4) 从黑淡出
        t = 0f;
        while (t < fadeInSeconds)
        {
            if (canvasGroup == null) { isFading = false; yield break; }
            t += Time.unscaledDeltaTime;
            float a = 1f - Mathf.Clamp01(t / Mathf.Max(0.0001f, fadeInSeconds));
            canvasGroup.alpha = a;
            yield return null;
        }
        if (canvasGroup != null) canvasGroup.alpha = 0f;

        isFading = false;
    }
}
