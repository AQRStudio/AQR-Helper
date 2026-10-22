using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AQRLiteHelper.Overlay;
using Newtonsoft.Json.Linq;

namespace AQRLiteHelper.Bridge
{
    /// <summary>
    /// Fetches public level metadata (title, credits, grades) from the AQR API
    /// for the in-game overlay. Failures only degrade the overlay, never the download.
    /// </summary>
    internal static class LevelInfoClient
    {
        private const string ApiBaseUrl = "https://api.adofaiaqr.cn";
        private const int MaxNamesPerRole = 2;

        private static readonly HttpClient Client = CreateClient();

        public static async Task LoadIntoOverlayAsync(string requestId, int levelId)
        {
            try
            {
                var json = await Client.GetStringAsync($"{ApiBaseUrl}/level/{levelId}").ConfigureAwait(false);
                if (!(JObject.Parse(json)["data"] is JObject data) || (int?)data["id"] != levelId)
                {
                    Main.Mod?.Logger.Warning($"关卡 {levelId} 信息响应中没有有效数据：{Truncate(json)}");
                    return;
                }

                var name = (string?)data["name"];
                var difficulty = (string?)data["difficulty"] ?? string.Empty;
                var quality = (string?)data["quality"] ?? string.Empty;
                DownloadOverlayState.Update(requestId, snapshot => snapshot.With(
                    title: string.IsNullOrWhiteSpace(name) ? null : name,
                    artists: Join(data["artists"]),
                    charters: Join(data["charters"]),
                    vfxers: Join(data["vfxers"]),
                    difficulty: difficulty,
                    quality: quality));
                Main.Mod?.Logger.Log($"已获取关卡 {levelId} 信息：难度 {difficulty}，质量 {quality}");
            }
            catch (Exception ex)
            {
                Main.Mod?.Logger.Warning($"获取关卡 {levelId} 信息失败：{ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Most levels are team efforts, so long credit lists collapse to the
        /// first few names, e.g. "A, B 等 4 人".
        /// </summary>
        private static string Join(JToken? values)
        {
            if (!(values is JArray array))
            {
                return string.Empty;
            }

            var names = array
                .Select(value => ((string?)value)?.Trim())
                .Where(value => !string.IsNullOrEmpty(value))
                .ToList();
            return names.Count <= MaxNamesPerRole
                ? string.Join(", ", names)
                : $"{string.Join(", ", names.Take(MaxNamesPerRole))} 等 {names.Count} 人";
        }

        private static string Truncate(string text)
        {
            return text.Length <= 200 ? text : text.Substring(0, 200) + "…";
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AQR-Helper/1.0");
            return client;
        }
    }
}
