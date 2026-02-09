using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using TMPro;
using RhythmCafe.Level;
using System.Collections;

public class ScrLevelCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    public RawImage coverImage;
    public TMP_Text songNameText;
    public TMP_Text authorText;
    public TMP_Text bpmText; // 新接口无 BPM，保留字段以兼容 Prefab
    public Image difficultyImage;
    public TMP_Text difficultyText;
    public Image borderImage;

    [Header("Buttons")]
    public Button downloadButton;
    public Button playButton;

    [Header("Difficulty Colors")]
    public Color easyColor = new Color(0.4f, 0.8f, 0.4f);
    public Color mediumColor = new Color(0.9f, 0.8f, 0.2f);
    public Color hardColor = new Color(0.9f, 0.5f, 0.2f);
    public Color expertColor = new Color(0.9f, 0.2f, 0.2f);

    private LevelSearchResultResponse levelData;
    private Coroutine loadImageCoroutine;

    private static readonly string[] DifficultyOrder = { "Easy", "Medium", "Hard", "Expert" };

    void Start()
    {
        SetBorderAlpha(0f);

        if (bpmText != null)
            bpmText.gameObject.SetActive(false);

        // 下载/试玩功能暂未实现，后续通过其他接口
        if (downloadButton != null)
            downloadButton.interactable = false;

        if (playButton != null)
            playButton.interactable = false;
    }

    public void SetData(LevelSearchResultResponse data)
    {
        levelData = data;

        if (songNameText != null)
            songNameText.text = data.Name ?? "Unknown";

        if (authorText != null)
            authorText.text = data.Authors != null && data.Authors.Count > 0
                ? string.Join(", ", data.Authors)
                : "Unknown";

        SetDifficulty(data.Difficulty);
        LoadCoverImage(data.CoverUrl);
    }

    private void SetDifficulty(string difficulty)
    {
        if (difficultyText != null)
            difficultyText.text = difficulty ?? "Unknown";

        if (difficultyImage != null)
        {
            var lower = (difficulty ?? "").ToLowerInvariant();
            difficultyImage.color = lower switch
            {
                "easy" => easyColor,
                "medium" => mediumColor,
                "hard" => hardColor,
                "expert" => expertColor,
                _ => easyColor
            };
        }
    }

    private void LoadCoverImage(string url)
    {
        if (string.IsNullOrEmpty(url) || coverImage == null)
            return;

        if (loadImageCoroutine != null)
            StopCoroutine(loadImageCoroutine);

        loadImageCoroutine = StartCoroutine(LoadImageCoroutine(url));
    }

    private IEnumerator LoadImageCoroutine(string url)
    {
        var request = UnityWebRequestTexture.GetTexture(url);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            var texture = DownloadHandlerTexture.GetContent(request);
            coverImage.texture = texture;
        }

        request.Dispose();
    }

    public LevelSearchResultResponse GetLevelData() => levelData;

    public void OnPointerEnter(PointerEventData eventData)
    {
        StopBorderFade();
        borderFadeCoroutine = StartCoroutine(FadeBorderAlpha(1f));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        StopBorderFade();
        borderFadeCoroutine = StartCoroutine(FadeBorderAlpha(0f));
    }

    private Coroutine borderFadeCoroutine;
    private float borderFadeDuration = 0.15f;

    private void StopBorderFade()
    {
        if (borderFadeCoroutine != null)
            StopCoroutine(borderFadeCoroutine);
    }

    private IEnumerator FadeBorderAlpha(float targetAlpha)
    {
        if (borderImage == null) yield break;

        var color = borderImage.color;
        float startAlpha = color.a;
        float elapsed = 0f;

        while (elapsed < borderFadeDuration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(startAlpha, targetAlpha, elapsed / borderFadeDuration);
            borderImage.color = color;
            yield return null;
        }

        color.a = targetAlpha;
        borderImage.color = color;
    }

    private void SetBorderAlpha(float alpha)
    {
        if (borderImage != null)
        {
            var color = borderImage.color;
            color.a = alpha;
            borderImage.color = color;
        }
    }

    // private void LoadLevelScene(string levelPath)
    // {
    //     GCS.customLevelPaths = new []{levelPath};
    //     GCS.sceneToLoad = "scnGame";
    //     SceneManager.LoadScene("scnLoading");
    // }
}
