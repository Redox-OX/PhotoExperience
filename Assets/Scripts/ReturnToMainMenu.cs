using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ReturnToMainMenu : MonoBehaviour
{
    [Header("Input (bind to your Grip actions)")]
    public InputActionReference leftGrip;   // 例如 XRI LeftHand/GripPressed（Button）
    public InputActionReference rightGrip;  // 例如 XRI RightHand/GripPressed（Button）

    [Header("Behavior")]
    [Min(0f)] public float holdSeconds = 3.0f;
    public string mainMenuScene = "MainMenu";
    public bool ignoreWhenInMain = true;
    public float cooldownAfterReturn = 1.0f;

    [Header("UI Progress")]
    public bool showProgressUI = true;
    [Range(0.5f, 4f)] public float uiScale = 1.0f;  // 进度环大小倍数
    public Color ringColor = new Color(1f,1f,1f,0.95f);
    public Color bgColor   = new Color(0f,0f,0f,0.35f);

    private float holdStart = -1f;
    private bool leftDown, rightDown;
    private bool busy;  // 正在切场
    private float lastReturnTime = -999f;

    // 进度 UI
    private Canvas uiCanvas;
    private CanvasGroup uiGroup;
    private Image ring;
    private Image ringBg;
    private const float RingSize = 120f; // 初始像素尺寸

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        HookInputs(true);
        if (showProgressUI) BuildUI();
        SetProgressVisible(false);
    }

    void OnDestroy()
    {
        HookInputs(false);
        if (uiCanvas) Destroy(uiCanvas.gameObject);
    }

    private void HookInputs(bool on)
    {
        if (leftGrip != null)
        {
            if (on)
            {
                leftGrip.action.started  += OnLeftStarted;
                leftGrip.action.canceled += OnLeftCanceled;
                leftGrip.action.Enable();
            }
            else
            {
                leftGrip.action.started  -= OnLeftStarted;
                leftGrip.action.canceled -= OnLeftCanceled;
            }
        }
        if (rightGrip != null)
        {
            if (on)
            {
                rightGrip.action.started  += OnRightStarted;
                rightGrip.action.canceled += OnRightCanceled;
                rightGrip.action.Enable();
            }
            else
            {
                rightGrip.action.started  -= OnRightStarted;
                rightGrip.action.canceled -= OnRightCanceled;
            }
        }
    }

    void Update()
    {
        if (busy) return;
        if (ignoreWhenInMain && SceneManager.GetActiveScene().name == mainMenuScene)
        {
            ResetProgress();
            return;
        }

        bool both = leftDown && rightDown;

        if (both)
        {
            if (holdStart < 0f) holdStart = Time.unscaledTime;

            float t = Mathf.Clamp01((Time.unscaledTime - holdStart) / Mathf.Max(0.0001f, holdSeconds));
            UpdateProgress(t);

            if (t >= 1f && Time.unscaledTime - lastReturnTime > cooldownAfterReturn)
            {
                // 触发返回主界面
                StartCoroutine(ReturnToMainRoutine());
            }
        }
        else
        {
            ResetProgress();
        }
    }

    // ==== Input callbacks ====
    private void OnLeftStarted(InputAction.CallbackContext _)  { leftDown  = true;  }
    private void OnLeftCanceled(InputAction.CallbackContext _) { leftDown  = false; }
    private void OnRightStarted(InputAction.CallbackContext _) { rightDown = true;  }
    private void OnRightCanceled(InputAction.CallbackContext _){ rightDown = false; }

    // ==== UI ====
    private void BuildUI()
    {
        uiCanvas = new GameObject("GripHoldUI").AddComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = short.MaxValue; // 最前
        uiGroup = uiCanvas.gameObject.AddComponent<CanvasGroup>();
        uiGroup.alpha = 0f;
        uiGroup.blocksRaycasts = false;

        // 背景圆
        var bgGO = new GameObject("RingBG");
        ringBg = bgGO.AddComponent<Image>();
        ringBg.color = bgColor;
        ringBg.raycastTarget = false;
        var bgRt = ringBg.rectTransform;
        bgGO.transform.SetParent(uiCanvas.transform, false);
        bgRt.sizeDelta = Vector2.one * RingSize;
        bgRt.anchoredPosition = Vector2.zero;
        bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0.5f);
        bgRt.localScale = Vector3.one * uiScale;
        ringBg.type = Image.Type.Filled; // 仅为统一圆角
        ringBg.fillMethod = Image.FillMethod.Radial360;
        ringBg.fillAmount = 1f;

        // 前景进度圆
        var ringGO = new GameObject("Ring");
        ring = ringGO.AddComponent<Image>();
        ring.color = ringColor;
        ring.raycastTarget = false;
        var rt = ring.rectTransform;
        ringGO.transform.SetParent(uiCanvas.transform, false);
        rt.sizeDelta = Vector2.one * (RingSize - 12f);
        rt.anchoredPosition = Vector2.zero;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one * uiScale;

        ring.type = Image.Type.Filled;
        ring.fillMethod = Image.FillMethod.Radial360;
        ring.fillOrigin = (int)Image.Origin360.Top; // 从上往下转
        ring.fillClockwise = true;
        ring.fillAmount = 0f;

        // 中心小点（可选）
        var dotGO = new GameObject("Dot");
        var dot = dotGO.AddComponent<Image>();
        dot.color = new Color(ringColor.r, ringColor.g, ringColor.b, 0.8f);
        dot.raycastTarget = false;
        var drt = dot.rectTransform;
        dotGO.transform.SetParent(uiCanvas.transform, false);
        drt.sizeDelta = Vector2.one * 6f;
        drt.anchoredPosition = new Vector2(0f, -(RingSize * 0.5f - 8f)) * uiScale;
        drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
    }

    private void UpdateProgress(float t01)
    {
        if (!showProgressUI) return;
        SetProgressVisible(true);
        ring.fillAmount = t01;
    }

    private void ResetProgress()
    {
        holdStart = -1f;
        if (showProgressUI)
        {
            ring.fillAmount = 0f;
            SetProgressVisible(false);
        }
    }

    private void SetProgressVisible(bool on)
    {
        if (uiGroup) uiGroup.alpha = on ? 1f : 0f;
    }

    // ==== Return ====
    private IEnumerator ReturnToMainRoutine()
    {
        busy = true;
        lastReturnTime = Time.unscaledTime;

        // 可加一次轻震动：留给你在各自手部控制器里调用（此处略）

        if (SceneFader.Instance != null)
        {
            SceneFader.Instance.FadeAndLoad(mainMenuScene, 0.25f, 0.25f);
            // 等待场景切换完成（简单办法：等到场景变为主菜单）
            string target = mainMenuScene;
            while (SceneManager.GetActiveScene().name != target) yield return null;
        }
        else
        {
            SceneManager.LoadScene(mainMenuScene);
            yield return null;
        }

        ResetProgress();
        // 主菜单一般不需要此功能，若仍常驻可由 ignoreWhenInMain 屏蔽
        busy = false;
    }
}
