using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRGrabInteractable))]
public class GrabPortal : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private string sceneName = "NextScene";

    [Header("Anti-misfire")]
    [Tooltip("抓起后至少握持多少秒，才允许按 A 触发")]
    [SerializeField, Min(0f)] private float minHoldSeconds = 0.20f;

    [Tooltip("触发后多少秒内不再响应（防二次触发）")]
    [SerializeField, Min(0f)] private float cooldownSeconds = 1.0f;

    [Header("Info Canvas (渐显/渐隐)")]
    [Tooltip("挂在这个 Portal 上的描述 Canvas 的 CanvasGroup（截图+文字的整体父级）")]
    [SerializeField] private CanvasGroup infoCanvasGroup;

    [Tooltip("抓起或放下时淡入/淡出的时间")]
    [SerializeField, Min(0f)] private float fadeDuration = 0.25f;

    private XRGrabInteractable grab;
    private float selectedAt = -1f;
    private float lastTriggeredAt = -999f;

    private Coroutine fadeRoutine;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();

        // 订阅“被抓住/放下”
        grab.selectEntered.AddListener(OnSelectEntered);
        grab.selectExited.AddListener(OnSelectExited);

        // A 键（Activate）
        grab.activated.AddListener(OnActivated);

        // 如果没手动拖 infoCanvasGroup，则自动在子物体里找一个
        if (infoCanvasGroup == null)
        {
            infoCanvasGroup = GetComponentInChildren<CanvasGroup>(true);
        }

        // 初始隐藏信息面板
        if (infoCanvasGroup != null)
        {
            infoCanvasGroup.alpha = 0f;
            infoCanvasGroup.gameObject.SetActive(false);
        }
    }

    void OnDestroy()
    {
        grab.selectEntered.RemoveListener(OnSelectEntered);
        grab.selectExited.RemoveListener(OnSelectExited);
        grab.activated.RemoveListener(OnActivated);
    }

    // ======== 抓取 / 放下 ========

    void OnSelectEntered(SelectEnterEventArgs _)
    {
        selectedAt = Time.unscaledTime;   // 记录抓起时刻

        // 抓起来时淡入信息 Canvas
        FadeInfoCanvas(visible: true);
    }

    void OnSelectExited(SelectExitEventArgs _)
    {
        selectedAt = -1f;

        // 松手时淡出信息 Canvas
        FadeInfoCanvas(visible: false);
    }

    // ======== A 键激活切场 ========

    void OnActivated(ActivateEventArgs args)
    {
        // 基本防御：必须已被抓住
        if (!grab.isSelected) return;

        // 抓取时间不足，判为误触
        if (selectedAt < 0f || (Time.unscaledTime - selectedAt) < minHoldSeconds)
            return;

        // 冷却中，忽略
        if (Time.unscaledTime - lastTriggeredAt < cooldownSeconds)
            return;

        lastTriggeredAt = Time.unscaledTime;

        // 过渡加载（若 SceneFader 存在则淡入淡出，否则直接切场）
        if (!string.IsNullOrEmpty(sceneName))
        {
            if (SceneFader.Instance != null)
                SceneFader.Instance.FadeAndLoad(sceneName, 0.25f, 0.25f);
            else
                SceneManager.LoadScene(sceneName);
        }
    }

    // ======== 信息 Canvas 渐显 / 渐隐 ========

    private void FadeInfoCanvas(bool visible)
    {
        if (infoCanvasGroup == null || fadeDuration <= 0f)
        {
            // 没有 CanvasGroup 或不想渐变，就直接开/关
            if (infoCanvasGroup != null)
            {
                infoCanvasGroup.alpha = visible ? 1f : 0f;
                infoCanvasGroup.gameObject.SetActive(visible);
            }
            return;
        }

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeCanvasRoutine(visible));
    }

    private IEnumerator FadeCanvasRoutine(bool visible)
    {
        float startAlpha = infoCanvasGroup.alpha;
        float targetAlpha = visible ? 1f : 0f;

        if (visible)
            infoCanvasGroup.gameObject.SetActive(true);

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float lerp = Mathf.Clamp01(t / fadeDuration);
            infoCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, lerp);
            yield return null;
        }

        infoCanvasGroup.alpha = targetAlpha;

        if (!visible)
            infoCanvasGroup.gameObject.SetActive(false);

        fadeRoutine = null;
    }
}
