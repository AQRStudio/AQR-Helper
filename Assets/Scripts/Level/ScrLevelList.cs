using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using RhythmCafe.Level;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.Text;

public class ScrLevelList : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField searchInput;
    public Transform cardContainer;
    public GameObject cardPrefab;
    public ScrollRect scrollRect;

    [Header("Settings")]
    public int pageSize = 15;
    public float searchDelay = 0.5f;
    public float loadMoreThreshold = 0.1f;
    public int instantiatePerFrame = 2;

    private const string API_URL = "https://api.azusa.site/levels/search";

    private List<GameObject> cardInstances = new List<GameObject>();
    private Queue<GameObject> cardPool = new Queue<GameObject>();
    private Coroutine searchCoroutine;
    private Coroutine populateCoroutine;
    private int currentPage = 1;
    private bool isLoading = false;
    private bool hasMoreData = true;
    private string currentQuery = "";

    void Start()
    {
        if (searchInput != null)
        {
            searchInput.onValueChanged.AddListener(OnSearchChanged);
        }

        if (scrollRect != null)
        {
            scrollRect.onValueChanged.AddListener(OnScrollChanged);
        }

        Search("");
    }

    private void OnScrollChanged(Vector2 position)
    {
        if (isLoading || !hasMoreData) return;

        if (position.y <= loadMoreThreshold)
        {
            LoadMore();
        }
    }

    private void OnSearchChanged(string value)
    {
        if (searchCoroutine != null)
            StopCoroutine(searchCoroutine);

        searchCoroutine = StartCoroutine(SearchWithDelay(value));
    }

    private IEnumerator SearchWithDelay(string query)
    {
        yield return new WaitForSeconds(searchDelay);
        Search(query);
    }

    public void Search(string query)
    {
        if (populateCoroutine != null)
            StopCoroutine(populateCoroutine);

        currentQuery = query;
        currentPage = 1;
        hasMoreData = true;
        RecycleAllCards();
        StartCoroutine(FetchLevels(query, 1, false));
    }

    private void LoadMore()
    {
        if (isLoading || !hasMoreData) return;
        currentPage++;
        StartCoroutine(FetchLevels(currentQuery, currentPage, true));
    }

    private IEnumerator FetchLevels(string query, int pageNum, bool append)
    {
        isLoading = true;
        ScrAlert.Show("加载中...", false);

        var requestBody = new LevelSearchRequest
        {
            Query = query ?? "",
            Filters = new LevelFilters
            {
                Difficulties = new List<string>(),
                Qualities = new List<string>(),
                TagIds = new List<int>()
            },
            PageNum = pageNum,
            PageSize = pageSize,
            Sort = new LevelSort
            {
                By = "uploadTime",
                Order = "desc"
            }
        };

        string jsonBody = JsonConvert.SerializeObject(requestBody, Formatting.Indented);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        Debug.Log($"[API Request] URL: {API_URL}\nBody:\n{jsonBody}");

        using (var request = new UnityWebRequest(API_URL, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"API Error: {request.error}");
                ScrAlert.Show("加载失败: " + request.error);
                isLoading = false;
                ScrAlert.Hide();
                yield break;
            }

            string responseText = request.downloadHandler.text;
            Debug.Log($"[API Response] Status: {request.responseCode}\nBody:\n{responseText}");

            var wrapper = JsonConvert.DeserializeObject<ApiResponse<LevelSearchResponse>>(responseText);
            var response = wrapper != null ? wrapper.Data : null;

            if (response != null)
            {
                hasMoreData = response.HasNext;

                if (response.Content != null && response.Content.Count > 0)
                {
                    populateCoroutine = StartCoroutine(PopulateCardsAsync(response.Content, append));
                    yield return populateCoroutine;
                }
                else if (!append)
                {
                    hasMoreData = false;
                }
            }

            isLoading = false;
            ScrAlert.Hide();
        }
    }

    private IEnumerator PopulateCardsAsync(List<LevelSearchResultResponse> content, bool append)
    {
        if (!append)
        {
            RecycleAllCards();
        }

        int count = 0;
        foreach (var item in content)
        {
            var cardObj = GetCardFromPool();
            var card = cardObj.GetComponent<ScrLevelCard>();

            if (card != null)
            {
                card.SetData(item);
            }

            cardInstances.Add(cardObj);

            count++;
            if (count >= instantiatePerFrame)
            {
                count = 0;
                yield return null;
            }
        }
    }

    private GameObject GetCardFromPool()
    {
        GameObject cardObj;
        if (cardPool.Count > 0)
        {
            cardObj = cardPool.Dequeue();
            cardObj.SetActive(true);
            cardObj.transform.SetAsLastSibling();
        }
        else
        {
            cardObj = Instantiate(cardPrefab, cardContainer);
        }
        return cardObj;
    }

    private void RecycleAllCards()
    {
        foreach (var card in cardInstances)
        {
            if (card != null)
            {
                card.SetActive(false);
                cardPool.Enqueue(card);
            }
        }
        cardInstances.Clear();
    }
}
