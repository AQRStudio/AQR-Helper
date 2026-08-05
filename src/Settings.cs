using System;
using System.IO;
using UnityFileDialog;
using UnityModManagerNet;
using UnityEngine;

namespace AQRLiteHelper
{
    public class Settings : UnityModManager.ModSettings
    {
        public string DownloadDirectory = string.Empty;

        [NonSerialized]
        private string _directoryError = string.Empty;

        public string EffectiveDownloadDirectory => string.IsNullOrWhiteSpace(DownloadDirectory)
            ? GetDefaultDownloadDirectory()
            : DownloadDirectory.Trim();

        public void OnGUI(UnityModManager.ModEntry modEntry)
        {
            GUILayout.Label("AQR Helper 设置");
            GUILayout.Space(6f);
            GUILayout.Label("谱面保存目录");

            GUILayout.BeginHorizontal();
            DownloadDirectory = GUILayout.TextField(EffectiveDownloadDirectory);
            if (GUILayout.Button("浏览", GUILayout.Width(70f)))
            {
                BrowseDownloadDirectory();
            }

            if (GUILayout.Button("打开文件夹", GUILayout.Width(100f)))
            {
                RevealDownloadDirectory();
            }

            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_directoryError))
            {
                var previousColor = GUI.color;
                GUI.color = Color.red;
                GUILayout.Label(_directoryError);
                GUI.color = previousColor;
            }
            else
            {
                GUILayout.Label("目录不存在时会自动创建，下载的谱面不会在游戏退出时删除。");
            }
        }

        public void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            TryPrepareDownloadDirectory(out _);
            Save(modEntry);
        }

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

        public static Settings Load(UnityModManager.ModEntry modEntry)
        {
            var settings = Load<Settings>(modEntry);
            if (string.IsNullOrWhiteSpace(settings.DownloadDirectory))
            {
                settings.DownloadDirectory = GetDefaultDownloadDirectory(modEntry);
            }

            return settings;
        }

        public bool TryPrepareDownloadDirectory(out string directory)
        {
            directory = EffectiveDownloadDirectory;
            _directoryError = string.Empty;

            try
            {
                directory = Path.GetFullPath(directory);
                Directory.CreateDirectory(directory);

                var testPath = Path.Combine(directory, $".aqr-write-test-{Guid.NewGuid():N}.tmp");
                using (File.Create(testPath))
                {
                }

                File.Delete(testPath);
                return true;
            }
            catch (Exception ex)
            {
                _directoryError = $"目录不可用：{ex.Message}";
                Main.Mod?.Logger.Warning(_directoryError);
                return false;
            }
        }

        private void BrowseDownloadDirectory()
        {
            var selected = FileBrowser.PickFolder(EffectiveDownloadDirectory, title: "选择谱面保存目录");
            if (!string.IsNullOrWhiteSpace(selected))
            {
                DownloadDirectory = selected;
                TryPrepareDownloadDirectory(out _);
            }
        }

        private void RevealDownloadDirectory()
        {
            if (!TryPrepareDownloadDirectory(out var directory))
            {
                return;
            }

            if (!FileBrowser.Reveal(directory))
            {
                _directoryError = "无法打开该目录。";
            }
        }

        private static string GetDefaultDownloadDirectory()
        {
            return GetDefaultDownloadDirectory(Main.Mod);
        }

        private static string GetDefaultDownloadDirectory(UnityModManager.ModEntry? modEntry)
        {
            var modPath = modEntry?.Path;
            return Path.Combine(
                string.IsNullOrWhiteSpace(modPath) ? AppDomain.CurrentDomain.BaseDirectory : modPath,
                "Downloads");
        }
    }
}
