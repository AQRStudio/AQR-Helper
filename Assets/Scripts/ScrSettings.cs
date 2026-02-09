using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class ScrSettings : MonoBehaviour
{
    public static ScrSettings Instance { get; private set; }

    [Header("UI References")]
    public RectTransform settingsPanel;
    public Button openButton;
    public Button closeButton;
    public Button confirmButton;
    public InputField cachePathInput;
    public Slider bgmVolumeSlider;
    public TMP_Text bgmVolumeText;

    [Header("Audio")]
    public AudioClip bgmClip;
    private AudioSource bgmAudioSource;

    [Header("Download Source")]
    public Button domesticButton;
    public Button foreignButton;
    public Color selectedColor = new Color(0.3f, 0.7f, 1f);
    public Color unselectedColor = new Color(0.5f, 0.5f, 0.5f);

    private const string PREF_KEY_USE_DOMESTIC = "UseDomesticApi";

    public static bool UseDomesticApi
    {
        get => PlayerPrefs.GetInt(PREF_KEY_USE_DOMESTIC, 1) == 1;
        private set => PlayerPrefs.SetInt(PREF_KEY_USE_DOMESTIC, value ? 1 : 0);
    }

    [Header("Animation Settings")]
    public float animationDuration = 0.3f;

    private const string PREF_KEY_CACHE_PATH = "LevelCachePath";
    private const string PREF_KEY_BGM_VOLUME = "BgmVolume";
    private Tween scaleTween;
    private bool isOpen = false;

    public static string LevelCachePath
    {
        get
        {
            string defaultPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Rhythm Doctor", "Levels"
            );
            return PlayerPrefs.GetString(PREF_KEY_CACHE_PATH, defaultPath);
        }
    }

    void Awake()
    {
        Instance = this;

        if (settingsPanel != null)
            settingsPanel.localScale = Vector3.zero;

        // 初始化音量滑块显示
        float savedVolume = PlayerPrefs.GetFloat(PREF_KEY_BGM_VOLUME, 1f);
        if (bgmVolumeSlider != null)
            bgmVolumeSlider.value = savedVolume;
        UpdateVolumeText(savedVolume);
    }

    void Start()
    {
        if (openButton != null)
            openButton.onClick.AddListener(OpenPanel);

        if (closeButton != null)
            closeButton.onClick.AddListener(ClosePanel);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(SaveAndClose);

        if (cachePathInput != null)
            cachePathInput.text = LevelCachePath;

        if (bgmVolumeSlider != null)
            bgmVolumeSlider.onValueChanged.AddListener(OnVolumeChanged);

        if (domesticButton != null)
            domesticButton.onClick.AddListener(() => SetDownloadSource(true));

        if (foreignButton != null)
            foreignButton.onClick.AddListener(() => SetDownloadSource(false));

        UpdateDownloadSourceButtons();

        // 延迟创建音频源
        if (bgmClip != null)
        {
            StartCoroutine(CreateBgmAudioSourceDelayed());
        }
    }

    private IEnumerator CreateBgmAudioSourceDelayed()
    {
        yield return new WaitForSeconds(1.5f);

        GameObject audioObj = new GameObject("BGM_AudioSource");
        bgmAudioSource = audioObj.AddComponent<AudioSource>();
        bgmAudioSource.clip = bgmClip;
        bgmAudioSource.loop = true;
        bgmAudioSource.volume = PlayerPrefs.GetFloat(PREF_KEY_BGM_VOLUME, 1f);
        bgmAudioSource.Play();
    }

    private void SetDownloadSource(bool useDomestic)
    {
        UseDomesticApi = useDomestic;
        PlayerPrefs.Save();
        UpdateDownloadSourceButtons();
    }

    private void UpdateDownloadSourceButtons()
    {
        if (domesticButton != null)
        {
            var colors = domesticButton.colors;
            colors.normalColor = UseDomesticApi ? selectedColor : unselectedColor;
            domesticButton.colors = colors;
        }

        if (foreignButton != null)
        {
            var colors = foreignButton.colors;
            colors.normalColor = UseDomesticApi ? unselectedColor : selectedColor;
            foreignButton.colors = colors;
        }
    }

    private void OnVolumeChanged(float value)
    {
        if (bgmAudioSource != null)
            bgmAudioSource.volume = value;
        UpdateVolumeText(value);
    }

    private void UpdateVolumeText(float value)
    {
        if (bgmVolumeText != null)
            bgmVolumeText.text = $"{Mathf.RoundToInt(value * 100)}%";
    }

    public void OpenPanel()
    {
        if (isOpen) return;
        isOpen = true;

        if (cachePathInput != null)
            cachePathInput.text = LevelCachePath;

        scaleTween?.Kill();
        scaleTween = DOTween.To(
            () => settingsPanel.localScale,
            scale => settingsPanel.localScale = scale,
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
            () => settingsPanel.localScale,
            scale => settingsPanel.localScale = scale,
            Vector3.zero,
            animationDuration
        ).SetEase(Ease.InBack);
    }

    private void SaveAndClose()
    {
        if (cachePathInput != null)
        {
            string newPath = cachePathInput.text.Trim();
            PlayerPrefs.SetString(PREF_KEY_CACHE_PATH, newPath);
        }

        if (bgmVolumeSlider != null)
        {
            PlayerPrefs.SetFloat(PREF_KEY_BGM_VOLUME, bgmVolumeSlider.value);
        }

        PlayerPrefs.Save();
        ScrAlert.Show("设置已保存");

        ClosePanel();
    }

    void OnDestroy()
    {
        scaleTween?.Kill();
        if (Instance == this)
            Instance = null;
    }
}
