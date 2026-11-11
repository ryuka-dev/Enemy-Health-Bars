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

        // Team marks. The mark keeps a readable size on a far or scaled-down bar: never smaller than at
        // the default scale at the edge of the default range.
        private const float MinMarkScale = 0.65f;
        private const float PopSeconds = 0.2f;
        private const float PopAmount = 0.3f;

        private readonly RectTransform _markRoot;
        private readonly Image _mark;
        private readonly Image _skullBone;
        private readonly Image _skullHoles;
        private readonly Image _eyes;
        private Sprite _skullBoneSprite;
        private Sprite _skullHolesSprite;
        private float _skullAspect = 1f;
        private int _shownMarks;
        private bool _marksFresh = true;
        private float _popStart = float.NegativeInfinity;

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
            _eyes = CreateImage("Mark Eyes", false);

            GameObject markObject = new GameObject("Mark", typeof(RectTransform));
            markObject.transform.SetParent(_rootRect, false);
            _markRoot = (RectTransform)markObject.transform;
            _markRoot.anchorMin = Vector2.zero;
            _markRoot.anchorMax = Vector2.zero;
            _markRoot.pivot = Vector2.zero;
            _markRoot.sizeDelta = Vector2.zero;
            _mark = CreateMarkImage("Mark Art");
            _skullBone = CreateMarkImage("Skull");
            _skullHoles = CreateMarkImage("Skull Holes");
            _skullBone.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _skullHoles.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            markObject.SetActive(false);
            _eyes.enabled = false;

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

            LayOutMark(style);
        }

        /// <summary>The skull cut from the game's portrait; null until it is ready, and the marks show without it.</summary>
        public void SetSkull(Sprite bone, Sprite holes, float aspect)
        {
            _skullBoneSprite = bone;
            _skullHolesSprite = holes;
            _skullAspect = aspect > 0f ? aspect : 1f;
            if (_style != null) LayOutMark(_style);
        }

        private void LayOutMark(BarStyle style)
        {
            _eyes.sprite = style.MarkEyes;
            if (style.Marks == null) return;

            // Behind: under the bar, so it seems to come out from beneath it. Otherwise over the frame and
            // under the name.
            if (style.MarkBehind) _markRoot.SetAsFirstSibling();
            else _markRoot.SetSiblingIndex(_frame.transform.GetSiblingIndex() + 1);
            _eyes.transform.SetSiblingIndex(_frame.transform.GetSiblingIndex() + 1);

            _markRoot.anchoredPosition = style.MarkPivot;
            Place(_mark.rectTransform, style.MarkRect.position - style.MarkPivot, style.MarkRect.size);

            float height = style.SkullHeight;
            Vector2 skullSize = new Vector2(height * _skullAspect, height);
            Vector2 skullAt = style.SkullCentre - style.MarkPivot;
            Quaternion tilt = Quaternion.Euler(0f, 0f, style.SkullAngle);
            Place(_skullBone.rectTransform, skullAt, skullSize);
            Place(_skullHoles.rectTransform, skullAt, skullSize);
            _skullBone.rectTransform.localRotation = tilt;
            _skullHoles.rectTransform.localRotation = tilt;

            _skullBone.sprite = _skullBoneSprite;
            _skullHoles.sprite = _skullHolesSprite;
            _skullBone.color = style.SkullBoneColor;
            _skullHoles.color = style.SkullHoleColor;
            _skullBone.enabled = height > 0f && _skullBoneSprite != null;
            _skullHoles.enabled = height > 0f && _skullHolesSprite != null && style.SkullHoleColor.a > 0f;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>
        /// Places and draws the bar. <paramref name="marks"/> is how many other players marked the enemy;
        /// returns whether a mark is drawn.
        /// </summary>
        public bool Show(Vector2 position, float scale, float alpha, float health, float trail, string name, int marks)
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

            return ShowMarks(marks, scale);
        }

        private bool ShowMarks(int marks, float scale)
        {
            bool show = marks > 0 && _style.Marks != null;
            if (_markRoot.gameObject.activeSelf != show) _markRoot.gameObject.SetActive(show);
            if (_eyes.enabled != (show && _style.MarkEyes != null)) _eyes.enabled = show && _style.MarkEyes != null;

            // A bar that comes up on an enemy already marked takes over the mark: no pop for that.
            if (_marksFresh) _marksFresh = false;
            else if (marks > _shownMarks) _popStart = Time.unscaledTime;
            _shownMarks = marks;

            if (!show) return false;

            _mark.sprite = _style.Marks[Mathf.Min(marks, _style.Marks.Length) - 1];

            float markScale = Mathf.Max(1f, MinMarkScale / Mathf.Max(scale, 0.01f));
            float sincePop = Time.unscaledTime - _popStart;
            if (sincePop < PopSeconds) markScale *= 1f + PopAmount * (1f - sincePop / PopSeconds);
            _markRoot.localScale = new Vector3(markScale, markScale, 1f);
            return true;
        }

        public void Hide()
        {
            _marksFresh = true;
            _shownMarks = 0;
            _popStart = float.NegativeInfinity;
            if (_root.activeSelf) _root.SetActive(false);
        }

        private Image CreateMarkImage(string name)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(_markRoot, false);

            RectTransform rect = (RectTransform)imageObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;

            Image image = imageObject.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
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
