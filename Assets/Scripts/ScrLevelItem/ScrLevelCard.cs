using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using TMPro;
using RhythmCafe.Level;
using System.Collections;
using System.IO;
using System.IO.Compression;
using UnityEngine.SceneManagement;

public class ScrLevelCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    public RawImage coverImage;
    public TMP_Text songNameText;
    public TMP_Text authorText;
    public TMP_Text bpmText;
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

    private LevelDocument levelData;
    private Coroutine loadImageCoroutine;

    private static readonly string[] DifficultyNames = { "Easy", "Medium", "Hard", "Expert" };

    public static bool IsAnyDownloading => isAnyDownloading;
    private static bool isAnyDownloading = false;
    private bool isDownloading = false;

    void Start()
    {
        SetBorderAlpha(0f);

        if (downloadButton != null)
            downloadButton.onClick.AddListener(OnDownloadClicked);

        if (playButton != null)
            playButton.onClick.AddListener(OnPlayClicked);
    }

    public void SetData(LevelDocument data)
    {
        levelData = data;

        if (songNameText != null)
            songNameText.text = data.song ?? "Unknown";

        if (authorText != null)
            authorText.text = data.authors != null && data.authors.Count > 0 
                ? string.Join(", ", data.authors) 
                : "Unknown";

        if (bpmText != null)
        {
            bpmText.text = $"{data.max_bpm} BPM";
        }
        SetDifficulty(data.difficulty);
        LoadCoverImage(data.image);
    }

    private void SetDifficulty(int difficulty)
    {
        difficulty = Mathf.Clamp(difficulty, 0, 3);

        if (difficultyText != null)
            difficultyText.text = DifficultyNames[difficulty];

        if (difficultyImage != null)
        {
            difficultyImage.color = difficulty switch
            {
                0 => easyColor,
                1 => mediumColor,
                2 => hardColor,
                3 => expertColor,
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
        var request = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url);
        yield return request.SendWebRequest();

        if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            var texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(request);
            coverImage.texture = texture;
        }
        
        request.Dispose();
    }

    public LevelDocument GetLevelData() => levelData;

    private void OnDownloadClicked()
    {
        if (isDownloading || levelData == null) return;

        if (isAnyDownloading)
        {
            // 正在下载中，不显示任何提示以免打断下载进度显示
            return;
        }

        string downloadUrl = !string.IsNullOrEmpty(levelData.url2) ? levelData.url2 : levelData.url;
        if (string.IsNullOrEmpty(downloadUrl)) return;

        StartCoroutine(DownloadAndExtract(downloadUrl, false));
    }

    private void OnPlayClicked()
    {
        if (isDownloading || levelData == null) return;

        if (isAnyDownloading)
        {
            // 正在下载中，不显示任何提示以免打断下载进度显示
            return;
        }

        string downloadUrl = !string.IsNullOrEmpty(levelData.url2) ? levelData.url2 : levelData.url;
        if (string.IsNullOrEmpty(downloadUrl)) return;

        StartCoroutine(DownloadAndExtract(downloadUrl, true));
    }

    private IEnumerator DownloadAndExtract(string originalUrl, bool playAfterDownload)
    {
        isDownloading = true;
        isAnyDownloading = true;
        ScrAlert.Show("准备下载...", false);

        string downloadUrl;
        if (ScrSettings.UseDomesticApi)
        {
            // 国内使用代理
            downloadUrl = "https://cafe.rhythmdoctor.top/api/download.php?url=" + UnityWebRequest.EscapeURL(originalUrl);
        }
        else
        {
            // 国外直接使用 URL2
            downloadUrl = originalUrl;
        }

        Debug.Log($"[Download] Original: {originalUrl}");
        Debug.Log($"[Download] Final: {downloadUrl}");
        string fileName = levelData.id + ".rdzip";
        string cachePath = ScrSettings.LevelCachePath;
        string filePath = Path.Combine(cachePath, fileName);
        string extractPath = Path.Combine(cachePath, levelData.id);

        if (!Directory.Exists(cachePath))
            Directory.CreateDirectory(cachePath);

        var request = UnityWebRequest.Get(downloadUrl);
        request.downloadHandler = new DownloadHandlerBuffer();
        var operation = request.SendWebRequest();

        while (!operation.isDone)
        {
            float progress = request.downloadProgress * 100f;
            ScrAlert.UpdateMessage($"下载中... {progress:F0}%");
            yield return null;
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            ScrAlert.UpdateMessage("下载失败: " + request.error);
            yield return new WaitForSeconds(2f);
            ScrAlert.Hide();
            isDownloading = false;
            isAnyDownloading = false;
            request.Dispose();
            yield break;
        }

        ScrAlert.UpdateMessage("保存文件...");
        yield return null;

        string saveError = null;
        try
        {
            File.WriteAllBytes(filePath, request.downloadHandler.data);
        }
        catch (System.Exception e)
        {
            saveError = e.Message;
        }

        request.Dispose();

        if (saveError != null)
        {
            ScrAlert.UpdateMessage("保存失败: " + saveError);
            yield return new WaitForSeconds(2f);
            ScrAlert.Hide();
            isDownloading = false;
            isAnyDownloading = false;
            yield break;
        }

        ScrAlert.UpdateMessage("解压中...");
        yield return null;

        string extractError = null;
        try
        {
            if (Directory.Exists(extractPath))
                Directory.Delete(extractPath, true);

            Directory.CreateDirectory(extractPath);

            using (var archive = ZipFile.OpenRead(filePath))
            {
                string rootFolder = null;
                if (archive.Entries.Count > 0)
                {
                    var firstEntry = archive.Entries[0].FullName;
                    int slashIndex = firstEntry.IndexOf('/');
                    if (slashIndex > 0)
                    {
                        string potentialRoot = firstEntry.Substring(0, slashIndex);
                        bool allInRoot = true;
                        foreach (var entry in archive.Entries)
                        {
                            if (!entry.FullName.StartsWith(potentialRoot + "/"))
                            {
                                allInRoot = false;
                                break;
                            }
                        }
                        if (allInRoot)
                            rootFolder = potentialRoot;
                    }
                }

                foreach (var entry in archive.Entries)
                {
                    string entryPath = entry.FullName;
                    if (rootFolder != null && entryPath.StartsWith(rootFolder + "/"))
                        entryPath = entryPath.Substring(rootFolder.Length + 1);

                    if (string.IsNullOrEmpty(entryPath)) continue;

                    string destPath = Path.Combine(extractPath, entryPath);

                    if (entry.FullName.EndsWith("/"))
                    {
                        Directory.CreateDirectory(destPath);
                    }
                    else
                    {
                        string destDir = Path.GetDirectoryName(destPath);
                        if (!Directory.Exists(destDir))
                            Directory.CreateDirectory(destDir);
                        entry.ExtractToFile(destPath, true);
                    }
                }
            }

            File.Delete(filePath);
        }
        catch (System.Exception e)
        {
            extractError = e.Message;
        }

        if (extractError != null)
        {
            ScrAlert.UpdateMessage("解压失败: " + extractError);
            yield return new WaitForSeconds(2f);
            ScrAlert.Hide();
            isDownloading = false;
            isAnyDownloading = false;
            yield break;
        }

        if (playAfterDownload)
        {
            ScrAlert.UpdateMessage("加载关卡...");
            yield return new WaitForSeconds(0.5f);
            ScrAlert.Hide();
            isDownloading = false;
            isAnyDownloading = false;

            string levelPath = Path.Combine(extractPath, "main.rdlevel");
            LoadLevelScene(levelPath);
        }
        else
        {
            ScrAlert.UpdateMessage("下载完成!");
            yield return new WaitForSeconds(1.5f);
            ScrAlert.Hide();
            isDownloading = false;
            isAnyDownloading = false;
        }
    }

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

    private void LoadLevelScene(string levelPath)
    {
        GCS.customLevelPaths = new []{levelPath};
        GCS.sceneToLoad = "scnGame";
        SceneManager.LoadScene("scnLoading");
        //RDStartup.Setup();
        //GCS.
    }
}
