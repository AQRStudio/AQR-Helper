using System;
using UnityEngine;

namespace AQRLiteHelper.Bridge
{
    internal static class BridgeJson
    {
        public static bool TryDeserializeOpenLevel(string json, out OpenLevelRequest? request)
        {
            request = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                request = JsonUtility.FromJson<OpenLevelRequest>(json);
                return request != null;
            }
            catch (Exception ex)
            {
                Main.Mod?.Logger.Warning($"无法解析桥接请求：{ex.Message}");
                return false;
            }
        }

        public static string Serialize<T>(T value)
        {
            return JsonUtility.ToJson(value);
        }
    }
}
