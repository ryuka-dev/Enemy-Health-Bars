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
        private const float NameGap = 2f;
        private const float NameFontSize = 18f;
        private const float NameWidth = 360f;

        private static readonly Color NameShadowColor = new Color(0f, 0f, 0f, 0.9f);
        private static readonly Vector2 NameShadowOffset = new Vector2(1.5f, -1.5f);

        private readonly GameObject _root;
        private readonly RectTransform _rootRect;
        private readonly CanvasGroup _group;
        private readonly Image _back;
        private readonly Image _trail;
        private readonly Image _fill;
        private readonly Image _frame;
        private readonly GameObject _nameRoot;
        private readonly TextMeshProUGUI _name;
        private readonly TextMeshProUGUI _nameShadow;

        private BarStyle _style;
        private string _shownName;

        public HealthBarView(Transform parent)
        {
            _root = new GameObject("Enemy Health Bar", typeof(RectTransform), typeof(CanvasGroup));
            _root.transform.SetParent(parent, false);

            _rootRect = (RectTransform)_root.transform;
            _rootRect.anchorMin = Vector2.zero;
            _rootRect.anchorMax = Vector2.zero;
            _rootRect.pivot = new Vector2(0.5f, 0f);

            _group = _root.GetComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;

            _back = CreateImage("Back", false);
            _trail = CreateImage("Trail", true);
            _fill = CreateImage("Fill", true);
            _frame = CreateImage("Frame", false);

            _nameRoot = new GameObject("Name", typeof(RectTransform));
            _nameRoot.transform.SetParent(_rootRect, false);
            RectTransform nameRect = (RectTransform)_nameRoot.transform;
            nameRect.anchorMin = new Vector2(0.5f, 1f);
            nameRect.anchorMax = new Vector2(0.5f, 1f);
            nameRect.pivot = new Vector2(0.5f, 0f);
            nameRect.anchoredPosition = new Vector2(0f, NameGap);
            nameRect.sizeDelta = new Vector2(NameWidth, NameFontSize * 1.5f);

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

        public void SetStyle(BarStyle style, Color uiColor)
        {
            _style = style;

            _rootRect.sizeDelta = style.Size;
            _back.sprite = style.Back;
            _trail.sprite = style.Fill;
            _fill.sprite = style.Fill;
            _frame.sprite = style.Frame;

            _trail.color = style.TrailColor;
            _fill.color = style.FillColor;
            _frame.color = style.FollowsUiColor ? uiColor : Color.white;
            _name.color = style.NameColor;
        }

        public void Show(Vector2 position, float scale, float alpha, float health, float trail, string name)
        {
            if (!_root.activeSelf) _root.SetActive(true);

            _rootRect.anchoredPosition = position;
            _rootRect.localScale = new Vector3(scale, scale, 1f);
            _group.alpha = alpha;

            _fill.fillAmount = _style.FillAmount(health);
            _trail.fillAmount = _style.FillAmount(trail);

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

        private Image CreateImage(string name, bool filled)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(_rootRect, false);

            RectTransform rect = (RectTransform)imageObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = imageObject.GetComponent<Image>();
            image.raycastTarget = false;

            if (filled)
            {
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Horizontal;
                image.fillOrigin = (int)Image.OriginHorizontal.Left;
            }

            return image;
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
    }
}
