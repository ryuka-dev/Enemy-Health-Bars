using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using PerfectRandom.Sulfur.Core;
using UnityEngine;

namespace EnemyHealthBars
{
    public enum BarStyleId
    {
        Sulfur = 1,
        Rounds = 2,
        Ichor = 3,
        Bone = 4,
        Classic = 5
    }

    /// <summary>
    /// One look for the bar. Sprites all share the bar's size, so a view stacks them without any
    /// layout of its own. Artwork comes from art/generate_styles.py.
    /// </summary>
    internal sealed class BarStyle
    {
        private static readonly Color HudRed = new Color(35f / 51f, 0.2980392f, 0.290196f, 1f);
        private static readonly Color PaleTrail = new Color(0.96f, 0.85f, 0.66f, 0.9f);
        private static readonly Color SkullColor = new Color(0.94f, 0.91f, 0.63f, 1f);

        public BarStyleId Id { get; private set; }
        public Vector2 Size { get; private set; }

        /// <summary>Where the empty and the full bar end, as a share of the sprite's width.</summary>
        public float FillStart { get; private set; }
        public float FillEnd { get; private set; }

        /// <summary>Whole steps the bar moves in, or 0 for a smooth bar.</summary>
        public int Steps { get; private set; }

        /// <summary>The frame follows the game's UI color, which changes with the environment.</summary>
        public bool FollowsUiColor { get; private set; }

        public Color FillColor { get; private set; }
        public Color TrailColor { get; private set; }
        public Color NameColor { get; private set; }

        public Sprite Back { get; private set; }
        public Sprite Fill { get; private set; }
        public Sprite Frame { get; private set; }

        /// <summary>The team mark for one, two and three other players; null if the style has none.</summary>
        public Sprite[] Marks { get; private set; }

        /// <summary>Drawn over the bar while it shows a mark (Bone's orange eyes); null for most styles.</summary>
        public Sprite MarkEyes { get; private set; }

        /// <summary>Where the mark sprite and its skull go, in canvas units with y up from the bar's bottom left.</summary>
        public Rect MarkRect { get; private set; }

        /// <summary>The point the mark scales around (its pop, and its minimum size), same units.</summary>
        public Vector2 MarkPivot { get; private set; }

        public bool MarkBehind { get; private set; }

        /// <summary>The skull's centre; its height is 0 for a style without one.</summary>
        public Vector2 SkullCentre { get; private set; }
        public float SkullHeight { get; private set; }
        public float SkullAngle { get; private set; }

        /// <summary>Tint of the skull's bone, and of its sockets and mouth (alpha 0 leaves them see-through).</summary>
        public Color SkullBoneColor { get; private set; }
        public Color SkullHoleColor { get; private set; }

        public float FillAmount(float health)
        {
            if (health <= 0f) return 0f;

            float amount = Mathf.Clamp01(health);
            if (Steps > 0) amount = Mathf.Ceil(amount * Steps) / Steps;
            return Mathf.Lerp(FillStart, FillEnd, amount);
        }

        private static readonly Dictionary<BarStyleId, BarStyle> Loaded = new Dictionary<BarStyleId, BarStyle>();
        private static readonly List<UnityEngine.Object> Owned = new List<UnityEngine.Object>();
        private static MethodInfo _decodeImage;

        /// <summary>
        /// The style for a setting. A style that fails to load falls back to one that did, so a bad
        /// asset costs a look and not the bars. Null only if no artwork loads at all.
        /// </summary>
        public static BarStyle Get(BarStyleId id)
        {
            BarStyle style;
            if (Loaded.TryGetValue(id, out style)) return style;

            style = Load(id);
            if (style == null && id != BarStyleId.Classic) style = Get(BarStyleId.Classic);
            Loaded[id] = style;
            return style;
        }

        public static void Unload()
        {
            foreach (UnityEngine.Object asset in Owned) UnityEngine.Object.Destroy(asset);
            Owned.Clear();
            Loaded.Clear();
        }

        private static BarStyle Load(BarStyleId id)
        {
            BarStyle style = Describe(id);
            if (style == null)
            {
                Plugin.Log.LogError("Unknown bar style " + id + ".");
                return null;
            }

            try
            {
                style.Id = id;
                style.Back = LoadSprite(id, "back");
                style.Fill = LoadSprite(id, "fill");
                style.Frame = LoadSprite(id, "frame");
                style.Size = style.Back.rect.size / BarArt.PixelsPerCanvasUnit;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Bar style " + id + " could not be loaded: " + e.Message);
                return null;
            }

            return style;
        }

        private bool _marksTried;

