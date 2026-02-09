using System.Collections.Generic;
using Newtonsoft.Json;

namespace RhythmCafe.Level
{
    /// <summary>
    /// 关卡搜索请求体
    /// </summary>
    [System.Serializable]
    public class LevelSearchRequest
    {
        [JsonProperty("query")]
        public string Query { get; set; }

        [JsonProperty("filters")]
        public LevelFilters Filters { get; set; }

        [JsonProperty("sort")]
        public LevelSort Sort { get; set; }

        [JsonProperty("pageNum")]
        public int PageNum { get; set; }

        [JsonProperty("pageSize")]
        public int PageSize { get; set; }
    }

    [System.Serializable]
    public class LevelFilters
    {
        [JsonProperty("difficulties")]
        public List<string> Difficulties { get; set; }

        [JsonProperty("qualities")]
        public List<string> Qualities { get; set; }

        [JsonProperty("tagIds")]
        public List<int> TagIds { get; set; }
    }

    [System.Serializable]
    public class LevelSort
    {
        [JsonProperty("by")]
        public string By { get; set; }

        [JsonProperty("order")]
        public string Order { get; set; }
    }

    /// <summary>
    /// API 统一响应包装（code, msg, data）
    /// </summary>
    [System.Serializable]
    public class ApiResponse<T>
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("msg")]
        public string Msg { get; set; }

        [JsonProperty("data")]
        public T Data { get; set; }
    }

    /// <summary>
    /// 关卡搜索响应（data 内容）
    /// </summary>
    [System.Serializable]
    public class LevelSearchResponse
    {
        [JsonProperty("content")]
        public List<LevelSearchResultResponse> Content { get; set; }

        [JsonProperty("totalElements")]
        public long TotalElements { get; set; }

        [JsonProperty("totalPages")]
        public int TotalPages { get; set; }

        [JsonProperty("currentPage")]
        public int CurrentPage { get; set; }

        [JsonProperty("pageSize")]
        public int PageSize { get; set; }

        [JsonProperty("hasNext")]
        public bool HasNext { get; set; }

        [JsonProperty("hasPrevious")]
        public bool HasPrevious { get; set; }
    }

    /// <summary>
    /// 关卡搜索结果项
    /// </summary>
    [System.Serializable]
    public class LevelSearchResultResponse
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("artists")]
        public List<string> Artists { get; set; }

        [JsonProperty("authors")]
        public List<string> Authors { get; set; }

        [JsonProperty("coverUrl")]
        public string CoverUrl { get; set; }

        [JsonProperty("quality")]
        public string Quality { get; set; }

        [JsonProperty("difficulty")]
        public string Difficulty { get; set; }

        [JsonProperty("authorization")]
        public bool Authorization { get; set; }

        [JsonProperty("ytbMod")]
        public bool YtbMod { get; set; }

        [JsonProperty("dlc")]
        public bool Dlc { get; set; }
    }
}
