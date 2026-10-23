using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AQRLiteHelper.Overlay
{
    /// <summary>
    /// Loads difficulty/quality grade icons embedded in the mod assembly
    /// (Assets/Grades). Values match the AQR API, e.g. "G12", "U5", "-2",
    /// "BYD+" or "unknown" for difficulty and "S+", "A" or "unknown" for
    /// quality, case-insensitively.
    /// </summary>
    internal static class GradeIcons
    {
        private const string ResourcePrefix = "Grades/";

        private static readonly Dictionary<string, Texture2D?> Cache =
            new Dictionary<string, Texture2D?>(StringComparer.OrdinalIgnoreCase);

        private static Dictionary<string, string>? _resourceNames;

        public static Texture2D? GetDifficulty(string value)
        {
            return Get("difficulty", value);
        }

        public static Texture2D? GetQuality(string value)
        {
            return Get("quality", value);
        }

        public static bool IsKnown(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && !value.Trim().Equals("unknown", StringComparison.OrdinalIgnoreCase);
        }

        private static Texture2D? Get(string kind, string value)
        {
            // Empty means the level info has not arrived (or failed); "unknown" has its own "?" icon.
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var key = kind + "/" + value.Trim();
            if (Cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            Texture2D? texture = null;
            try
            {
                if (ResourceNames.TryGetValue(key, out var resourceName))
                {
                    texture = Load(resourceName);
                }
            }
            catch (Exception ex)
            {
                Main.Mod?.Logger.Warning($"加载等级图标 {key} 失败：{ex.Message}");
            }

            Cache[key] = texture;
            return texture;
        }

        private static Texture2D? Load(string resourceName)
        {
            using var stream = typeof(GradeIcons).Assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                return null;
            }

            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true)
            {
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            if (texture.LoadImage(buffer.ToArray(), true))
            {
                return texture;
            }

            UnityEngine.Object.Destroy(texture);
            return null;
        }

        /// <summary>Maps "difficulty/U5" style keys to manifest resource names.</summary>
        private static Dictionary<string, string> ResourceNames
        {
            get
            {
                if (_resourceNames != null)
                {
                    return _resourceNames;
                }

                var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var name in typeof(GradeIcons).Assembly.GetManifestResourceNames())
                {
                    var normalized = name.Replace('\\', '/');
                    if (!normalized.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                        || !normalized.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var key = normalized.Substring(ResourcePrefix.Length, normalized.Length - ResourcePrefix.Length - 4);
                    names[key] = name;
                }

                _resourceNames = names;
                return names;
            }
        }
    }
}
