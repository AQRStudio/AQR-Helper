using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class ScrAlert : MonoBehaviour
{
    public static ScrAlert Instance { get; private set; }

    [Header("UI References")]
    public RectTransform alertPanel;
    public Image iconImage;
    public TMP_Text messageText;

    [Header("Animation Settings")]
    public float showDuration = 0.3f;
    public float hiddenY = -64f;
    public float shownY = 64f;
    public float autoHideDelay = 2f;

    private Tween moveTween;
    private Tween rotateTween;
    private Coroutine autoHideCoroutine;
    private bool isShowing = false;

    void Awake()
    {
        Instance = this;
        if (alertPanel != null)
        {
            var pos = alertPanel.anchoredPosition;
            pos.y = hiddenY;
            alertPanel.anchoredPosition = pos;
        }
    }

    public static void Show(string message, bool autoHide = true)
    {
        if (Instance == null) return;
        Instance.ShowAlert(message, autoHide);
    }

    public static void Hide()
    {
        if (Instance == null) return;
        Instance.HideAlert();
    }

    public static void UpdateMessage(string message)
    {
        if (Instance == null) return;
        if (Instance.messageText != null)
            Instance.messageText.text = message;
    }

    private void ShowAlert(string message, bool autoHide)
    {
        if (messageText != null)
            messageText.text = message;

        if (autoHideCoroutine != null)
            StopCoroutine(autoHideCoroutine);

        if (isShowing)
        {
            if (autoHide)
                autoHideCoroutine = StartCoroutine(AutoHideCoroutine());
            return;
        }
        isShowing = true;

        moveTween?.Kill();
        moveTween = DOTween.To(
            () => alertPanel.anchoredPosition.y,
            y => alertPanel.anchoredPosition = new Vector2(alertPanel.anchoredPosition.x, y),
            shownY,
            showDuration
        ).SetEase(Ease.OutBack);

        rotateTween?.Kill();
        if (iconImage != null)
        {
            iconImage.rectTransform.rotation = Quaternion.identity;
            rotateTween = DOTween.To(
                () => 0f,
                z => iconImage.rectTransform.rotation = Quaternion.Euler(0, 0, z),
                -360f,
                1f
            ).SetLoops(-1, LoopType.Restart).SetEase(Ease.Linear);
        }

        if (autoHide)
            autoHideCoroutine = StartCoroutine(AutoHideCoroutine());
    }

    private System.Collections.IEnumerator AutoHideCoroutine()
    {
        yield return new WaitForSeconds(autoHideDelay);
        HideAlert();
    }

    private void HideAlert()
    {
        if (!isShowing) return;
        isShowing = false;

        rotateTween?.Kill();
        if (iconImage != null)
            iconImage.rectTransform.rotation = Quaternion.identity;

        moveTween?.Kill();
        moveTween = DOTween.To(
            () => alertPanel.anchoredPosition.y,
            y => alertPanel.anchoredPosition = new Vector2(alertPanel.anchoredPosition.x, y),
            hiddenY,
            showDuration
        ).SetEase(Ease.InBack);
    }

    void OnDestroy()
    {
        moveTween?.Kill();
        rotateTween?.Kill();
        if (Instance == this)
            Instance = null;
    }
}
