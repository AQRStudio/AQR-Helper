using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace AQRLiteHelper.Overlay
{
    /// <summary>
    /// Top-left in-game card showing the level being downloaded from the AQR
    /// website and its progress. Built entirely in code (no AssetBundle) and
    /// kept across scene loads so it stays visible while the editor opens.
    /// </summary>
    internal sealed partial class DownloadOverlay : MonoBehaviour
    {
        private const float OpenedHideDelay = 3.5f;
        private const float ErrorHideDelay = 10f;
        private const float OpeningTimeout = 45f;
        private const float FadeSpeed = 6f;

        private static DownloadOverlay? _instance;

        private OverlaySnapshot _snapshot = OverlaySnapshot.Empty;
        private int _seenVersion = -1;
        private string? _appliedTitle;
        private string? _appliedCredits;
        private string? _appliedVfxCredits;
        private float _phaseStartedAt;
        private float _hideAt = float.MaxValue;
        private float _displayedProgress;
        private long _speedLoaded;
        private float _speedSampledAt;
        private float _bytesPerSecond;

        public static void Install()
        {
            if (_instance != null)
            {
                return;
            }

            var gameObject = new GameObject("AQRHelper Download Overlay");
            DontDestroyOnLoad(gameObject);
            _instance = gameObject.AddComponent<DownloadOverlay>();
        }

        public static void Uninstall()
        {
            if (_instance == null)
            {
                return;
            }

            Destroy(_instance.gameObject);
            _instance = null;
        }

        private void Awake()
        {
            BuildUi();
            _group.alpha = 0f;
            _canvas.enabled = false;
        }

        private void OnDestroy()
        {
            DestroyUiResources();
        }

        private void Update()
        {
            var snapshot = DownloadOverlayState.Current;
            if (snapshot.Version != _seenVersion)
            {
                Apply(snapshot);
            }

            if (_snapshot.Phase == OverlayPhase.Opening)
            {
                CheckOpened();
            }

            UpdateProgressBar();

            var visible = _snapshot.Phase != OverlayPhase.Hidden && Time.unscaledTime < _hideAt;
            _group.alpha = Mathf.MoveTowards(_group.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * FadeSpeed);
            _canvas.enabled = _group.alpha > 0f;
        }

        private void Apply(OverlaySnapshot snapshot)
        {
            var now = Time.unscaledTime;
            var newRequest = snapshot.RequestId != _snapshot.RequestId;
            if (newRequest)
            {
                _displayedProgress = 0f;
                _bytesPerSecond = 0f;
                _speedLoaded = 0L;
                _speedSampledAt = now;
            }

            if (newRequest || snapshot.Phase != _snapshot.Phase)
            {
                _phaseStartedAt = now;
                _hideAt = snapshot.Phase == OverlayPhase.Opened ? now + OpenedHideDelay
                    : snapshot.Phase == OverlayPhase.Error ? now + ErrorHideDelay
                    : float.MaxValue;
            }

            if (snapshot.Phase == OverlayPhase.Downloading)
            {
                SampleSpeed(snapshot.Loaded, now);
            }

            _snapshot = snapshot;
            _seenVersion = snapshot.Version;
            ApplyTexts();
            ApplyGrades();
        }

        private void SampleSpeed(long loaded, float now)
        {
            var elapsed = now - _speedSampledAt;
            if (elapsed < 0.25f || loaded < _speedLoaded)
            {
                return;
            }

            var rate = (loaded - _speedLoaded) / elapsed;
            _bytesPerSecond = _bytesPerSecond <= 0f ? rate : Mathf.Lerp(_bytesPerSecond, rate, 0.3f);
            _speedLoaded = loaded;
            _speedSampledAt = now;
        }

        private void ApplyTexts()
        {
            var s = _snapshot;
            _header.text = s.LevelId > 0 ? $"AQR Helper  ·  #{s.LevelId}" : "AQR Helper";
            SetEllipsized(_title, ref _appliedTitle, string.IsNullOrWhiteSpace(s.Title) ? $"关卡 {s.LevelId}" : s.Title);

            // Role names follow the AQR website (艺术家 / 采音 / 特效).
            var credits = string.Empty;
            if (!string.IsNullOrEmpty(s.Artists))
            {
                credits = "艺术家 " + s.Artists;
            }

            if (!string.IsNullOrEmpty(s.Charters))
            {
                credits += (credits.Length > 0 ? "   ·   " : string.Empty) + "采音 " + s.Charters;
            }

            SetEllipsized(_credits, ref _appliedCredits, credits);
            SetEllipsized(_vfxCredits, ref _appliedVfxCredits,
                string.IsNullOrEmpty(s.Vfxers) ? string.Empty : "特效 " + s.Vfxers);

            _status.color = Color.white;
            _percent.text = string.Empty;
            switch (s.Phase)
            {
                case OverlayPhase.Downloading:
                    _status.text = s.Total > 0L
                        ? $"下载中  {FormatSize(s.Loaded)} / {FormatSize(s.Total)}"
                        : $"下载中  {FormatSize(s.Loaded)}";
                    if (_bytesPerSecond > 0f)
                    {
                        _status.text += $"  ·  {FormatSize((long)_bytesPerSecond)}/s";
                    }

                    if (s.Total > 0L)
                    {
                        _percent.text = $"{Math.Min(100L, s.Loaded * 100L / s.Total)}%";
                    }

                    break;
                case OverlayPhase.Extracting:
                    _status.text = "正在解压谱面…";
                    break;
                case OverlayPhase.Opening:
                    _status.text = "正在打开编辑器…";
                    break;
                case OverlayPhase.Opened:
                    _status.text = "已在编辑器中打开";
                    _status.color = SuccessColor;
                    _percent.text = "完成";
                    break;
                case OverlayPhase.Error:
                    _status.text = string.IsNullOrEmpty(s.Message) ? "谱面打开失败" : s.Message;
                    _status.color = ErrorColor;
                    break;
                default:
                    _status.text = string.IsNullOrEmpty(s.Message) ? "正在准备下载…" : s.Message;
                    break;
            }

            _percent.color = _status.color;
        }

        private void ApplyGrades()
        {
            var difficulty = GradeIcons.GetDifficulty(_snapshot.Difficulty);
            _difficulty.texture = difficulty;
            _difficulty.enabled = difficulty != null;
            _gradeBackdrop.enabled = difficulty == null;

            // Without an icon, show the raw grade (e.g. a newly added "-21") rather than nothing.
            _gradePlaceholder.enabled = difficulty == null;
            _gradePlaceholder.text = GradeIcons.IsKnown(_snapshot.Difficulty) ? _snapshot.Difficulty.Trim() : "AQR";

            var quality = GradeIcons.GetQuality(_snapshot.Quality);
            _quality.texture = quality;
            _quality.enabled = quality != null;
            _qualityRing.enabled = quality != null;
        }

        private void CheckOpened()
        {
            var requestId = _snapshot.RequestId;
            try
            {
                if (ADOBase.isLevelEditor && SamePath(ADOBase.levelPath, _snapshot.LevelPath))
                {
                    DownloadOverlayState.Update(requestId, s => s.With(phase: OverlayPhase.Opened));
                    return;
                }
            }
            catch
            {
                // Editor state may be unavailable mid scene load; check again next frame.
            }

            // An unsaved-changes prompt or a cancelled load would otherwise pin the card.
            if (Time.unscaledTime - _phaseStartedAt > OpeningTimeout)
            {
                DownloadOverlayState.Update(requestId, s => s.With(phase: OverlayPhase.Hidden));
            }
        }

        private void UpdateProgressBar()
        {
            var s = _snapshot;
            var indeterminate = s.Phase == OverlayPhase.Preparing
                || s.Phase == OverlayPhase.Extracting
                || s.Phase == OverlayPhase.Opening
                || (s.Phase == OverlayPhase.Downloading && s.Total <= 0L);

            _fillImage.color = s.Phase == OverlayPhase.Opened ? SuccessColor
                : s.Phase == OverlayPhase.Error ? ErrorColor
                : AccentColor;

            if (indeterminate)
            {
                var head = Mathf.Repeat(Time.unscaledTime * 0.9f, 1.4f) - 0.4f;
                SetFill(Mathf.Clamp01(head), Mathf.Clamp01(head + 0.4f));
                return;
            }

            var target = s.Phase == OverlayPhase.Downloading
                ? Mathf.Clamp01((float)((double)s.Loaded / s.Total))
                : s.Phase == OverlayPhase.Error ? _displayedProgress : 1f;
            _displayedProgress = Mathf.MoveTowards(_displayedProgress, target, Time.unscaledDeltaTime * 2.5f);
            SetFill(0f, _displayedProgress);
        }

        private void SetFill(float start, float end)
        {
            _fill.anchorMin = new Vector2(start, 0f);
            _fill.anchorMax = new Vector2(end, 1f);
        }

        private static bool SamePath(string? left, string? right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            {
                return false;
            }

            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Keeps <paramref name="target"/> on one line, trimming the end with "…"
        /// when it is wider than its box. <paramref name="applied"/> caches the
        /// last source string so unchanged values skip the measurement.
        /// </summary>
        private static void SetEllipsized(Text target, ref string? applied, string value)
        {
            if (applied == value)
            {
                return;
            }

            applied = value;
            target.text = value;
            var maxWidth = target.rectTransform.rect.width;
            if (target.preferredWidth <= maxWidth)
            {
                return;
            }

            // Binary search the longest prefix that still fits with the ellipsis.
            var low = 0;
            var high = value.Length;
            while (low < high)
            {
                var mid = (low + high + 1) / 2;
                target.text = value.Substring(0, mid).TrimEnd() + "…";
                if (target.preferredWidth <= maxWidth)
                {
                    low = mid;
                }
                else
                {
                    high = mid - 1;
                }
            }

            target.text = value.Substring(0, low).TrimEnd() + "…";
        }

        private static string FormatSize(long bytes)
        {
            return bytes >= 1024L * 1024L
                ? $"{bytes / 1024f / 1024f:0.0} MB"
                : $"{bytes / 1024f:0} KB";
        }
    }
}
