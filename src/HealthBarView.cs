using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnemyHealthBars
{
    /// <summary>
    /// One pooled bar on the overlay canvas. Holds no game references, so a pooled view cannot keep a
    /// destroyed unit alive or read it by mistake.
    /// </summary>
    internal sealed class HealthBarView
    {
        private const float Width = 120f;
        private const float Height = 9f;
        private const float Border = 1f;
        private const float NameGap = 2f;
        private const float NameFontSize = 18f;

        private static readonly Color BackColor = new Color(0f, 0f, 0f, 0.65f);
        private static readonly Color TrailColor = new Color(0.95f, 0.85f, 0.75f, 0.9f);
        private static readonly Color FillColor = new Color(0.82f, 0.16f, 0.13f, 1f);
        private static readonly Color NameShadowColor = new Color(0f, 0f, 0f, 0.9f);
        private static readonly Vector2 NameShadowOffset = new Vector2(1.5f, -1.5f);

        private readonly GameObject _root;
        private readonly RectTransform _rootRect;
        private readonly CanvasGroup _group;
        private readonly RectTransform _fill;
        private readonly RectTransform _trail;
        private readonly GameObject _nameRoot;
        private readonly TextMeshProUGUI _name;
        private readonly TextMeshProUGUI _nameShadow;

        private string _shownName;

        public HealthBarView(Transform parent)
        {
            _root = new GameObject("Enemy Health Bar", typeof(RectTransform), typeof(CanvasGroup));
            _root.transform.SetParent(parent, false);

            _rootRect = (RectTransform)_root.transform;
            _rootRect.anchorMin = Vector2.zero;
            _rootRect.anchorMax = Vector2.zero;
            _rootRect.pivot = new Vector2(0.5f, 0f);
            _rootRect.sizeDelta = new Vector2(Width + Border * 2f, Height + Border * 2f);

            _group = _root.GetComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;

            CreateImage("Back", _rootRect, BackColor);

            RectTransform track = CreateImage("Track", _rootRect, Color.clear);
            track.offsetMin = new Vector2(Border, Border);
            track.offsetMax = new Vector2(-Border, -Border);

            _trail = CreateImage("Trail", track, TrailColor);
            _fill = CreateImage("Fill", track, FillColor);

            _nameRoot = new GameObject("Name", typeof(RectTransform));
            _nameRoot.transform.SetParent(_rootRect, false);
            RectTransform nameRect = (RectTransform)_nameRoot.transform;
            nameRect.anchorMin = new Vector2(0.5f, 1f);
            nameRect.anchorMax = new Vector2(0.5f, 1f);
            nameRect.pivot = new Vector2(0.5f, 0f);
            nameRect.anchoredPosition = new Vector2(0f, NameGap);
            nameRect.sizeDelta = new Vector2(Width * 3f, NameFontSize * 1.5f);

            // A second copy of the text rather than a font outline. Outline width depends on the
            // padding of each font's atlas: the same setting that suits the Latin font eats the
            // strokes of the Chinese one.
            _nameShadow = CreateText("Shadow", nameRect, NameShadowColor, NameShadowOffset);
            _name = CreateText("Text", nameRect, Color.white, Vector2.zero);

            Hide();
        }

        public void SetFont(TMP_FontAsset font)
        {
            if (font == null) return;

            _name.font = font;
            _nameShadow.font = font;
        }

        public void Show(Vector2 position, float scale, float alpha, float health, float trail, string name)
        {
            if (!_root.activeSelf) _root.SetActive(true);

            _rootRect.anchoredPosition = position;
            _rootRect.localScale = new Vector3(scale, scale, 1f);
            _group.alpha = alpha;

            SetWidth(_fill, health);
            SetWidth(_trail, trail);

            bool hasName = !string.IsNullOrEmpty(name);
            if (_nameRoot.activeSelf != hasName) _nameRoot.SetActive(hasName);

            if (hasName && !string.Equals(_shownName, name, System.StringComparison.Ordinal))
            {
                _shownName = name;
                _name.text = name;
                _nameShadow.text = name;
            }
        }

        public void Hide()
        {
            if (_root.activeSelf) _root.SetActive(false);
        }

        private static void SetWidth(RectTransform bar, float amount)
        {
            bar.anchorMax = new Vector2(amount, 1f);
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, Color color, Vector2 offset)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);

            RectTransform rect = (RectTransform)textObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offset;
            rect.offsetMax = offset;

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Bottom;
            text.fontSize = NameFontSize;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.color = color;
            return text;
        }

        private static RectTransform CreateImage(string name, Transform parent, Color color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);

            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            RectTransform rect = (RectTransform)imageObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
