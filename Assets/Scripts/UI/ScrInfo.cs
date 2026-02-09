using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class ScrInfo : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform infoPanel;
    public Button openButton;
    public Button closeButton;

    [Header("Animation Settings")]
    public float animationDuration = 0.3f;

    private Tween scaleTween;
    private bool isOpen = false;

    void Awake()
    {
        if (infoPanel != null)
            infoPanel.localScale = Vector3.zero;
    }

    void Start()
    {
        if (openButton != null)
            openButton.onClick.AddListener(OpenPanel);

        if (closeButton != null)
            closeButton.onClick.AddListener(ClosePanel);
    }

    public void OpenPanel()
    {
        if (isOpen) return;
        isOpen = true;

        scaleTween?.Kill();
        scaleTween = DOTween.To(
            () => infoPanel.localScale,
            scale => infoPanel.localScale = scale,
            Vector3.one,
            animationDuration
        ).SetEase(Ease.OutBack);
    }

    public void ClosePanel()
    {
        if (!isOpen) return;
        isOpen = false;

        scaleTween?.Kill();
        scaleTween = DOTween.To(
            () => infoPanel.localScale,
            scale => infoPanel.localScale = scale,
            Vector3.zero,
            animationDuration
        ).SetEase(Ease.InBack);
    }

    void OnDestroy()
    {
        scaleTween?.Kill();
    }
}
