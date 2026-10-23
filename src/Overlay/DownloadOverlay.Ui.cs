using UnityEngine;
using UnityEngine.UI;

namespace AQRLiteHelper.Overlay
{
    internal sealed partial class DownloadOverlay
    {
        private const float PanelWidth = 520f;
        private const float PanelHeight = 148f;
        private const float Margin = 24f;
        private const float Padding = 14f;
        private const float CoverSize = 120f;
        private const float QualitySize = 46f;
        private const float QualityOverhang = 6f;
        private const float QualityRingWidth = 3f;
        private const int SpriteSize = 48;
        private const float SpriteRadius = 16f;

        private static readonly Color PanelColor = new Color(0.07f, 0.07f, 0.10f, 0.92f);
        private static readonly Color PanelOpaqueColor = new Color(0.07f, 0.07f, 0.10f, 1f);
        private static readonly Color AccentColor = new Color(0.96f, 0.45f, 0.71f, 1f);
        private static readonly Color SuccessColor = new Color(0.45f, 0.88f, 0.58f, 1f);
        private static readonly Color ErrorColor = new Color(1f, 0.42f, 0.42f, 1f);
        private static readonly Color MutedColor = new Color(1f, 1f, 1f, 0.62f);
        private static readonly Color TrackColor = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color CoverBackColor = new Color(1f, 1f, 1f, 0.08f);

        // First installed font wins; the rest act as glyph fallbacks (e.g. Hangul).
        private static readonly string[] FontNames =
        {
            "Microsoft YaHei UI", "Microsoft YaHei", "PingFang SC", "Hiragino Sans GB",
            "Noto Sans CJK SC", "Source Han Sans SC", "WenQuanYi Micro Hei", "SimHei",
            "Malgun Gothic", "Yu Gothic UI", "Meiryo", "Arial"
        };

        private Canvas _canvas = null!;
        private CanvasGroup _group = null!;
        private Font _font = null!;
        private Texture2D? _roundedTexture;
        private Sprite? _roundedSprite;
        private RawImage _difficulty = null!;
        private RawImage _quality = null!;
        private Image _qualityRing = null!;
        private Image _gradeBackdrop = null!;
        private Text _gradePlaceholder = null!;
        private Text _header = null!;
        private Text _title = null!;
        private Text _credits = null!;
        private Text _vfxCredits = null!;
        private Text _status = null!;
        private Text _percent = null!;
        private RectTransform _fill = null!;
        private Image _fillImage = null!;

        private void BuildUi()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32000;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Purely informational: never intercept clicks meant for the game.
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;

            _font = CreateFont();
            _roundedSprite = CreateRoundedSprite(out _roundedTexture);

            var panel = CreateRect("Panel", transform);
            Place(panel, Margin, Margin, PanelWidth, PanelHeight);
            AddImage(panel, PanelColor, SpriteRadius / 14f);

            var gradeFrame = CreateRect("GradeFrame", panel);
            Place(gradeFrame, Padding, Padding, CoverSize, CoverSize);
            _gradeBackdrop = AddImage(gradeFrame, CoverBackColor, SpriteRadius / 10f);

            var difficultyRect = CreateRect("Difficulty", gradeFrame);
            Stretch(difficultyRect);
            _difficulty = difficultyRect.gameObject.AddComponent<RawImage>();
            _difficulty.raycastTarget = false;
            _difficulty.enabled = false;

            _gradePlaceholder = CreateText(gradeFrame, "GradePlaceholder", 0f, 0f, CoverSize, CoverSize,
                26, AccentColor, FontStyle.Bold, TextAnchor.MiddleCenter);
            _gradePlaceholder.text = "AQR";

            // Quality badge overhangs the difficulty icon's bottom-right corner slightly;
            // a panel-coloured ring separates it from the (often dark) difficulty icon.
            var ringSize = QualitySize + QualityRingWidth * 2f;
            var ringRect = CreateRect("QualityRing", gradeFrame);
            ringRect.anchorMin = ringRect.anchorMax = ringRect.pivot = new Vector2(1f, 0f);
            ringRect.anchoredPosition = new Vector2(QualityOverhang + QualityRingWidth, -QualityOverhang - QualityRingWidth);
            ringRect.sizeDelta = new Vector2(ringSize, ringSize);
            _qualityRing = AddImage(ringRect, PanelOpaqueColor, SpriteRadius / (ringSize / 2f));
            _qualityRing.enabled = false;