        /// <summary>
        /// Loads the team marks the first time they are wanted, so a game without an integration decodes
        /// none of their art. Returns true when this call loaded them.
        /// </summary>
        public bool EnsureMarks()
        {
            if (_marksTried) return false;
            _marksTried = true;
            LoadMarks(this);
            return Marks != null;
        }

        /// <summary>
        /// A style whose mark fails to load still draws its bars; it only shows no marks, which leaves them
        /// to SULFUR Together's own icon.
        /// </summary>
        private static void LoadMarks(BarStyle style)
        {
            MarkLayout layout;
            if (!Layouts.TryGetValue(style.Id, out layout)) return;

            try
            {
                Sprite[] marks = new Sprite[HealthBarIntegration.MaxMarks];
                for (int i = 0; i < marks.Length; i++) marks[i] = LoadSprite(style.Id, "mark" + (i + 1));
                style.MarkEyes = layout.HasEyes ? LoadSprite(style.Id, "eyes") : null;

                // The generator measures from the top left; the view from the bottom left.
                float barHeight = style.Size.y;
                Vector2 size = marks[0].rect.size / BarArt.PixelsPerCanvasUnit;
                style.MarkRect = new Rect(layout.Left, barHeight - layout.Top - size.y, size.x, size.y);
                style.MarkPivot = new Vector2(layout.PivotX, barHeight - layout.PivotY);
                style.SkullCentre = new Vector2(layout.SkullX, barHeight - layout.SkullY);
                style.SkullHeight = layout.SkullHeight;
                style.SkullAngle = layout.SkullAngle;
                style.MarkBehind = layout.Behind;
                style.SkullBoneColor = layout.SkullBone;
                style.SkullHoleColor = layout.SkullHoles;
                style.Marks = marks;
            }
            catch (Exception e)
            {
                style.Marks = null;
                style.MarkEyes = null;
                Plugin.Log.LogError("Team marks for bar style " + style.Id + " could not be loaded: " + e.Message);
            }
        }

        private struct MarkLayout
        {
            public float Left, Top, PivotX, PivotY, SkullX, SkullY, SkullHeight, SkullAngle;
            public bool Behind, HasEyes;
            public Color SkullBone, SkullHoles;
        }

        // The skull's colors: dark, or orange with dark sockets where it hangs on its own (Ichor's drip).
        private static readonly Color SkullDark = new Color(29f / 255f, 59f / 255f, 51f / 255f, 1f);
        private static readonly Color SeeThrough = new Color(0f, 0f, 0f, 0f);

        private static readonly Dictionary<BarStyleId, MarkLayout> Layouts = new Dictionary<BarStyleId, MarkLayout>
        {
            {
                BarStyleId.Sulfur, new MarkLayout
                {
                    Left = BarArt.SulfurMarkLeft, Top = BarArt.SulfurMarkTop,
                    PivotX = BarArt.SulfurMarkPivotX, PivotY = BarArt.SulfurMarkPivotY,
                    SkullX = BarArt.SulfurSkullX, SkullY = BarArt.SulfurSkullY,
                    SkullHeight = BarArt.SulfurSkullHeight, SkullAngle = BarArt.SulfurSkullAngle,
                    Behind = BarArt.SulfurMarkBehind, SkullBone = SkullDark, SkullHoles = SeeThrough
                }
            },
            {
                BarStyleId.Rounds, new MarkLayout
                {
                    Left = BarArt.RoundsMarkLeft, Top = BarArt.RoundsMarkTop,
                    PivotX = BarArt.RoundsMarkPivotX, PivotY = BarArt.RoundsMarkPivotY,
                    SkullX = BarArt.RoundsSkullX, SkullY = BarArt.RoundsSkullY,
                    SkullHeight = BarArt.RoundsSkullHeight, SkullAngle = BarArt.RoundsSkullAngle,
                    Behind = BarArt.RoundsMarkBehind, SkullBone = SkullDark, SkullHoles = SeeThrough
                }
            },
            {
                BarStyleId.Ichor, new MarkLayout
                {
                    Left = BarArt.IchorMarkLeft, Top = BarArt.IchorMarkTop,
                    PivotX = BarArt.IchorMarkPivotX, PivotY = BarArt.IchorMarkPivotY,
                    SkullX = BarArt.IchorSkullX, SkullY = BarArt.IchorSkullY,
                    SkullHeight = BarArt.IchorSkullHeight, SkullAngle = BarArt.IchorSkullAngle,
                    Behind = BarArt.IchorMarkBehind, SkullBone = SulfurConstants.COLOR_SulfurYellow, SkullHoles = SkullDark
                }
            },
            {
                BarStyleId.Bone, new MarkLayout
                {
                    Left = BarArt.BoneMarkLeft, Top = BarArt.BoneMarkTop,
                    PivotX = BarArt.BoneMarkPivotX, PivotY = BarArt.BoneMarkPivotY,
                    SkullX = BarArt.BoneSkullX, SkullY = BarArt.BoneSkullY,
                    SkullHeight = BarArt.BoneSkullHeight, SkullAngle = BarArt.BoneSkullAngle,
                    Behind = BarArt.BoneMarkBehind, HasEyes = true, SkullBone = SkullDark, SkullHoles = SeeThrough
                }
            },
            {
                BarStyleId.Classic, new MarkLayout
                {
                    Left = BarArt.ClassicMarkLeft, Top = BarArt.ClassicMarkTop,
                    PivotX = BarArt.ClassicMarkPivotX, PivotY = BarArt.ClassicMarkPivotY,
                    SkullX = BarArt.ClassicSkullX, SkullY = BarArt.ClassicSkullY,
                    SkullHeight = BarArt.ClassicSkullHeight, SkullAngle = BarArt.ClassicSkullAngle,
                    Behind = BarArt.ClassicMarkBehind, SkullBone = SkullDark, SkullHoles = SeeThrough
                }
            }
        };

