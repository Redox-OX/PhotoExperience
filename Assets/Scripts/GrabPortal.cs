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

    private XRGrabInteractable grab;
    private float selectedAt = -1f;
    private float lastTriggeredAt = -999f;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();

        // 订阅“被抓住/放下”
        grab.selectEntered.AddListener(OnSelectEntered);
        grab.selectExited.AddListener(OnSelectExited);

        // A 键（Activate）
        grab.activated.AddListener(OnActivated);
    }

    void OnDestroy()
    {
        grab.selectEntered.RemoveListener(OnSelectEntered);
        grab.selectExited.RemoveListener(OnSelectExited);
        grab.activated.RemoveListener(OnActivated);
    }

    void OnSelectEntered(SelectEnterEventArgs _)
    {
        selectedAt = Time.unscaledTime;   // 记录抓起时刻（用 unscaled，避免 timescale 影响）
    }

    void OnSelectExited(SelectExitEventArgs _)
    {
        selectedAt = -1f;
    }

    void OnActivated(ActivateEventArgs args)
    {
        // 基本防御：必须已被抓住
        if (!grab.isSelected) return;

        // 抓取时间不足，判为误触
        if (selectedAt < 0f || (Time.unscaledTime - selectedAt) < minHoldSeconds) return;

        // 冷却中，忽略
        if (Time.unscaledTime - lastTriggeredAt < cooldownSeconds) return;

        lastTriggeredAt = Time.unscaledTime;

        // 过渡加载（若 SceneFader 存在则淡入淡出，否则直接切场）
        if (!string.IsNullOrEmpty(sceneName))
        {
            if (SceneFader.Instance != null)
                SceneFader.Instance.FadeAndLoad(sceneName, 0.25f, 0.25f); // 入场0.25s，出场0.25s
            else
                SceneManager.LoadScene(sceneName);
        }
    }
}
