using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneFader : MonoBehaviour
{
    public static SceneFader Instance { get; private set; }

    [Header("Style")]
    [SerializeField] private Color fadeColor = Color.black;

    private Canvas canvas;
    private Image image;
    private bool busy;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 构建全屏 UI
        canvas = new GameObject("SceneFaderCanvas").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;            // 最前

        var cg = canvas.gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = true;                        // 阻断点击
        cg.alpha = 0f;

        var go = new GameObject("Fade");
        go.transform.SetParent(canvas.transform, false);
        image = go.AddComponent<Image>();
        image.color = fadeColor;
        image.raycastTarget = true;

        var rt = image.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    public void FadeAndLoad(string sceneName, float fadeOutSeconds = 0.25f, float fadeInSeconds = 0.25f)
    {
        if (!busy) StartCoroutine(FadeAndLoadRoutine(sceneName, fadeOutSeconds, fadeInSeconds));
    }

    private IEnumerator FadeAndLoadRoutine(string sceneName, float outSec, float inSec)
    {
        busy = true;
        var cg = canvas.GetComponent<CanvasGroup>();

        // 淡入到黑
        for (float t = 0; t < outSec; t += Time.unscaledDeltaTime)
        {
            cg.alpha = Mathf.Clamp01(t / Mathf.Max(0.0001f, outSec));
            yield return null;
        }
        cg.alpha = 1f;

        // 异步加载
        var op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone) yield return null;

        // 可在这里等待一帧以确保场景稳定
        yield return null;

        // 从黑淡出
        for (float t = 0; t < inSec; t += Time.unscaledDeltaTime)
        {
            cg.alpha = 1f - Mathf.Clamp01(t / Mathf.Max(0.0001f, inSec));
            yield return null;
        }
        cg.alpha = 0f;

        busy = false;
    }
}
