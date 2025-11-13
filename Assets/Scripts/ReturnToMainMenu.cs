using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ReturnToMainMenu : MonoBehaviour
{
    // ====== 单例，跨场景只保留一个 ======
    private static ReturnToMainMenu _instance;

    [Header("Input (LEFT trigger)")]
    // 这里绑定到 XRI LeftHand / Select 或任何代表左 Trigger 的 Action
    public InputActionReference leftTrigger;
    [Range(0f, 1f)] public float triggerThreshold = 0.2f; // 大于这个值算“按住”

    [Header("Behavior")]
    [Min(0f)] public float holdSeconds = 3.0f;
    public string mainMenuScene = "MainMenu";
    public bool ignoreWhenInMain = true;
    public float cooldownAfterReturn = 1.0f;

    [Header("UI Progress")]
    public bool showProgressUI = true;
    [Range(0.5f, 4f)] public float uiScale = 1.0f;
    public Color ringColor = new Color(1f, 1f, 1f, 0.95f);
    public Color bgColor   = new Color(0f, 0f, 0f, 0.35f);

    // 状态
    private float holdStart = -1f;
    private bool busy;
    private float lastReturnTime = -999f;

    // UI
    private Canvas uiCanvas;
    private CanvasGroup uiGroup;
    private Image ring;
    private Image ringBg;
    private const float RingSize = 120f;

    void Awake()
    {
        // 单例：后面场景再出现同脚本就直接删掉
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        if (leftTrigger != null)
            leftTrigger.action.Enable();

        if (showProgressUI) BuildUI();
        SetProgressVisible(false);
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;

        if (leftTrigger != null)
            leftTrigger.action.Disable();

        if (uiCanvas) Destroy(uiCanvas.gameObject);
    }

    void Update()
    {
        if (busy) return;

        // 在主菜单时如果不需要此功能
        if (ignoreWhenInMain && SceneManager.GetActiveScene().name == mainMenuScene)
        {
            ResetProgress();
            return;
        }

        // ====== 每帧轮询左 Trigger 值 ======
        float triggerValue = 0f;
        if (leftTrigger != null)
            triggerValue = leftTrigger.action.ReadValue<float>();

        bool isDown = triggerValue > triggerThreshold;

        if (isDown)
        {
            if (holdStart < 0f) holdStart = Time.unscaledTime;

            float t = Mathf.Clamp01(
                (Time.unscaledTime - holdStart) /
                Mathf.Max(0.0001f, holdSeconds)
            );
            UpdateProgress(t);

            if (t >= 1f && Time.unscaledTime - lastReturnTime > cooldownAfterReturn)
            {
                StartCoroutine(ReturnRoutine());
            }
        }
        else
        {
            ResetProgress();
        }
    }

    // ========== UI 构建 ==========
    private void BuildUI()
{
    uiCanvas = new GameObject("ReturnToMainMenuUI").AddComponent<Canvas>();
    uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
    uiCanvas.sortingOrder = short.MaxValue;
    uiGroup = uiCanvas.gameObject.AddComponent<CanvasGroup>();
    uiGroup.alpha = 0f;
    uiGroup.blocksRaycasts = false;

    // 用 whiteTexture 生成一个 Sprite，尺寸必须用真实宽高（4x4）
    var tex = Texture2D.whiteTexture;
    Sprite whiteSprite = Sprite.Create(
        tex,
        new Rect(0, 0, tex.width, tex.height),
        new Vector2(0.5f, 0.5f)
    );

    // ===== 背景圆 =====
    var bgGO = new GameObject("RingBG");
    ringBg = bgGO.AddComponent<Image>();
    ringBg.sprite = whiteSprite;        // ★ 必须有 sprite
    ringBg.color = bgColor;
    ringBg.raycastTarget = false;

    var bgRt = ringBg.rectTransform;
    bgGO.transform.SetParent(uiCanvas.transform, false);
    bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0.5f);
    bgRt.anchoredPosition = Vector2.zero;
    bgRt.sizeDelta = Vector2.one * RingSize;
    bgRt.localScale = Vector3.one * uiScale;

    ringBg.type = Image.Type.Filled;
    ringBg.fillMethod = Image.FillMethod.Radial360;
    ringBg.fillAmount = 1f;

    // ===== 前景进度圆 =====
    var ringGO = new GameObject("Ring");
    ring = ringGO.AddComponent<Image>();
    ring.sprite = whiteSprite;          // ★ 同样必须有 sprite
    ring.color = ringColor;
    ring.raycastTarget = false;

    var rt = ring.rectTransform;
    ringGO.transform.SetParent(uiCanvas.transform, false);
    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
    rt.anchoredPosition = Vector2.zero;
    rt.sizeDelta = Vector2.one * (RingSize - 12f);
    rt.localScale = Vector3.one * uiScale;

    ring.type = Image.Type.Filled;
    ring.fillMethod = Image.FillMethod.Radial360;
    ring.fillOrigin = (int)Image.Origin360.Top;
    ring.fillClockwise = true;
    ring.fillAmount = 0f;
}




    private void UpdateProgress(float t01)
    {
        if (!showProgressUI || ring == null) return;
        SetProgressVisible(true);
        ring.fillAmount = t01;
    }

    private void ResetProgress()
    {
        holdStart = -1f;
        if (showProgressUI && ring != null)
        {
            ring.fillAmount = 0f;
            SetProgressVisible(false);
        }
    }

    private void SetProgressVisible(bool on)
    {
        if (uiGroup) uiGroup.alpha = on ? 1f : 0f;
    }

    // ========== 返回主菜单 ==========
    private IEnumerator ReturnRoutine()
    {
        busy = true;
        lastReturnTime = Time.unscaledTime;

        if (SceneFader.Instance != null)
        {
            SceneFader.Instance.FadeAndLoad(mainMenuScene, 0.25f, 0.25f);
            while (SceneManager.GetActiveScene().name != mainMenuScene)
                yield return null;
        }
        else
        {
            SceneManager.LoadScene(mainMenuScene);
            yield return null;
        }

        ResetProgress();
        busy = false;
    }
}