            var qualityRect = CreateRect("Quality", gradeFrame);
            qualityRect.anchorMin = qualityRect.anchorMax = qualityRect.pivot = new Vector2(1f, 0f);
            qualityRect.anchoredPosition = new Vector2(QualityOverhang, -QualityOverhang);
            qualityRect.sizeDelta = new Vector2(QualitySize, QualitySize);
            _quality = qualityRect.gameObject.AddComponent<RawImage>();
            _quality.raycastTarget = false;
            _quality.enabled = false;

            var left = Padding * 2f + CoverSize;
            var width = PanelWidth - left - Padding;
            _header = CreateText(panel, "Header", left, 9f, width, 18f, 13, AccentColor, FontStyle.Bold);
            _title = CreateText(panel, "Title", left, 26f, width, 32f, 22, Color.white, FontStyle.Bold);
            _credits = CreateText(panel, "Credits", left, 58f, width, 19f, 14, MutedColor);
            _vfxCredits = CreateText(panel, "VfxCredits", left, 77f, width, 19f, 14, MutedColor);
            _status = CreateText(panel, "Status", left, 98f, width - 64f, 22f, 14, Color.white);
            _percent = CreateText(panel, "Percent", left + width - 64f, 98f, 64f, 22f, 14, Color.white,
                FontStyle.Bold, TextAnchor.MiddleRight);

            var track = CreateRect("Track", panel);
            Place(track, left, 128f, width, 6f);
            AddImage(track, TrackColor, SpriteRadius / 3f);

            _fill = CreateRect("Fill", track);
            Stretch(_fill);
            _fillImage = AddImage(_fill, AccentColor, SpriteRadius / 3f);
        }

        private void DestroyUiResources()
        {
            if (_roundedSprite != null)
            {
                Destroy(_roundedSprite);
            }

            if (_roundedTexture != null)
            {
                Destroy(_roundedTexture);
            }
        }

        private static Font CreateFont()
        {
            try
            {
                var font = Font.CreateDynamicFontFromOSFont(FontNames, 24);
                if (font != null)
                {
                    return font;
                }
            }
            catch
            {
                // Fall back to Unity's built-in font below.
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        /// <summary>
        /// White rounded square used as a 9-sliced sprite; corner radius per
        /// element is tuned through <see cref="Image.pixelsPerUnitMultiplier"/>.
        /// </summary>
        private static Sprite CreateRoundedSprite(out Texture2D texture)
        {
            texture = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[SpriteSize * SpriteSize];
            for (var y = 0; y < SpriteSize; y++)
            {
                for (var x = 0; x < SpriteSize; x++)
                {
                    var dx = Mathf.Max(SpriteRadius - (x + 0.5f), x + 0.5f - (SpriteSize - SpriteRadius), 0f);
                    var dy = Mathf.Max(SpriteRadius - (y + 0.5f), y + 0.5f - (SpriteSize - SpriteRadius), 0f);
                    var alpha = Mathf.Clamp01(SpriteRadius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    pixels[y * SpriteSize + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var border = new Vector4(SpriteRadius, SpriteRadius, SpriteRadius, SpriteRadius);
            return Sprite.Create(texture, new Rect(0f, 0f, SpriteSize, SpriteSize), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, border);
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        /// <summary>Positions <paramref name="rect"/> from its parent's top-left corner.</summary>
        private static void Place(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private Image AddImage(RectTransform rect, Color color, float radiusScale)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = _roundedSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = radiusScale;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text CreateText(RectTransform parent, string name, float left, float top, float width, float height,
            int size, Color color, FontStyle style = FontStyle.Normal, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var rect = CreateRect(name, parent);
            Place(rect, left, top, width, height);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }
    }
}
