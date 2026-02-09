using System.Collections.Generic;
using Newtonsoft.Json;

namespace RhythmCafe.Level
{
    [System.Serializable]
    public class LevelSearchResponse
    {
        [JsonProperty("found")]
        public int Found { get; set; }

        [JsonProperty("hits")]
        public List<LevelHit> Hits { get; set; }

        [JsonProperty("page")]
        public int Page { get; set; }

        [JsonProperty("out_of")]
        public int OutOf { get; set; }
    }

    [System.Serializable]
    public class LevelHit
    {
        [JsonProperty("document")]
        public LevelDocument Document { get; set; }
    }

    [System.Serializable]
    public class LevelDocument
    {
        public string id;
        public string song;
        public string artist;
        public List<string> authors;
        public int difficulty;
        public string image;
        public float min_bpm;
        public float max_bpm;
        public List<string> tags;
        public string url;
        public string url2;
        public string description;
        public int approval;
        public string source;
    }
}
