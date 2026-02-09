using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RhythmCafe.Level;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;

public class ScrLevelList : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField searchInput;
    public Transform cardContainer;
    public GameObject cardPrefab;
    public ScrollRect scrollRect;

    [Header("Settings")]
    public int perPage = 15;
    public float searchDelay = 0.5f;
    public float loadMoreThreshold = 0.1f;
    public int instantiatePerFrame = 2;

    private const string API_URL_DOMESTIC = "https://cafe.rhythmdoctor.top/api/get_chartlist.php";
    private const string API_URL_FOREIGN = "https://orchardb.fly.dev/typesense/collections/levels/documents/search";
    private const string API_KEY = "nicolebestgirl";
    
    private List<GameObject> cardInstances = new List<GameObject>();
    private Queue<GameObject> cardPool = new Queue<GameObject>();
    private Coroutine searchCoroutine;
    private Coroutine populateCoroutine;
    private int currentPage = 1;
    private int totalFound = 0;
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

    private IEnumerator FetchLevels(string query, int page, bool append)
    {
        isLoading = true;

        // 只有在没有下载任务时才显示加载提示
        bool showAlert = !ScrLevelCard.IsAnyDownloading;
        if (showAlert)
            ScrAlert.Show("加载中...", false);

        string queryBy = "song,authors,artist,tags,description";
        string queryByWeights = "12,8,6,5,4";
        string facetBy = "authors,tags,source,difficulty,artist";
        string maxFacetValues = "10";
        string numTypos = "2,1,1,1,0";
        string sortBy = "_text_match:desc,indexed:desc,last_updated:desc";
        string filterBy = "approval:=[-1..20]";

        bool useDomestic = ScrSettings.UseDomesticApi;
        string baseUrl = useDomestic ? API_URL_DOMESTIC : API_URL_FOREIGN;

        string url = $"{baseUrl}?q={UnityEngine.Networking.UnityWebRequest.EscapeURL(query)}" +
            $"&page={page}" +
            $"&per_page={perPage}" +
            $"&query_by={UnityEngine.Networking.UnityWebRequest.EscapeURL(queryBy)}" +
            $"&query_by_weights={UnityEngine.Networking.UnityWebRequest.EscapeURL(queryByWeights)}" +
            $"&facet_by={UnityEngine.Networking.UnityWebRequest.EscapeURL(facetBy)}" +
            $"&max_facet_values={maxFacetValues}" +
            $"&num_typos={UnityEngine.Networking.UnityWebRequest.EscapeURL(numTypos)}" +
            $"&sort_by={UnityEngine.Networking.UnityWebRequest.EscapeURL(sortBy)}" +
            $"&filter_by={UnityEngine.Networking.UnityWebRequest.EscapeURL(filterBy)}";

        var request = UnityEngine.Networking.UnityWebRequest.Get(url);

        // 国外 API 需要添加 API Key
        if (!useDomestic)
        {
            request.SetRequestHeader("x-typesense-api-key", API_KEY);
        }
        yield return request.SendWebRequest();

        if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            Debug.LogError($"API Error: {request.error}");
            request.Dispose();
            isLoading = false;
            yield break;
        }

        var response = JsonConvert.DeserializeObject<LevelSearchResponse>(request.downloadHandler.text);
        request.Dispose();
        
        if (response != null)
        {
            totalFound = response.Found;
            
            if (response.Hits != null && response.Hits.Count > 0)
            {
                populateCoroutine = StartCoroutine(PopulateCardsAsync(response.Hits, append));
                yield return populateCoroutine;
                hasMoreData = cardInstances.Count < totalFound;
            }
            else
            {
                hasMoreData = false;
            }
        }

        isLoading = false;
        if (showAlert)
            ScrAlert.Hide();
    }

    private IEnumerator PopulateCardsAsync(List<LevelHit> hits, bool append)
    {
        if (!append)
        {
            RecycleAllCards();
        }

        int count = 0;
        foreach (var hit in hits)
        {
            if (hit.Document == null) continue;

            var cardObj = GetCardFromPool();
            var card = cardObj.GetComponent<ScrLevelCard>();
            
            if (card != null)
            {
                card.SetData(hit.Document);
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