        /// <summary>
        /// By reflection because the module cannot be referenced from net472: it needs netstandard
        /// 2.1, and its overloads take a type this target does not have.
        /// </summary>
        private static bool DecodeImage(Texture2D texture, byte[] bytes)
        {
            if (_decodeImage == null)
            {
                Type conversion = AccessTools.TypeByName("UnityEngine.ImageConversion");
                _decodeImage = conversion?.GetMethod("LoadImage", BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) }, null);

                if (_decodeImage == null) throw new MissingMethodException("UnityEngine.ImageConversion", "LoadImage");
            }

            return (bool)_decodeImage.Invoke(null, new object[] { texture, bytes, true });
        }

        private static BarStyle Describe(BarStyleId id)
        {
            switch (id)
            {
                case BarStyleId.Sulfur:
                    return new BarStyle
                    {
                        FillStart = BarArt.SulfurFillStart, FillEnd = BarArt.SulfurFillEnd, FollowsUiColor = true,
                        FillColor = HudRed, TrailColor = PaleTrail, NameColor = Color.white
                    };
                case BarStyleId.Rounds:
                    return new BarStyle
                    {
                        FillStart = BarArt.RoundsFillStart, FillEnd = BarArt.RoundsFillEnd,
                        Steps = BarArt.RoundsCount, FollowsUiColor = true,
                        FillColor = HudRed, TrailColor = PaleTrail, NameColor = Color.white
                    };
                case BarStyleId.Ichor:
                    return new BarStyle
                    {
                        FillStart = BarArt.IchorFillStart, FillEnd = BarArt.IchorFillEnd,
                        FillColor = HudRed, TrailColor = PaleTrail, NameColor = Color.white
                    };
                case BarStyleId.Bone:
                    return new BarStyle
                    {
                        FillStart = BarArt.BoneFillStart, FillEnd = BarArt.BoneFillEnd,
                        FillColor = HudRed, TrailColor = PaleTrail, NameColor = SkullColor
                    };
                case BarStyleId.Classic:
                    return new BarStyle
                    {
                        FillStart = BarArt.ClassicFillStart, FillEnd = BarArt.ClassicFillEnd,
                        FillColor = new Color(0.82f, 0.16f, 0.13f, 1f),
                        TrailColor = new Color(0.95f, 0.85f, 0.75f, 0.9f),
                        NameColor = Color.white
                    };
                default:
                    return null;
            }
        }

        private static Sprite LoadSprite(BarStyleId id, string layer)
        {
            string resource = "EnemyHealthBars.Styles." + id + "." + layer + ".png";

            byte[] bytes;
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            {
                if (stream == null) throw new FileNotFoundException("missing resource", resource);

                bytes = new byte[stream.Length];
                int read = 0;
                while (read < bytes.Length)
                {
                    int chunk = stream.Read(bytes, read, bytes.Length - read);
                    if (chunk <= 0) throw new EndOfStreamException(resource);
                    read += chunk;
                }
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, true)
            {
                name = resource,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear
            };

            // Without these flags the game's unused-asset sweep on a level change may destroy them.
            texture.hideFlags = HideFlags.HideAndDontSave;
            Owned.Add(texture);

            if (!DecodeImage(texture, bytes)) throw new InvalidDataException("not an image: " + resource);

            Rect rect = new Rect(0f, 0f, texture.width, texture.height);
            Sprite sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = resource;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Owned.Add(sprite);
            return sprite;
        }
    }
}
