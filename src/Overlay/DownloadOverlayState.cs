using System;

namespace AQRLiteHelper.Overlay
{
    internal enum OverlayPhase
    {
        Hidden,
        Preparing,
        Downloading,
        Extracting,
        Opening,
        Opened,
        Error
    }

    /// <summary>
    /// Immutable view of the current download, published by bridge worker
    /// threads and consumed by <see cref="DownloadOverlay"/> on the main thread.
    /// </summary>
    internal sealed class OverlaySnapshot
    {
        public static readonly OverlaySnapshot Empty = new OverlaySnapshot();

        public int Version { get; private set; }
        public string RequestId { get; private set; } = string.Empty;
        public int LevelId { get; private set; }
        public string Title { get; private set; } = string.Empty;
        public string Artists { get; private set; } = string.Empty;
        public string Charters { get; private set; } = string.Empty;
        public string Vfxers { get; private set; } = string.Empty;
        public string Difficulty { get; private set; } = string.Empty;
        public string Quality { get; private set; } = string.Empty;
        public OverlayPhase Phase { get; private set; } = OverlayPhase.Hidden;
        public long Loaded { get; private set; }
        public long Total { get; private set; } = -1L;
        public string Message { get; private set; } = string.Empty;
        public string LevelPath { get; private set; } = string.Empty;

        public static OverlaySnapshot Start(int version, string requestId, int levelId, string title)
        {
            return new OverlaySnapshot
            {
                Version = version,
                RequestId = requestId,
                LevelId = levelId,
                Title = title,
                Phase = OverlayPhase.Preparing,
                Message = "正在准备下载…"
            };
        }

        public OverlaySnapshot With(
            string? title = null,
            string? artists = null,
            string? charters = null,
            string? vfxers = null,
            string? difficulty = null,
            string? quality = null,
            OverlayPhase? phase = null,
            long? loaded = null,
            long? total = null,
            string? message = null,
            string? levelPath = null)
        {
            var copy = (OverlaySnapshot)MemberwiseClone();
            copy.Version = Version + 1;
            copy.Title = title ?? Title;
            copy.Artists = artists ?? Artists;
            copy.Charters = charters ?? Charters;
            copy.Vfxers = vfxers ?? Vfxers;
            copy.Difficulty = difficulty ?? Difficulty;
            copy.Quality = quality ?? Quality;
            copy.Phase = phase ?? Phase;
            copy.Loaded = loaded ?? Loaded;
            copy.Total = total ?? Total;
            copy.Message = message ?? Message;
            copy.LevelPath = levelPath ?? LevelPath;
            return copy;
        }
    }

    internal static class DownloadOverlayState
    {
        private static readonly object Sync = new object();
        private static OverlaySnapshot _current = OverlaySnapshot.Empty;

        public static OverlaySnapshot Current
        {
            get
            {
                lock (Sync)
                {
                    return _current;
                }
            }
        }

        public static void Begin(string requestId, int levelId, string fallbackTitle)
        {
            lock (Sync)
            {
                _current = OverlaySnapshot.Start(_current.Version + 1, requestId, levelId, fallbackTitle);
            }
        }

        /// <summary>
        /// Applies <paramref name="update"/> only while <paramref name="requestId"/>
        /// is still the active request, so late callbacks from an older request
        /// cannot overwrite a newer one.
        /// </summary>
        public static void Update(string requestId, Func<OverlaySnapshot, OverlaySnapshot> update)
        {
            lock (Sync)
            {
                if (_current.RequestId != requestId)
                {
                    return;
                }

                _current = update(_current);
            }
        }

        public static void Hide()
        {
            lock (Sync)
            {
                _current = _current.With(phase: OverlayPhase.Hidden);
            }
        }
    }
}
