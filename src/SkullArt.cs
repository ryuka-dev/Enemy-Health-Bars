using System;
using System.Collections.Generic;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.Units;
using UnityEngine;

namespace EnemyHealthBars
{
    /// <summary>
    /// The skull in the team marks, cut at run time out of the game's own portrait of the Black Guild
    /// skeleton (its unit artwork, loaded through UnitSO.LoadAndSetArtwork), so no game art ships with
    /// the mod. The recipe is the one SULFUR Together uses for its icon, so both show the same skull.
    ///
    /// Only the portrait's alpha is used: crop the head, scale it up, tilt it 6 degrees clockwise,
    /// threshold, cut the neck off, keep the main piece. The result is two white masks the bars tint
    /// per style: the bone, and the eye sockets, nose and mouth inside it.
    ///
    /// Nothing loads without an integration that provides marks; with one, the portrait loads once, at the
    /// first frame the bars are live. Until it is in, or if it cannot be had, the marks show without the skull.
    /// </summary>
    internal static class SkullArt
    {
        private const string SkullUnitName = "BlackGuildSkeleton";
        private const float LoadTimeoutSeconds = 20f;

        private enum State { NotStarted, Loading, TimedOut, Done }

        private static State _state;
        private static float _loadStartedAt;
        private static readonly List<UnityEngine.Object> Owned = new List<UnityEngine.Object>();

        /// <summary>The bone, white on transparent. Null until the skull is cut, and if it cannot be.</summary>
        public static Sprite Bone { get; private set; }

        /// <summary>The sockets, nose and mouth, in the same rect as <see cref="Bone"/>.</summary>
        public static Sprite Holes { get; private set; }

        /// <summary>Width over height of both sprites.</summary>
        public static float Aspect { get; private set; } = 1f;

        /// <summary>Starts or watches the load. Cheap once it is done; called every frame while marks can show.</summary>
        public static void Tick()
        {
            if (_state == State.Done || _state == State.TimedOut) return;

            if (_state == State.Loading)
            {
                if (Time.realtimeSinceStartup - _loadStartedAt > LoadTimeoutSeconds)
                {
                    // A late callback still cuts the skull.
                    _state = State.TimedOut;
                    Plugin.Log.LogWarning("The skull portrait did not load in " + LoadTimeoutSeconds +
                                          " s; team marks show without the skull until it does.");
                }

                return;
            }

            AsyncAssetLoading loading = StaticInstance<AsyncAssetLoading>.Instance;
            UnitDatabase database = loading != null ? loading.unitDatabase : null;
            if (database == null) return; // asset loading not done yet

            UnitSO unit = null;
            try
            {
                foreach (UnitSO candidate in database.GetRawList())
                {
                    if (candidate != null && string.Equals(candidate.name, SkullUnitName, StringComparison.Ordinal))
                    {
                        unit = candidate;
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Finish(null, "unit database: " + e.Message);
                return;
            }

            if (unit == null)
            {
                Finish(null, "no unit named " + SkullUnitName);
                return;
            }

            _state = State.Loading;
            _loadStartedAt = Time.realtimeSinceStartup;
            try
            {
                unit.LoadAndSetArtwork(OnPortrait);
            }
            catch (Exception e)
            {
                Finish(null, "LoadAndSetArtwork: " + e.Message);
            }
        }

        public static void Unload()
        {
            foreach (UnityEngine.Object asset in Owned)
            {
                if (asset != null) UnityEngine.Object.Destroy(asset);
            }

            Owned.Clear();
            Bone = null;
            Holes = null;
            Aspect = 1f;
            _state = State.NotStarted;
        }

        /// <summary>Stops trying for good, after loading the portrait failed in a way Tick could not catch.</summary>
        public static void GiveUp(Exception e)
        {
            if (_state == State.Done) return;
            _state = State.Done;
            Plugin.Log.LogWarning("The skull portrait cannot be loaded (" + e.GetType().Name + ": " + e.Message +
                                  "); team marks show without the skull.");
        }

        private static void OnPortrait(Sprite portrait)
        {
            // A callback that arrives after Unload belongs to a load nobody waits for any more.
            if (_state != State.Loading && _state != State.TimedOut) return;
            Finish(portrait, portrait == null ? "the portrait loaded empty" : null);
        }

        private static void Finish(Sprite portrait, string why)
        {
            if (_state == State.Done) return;
            _state = State.Done;

            if (portrait != null)
            {
                try
                {
                    float[] bone, holes;
                    int w, h;
                    CutSkull(portrait, out bone, out holes, out w, out h);
                    Bone = MakeSprite(bone, w, h, "EnemyHealthBars.Skull.Bone");
                    Holes = MakeSprite(holes, w, h, "EnemyHealthBars.Skull.Holes");
                    Aspect = (float)w / h;
                    Plugin.Log.LogInfo("Team mark skull cut from " + portrait.name + ", " + w + "x" + h + ".");
                    return;
                }
                catch (Exception e)
                {
                    why = e.GetType().Name + ": " + e.Message;
                    Bone = null;
                    Holes = null;
                }
            }

            Plugin.Log.LogWarning("The skull portrait is unavailable (" + why + "); team marks show without the skull.");
        }

        private static Sprite MakeSprite(float[] coverage, int w, int h, string name)
        {
            // White everywhere, so filtering never blends an edge toward another color.
            Color32[] pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(coverage[y * w + x] * 255f), 0, 255);
                    pixels[(h - 1 - y) * w + x] = new Color32(255, 255, 255, a); // to Unity's bottom-up rows
                }
            }

            Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            Owned.Add(texture);
            texture.SetPixels32(pixels);
            texture.Apply(true, true);

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Owned.Add(sprite);
            return sprite;
        }

        // The portrait is 256 x 256; the head is in (30, 0)-(200, 140) from the top left. Cut at S times that.
        private const int S = 2;
        private const float Tilt = 6f;           // degrees, clockwise on screen
        private const float AlphaCut = 110f / 255f;

        /// <summary>Coverage masks (row 0 = top) of the bone and of the holes inside it.</summary>
        private static void CutSkull(Sprite portrait, out float[] bone, out float[] holes, out int w, out int h)
        {
            if (portrait.packed && (portrait.packingMode == SpritePackingMode.Tight ||
                                    portrait.packingRotation != SpritePackingRotation.None))
            {
                throw new InvalidOperationException("the portrait is tightly packed or rotated in an atlas");
            }

            int tw, th;
            float[] src = ReadAlpha(portrait, out tw, out th);

            // The recipe is in 256 x 256 portrait pixels of the sprite's own rect; an atlas may have trimmed
            // the transparent border, leaving the pixels at textureRectOffset inside that rect.
            float pw = portrait.rect.width, ph = portrait.rect.height;
            Vector2 offset = portrait.textureRectOffset;
            float fx = pw / 256f, fy = ph / 256f;
            float topTrim = ph - (offset.y + th);
            int W = 170 * S, H = 140 * S;
            bool[] solid = new bool[W * H];
            float c = Mathf.Cos(Tilt * Mathf.Deg2Rad), s = Mathf.Sin(Tilt * Mathf.Deg2Rad);
            float cx = W / 2f, cy = H / 2f;
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    // Inverse of a clockwise tilt (y grows downwards), then back to portrait pixels.
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float ix = dx * c + dy * s + cx, iy = -dx * s + dy * c + cy;
                    float sx = (30f + ix / S) * fx - offset.x, sy = iy / S * fy - topTrim;
                    solid[y * W + x] = SampleTopLeft(src, tw, th, sx, sy) > AlphaCut;
                }
            }

            // Cut the neck: below the jaw the rows narrow sharply.
            int bottom = H;
            bool seen = false;
            for (int y = 0; y < H; y++)
            {
                int first = -1, last = -1;
                for (int x = 0; x < W; x++)
                {
                    if (!solid[y * W + x]) continue;
                    if (first < 0) first = x;
                    last = x;
                }

                int width = first < 0 ? 0 : last - first + 1;
                if (width > 90 * S) seen = true;
                if (seen && width < 55 * S && y > 70 * S)
                {
                    bottom = y;
                    break;
                }
            }

            H = bottom;

            // Everything not reachable from outside through empty pixels is the silhouette; its empty part
            // is the holes.
            bool[] outside = new bool[W * H];
            Stack<int> stack = new Stack<int>();
            Action<int, int> seed = (x, y) =>
            {
                int i = y * W + x;
                if (!outside[i] && !solid[i])
                {
                    outside[i] = true;
                    stack.Push(i);
                }
            };
            for (int x = 0; x < W; x++)
            {
                seed(x, 0);
                seed(x, H - 1);
            }

            for (int y = 0; y < H; y++)
            {
                seed(0, y);
                seed(W - 1, y);
            }

            while (stack.Count > 0)
            {
                int i = stack.Pop(), x = i % W, y = i / W;
                if (x > 0) seed(x - 1, y);
                if (x < W - 1) seed(x + 1, y);
                if (y > 0) seed(x, y - 1);
                if (y < H - 1) seed(x, y + 1);
            }

            // Crop to the silhouette, at most 166 x 99, and keep its main piece.
            int bx0, by0, bx1, by1;
            Bounds(outside, W, H, out bx0, out by0, out bx1, out by1);
            int cw = Math.Min(bx1 - bx0 + 1, 665 * S / 4), ch = Math.Min(by1 - by0 + 1, 398 * S / 4);
            bool[] silhouette = new bool[cw * ch];
            for (int y = 0; y < ch; y++)
            {
                for (int x = 0; x < cw; x++) silhouette[y * cw + x] = !outside[(y + by0) * W + x + bx0];
            }

            bool[] main = MainPiece(silhouette, cw, ch, cw / 2, 10 * S);
            int mx0, my0, mx1, my1;
            Bounds(Invert(main), cw, ch, out mx0, out my0, out mx1, out my1);

            w = mx1 - mx0 + 1;
            h = my1 - my0 + 1;
            bone = new float[w * h];
            holes = new float[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!main[(y + my0) * cw + x + mx0]) continue;
                    if (solid[(y + my0 + by0) * W + x + mx0 + bx0]) bone[y * w + x] = 1f;
                    else holes[y * w + x] = 1f;
                }
            }
        }

        /// <summary>The sprite's alpha, row 0 = bottom. The texture is not CPU-readable, so it goes through a
        /// render texture.</summary>
        private static float[] ReadAlpha(Sprite sprite, out int w, out int h)
        {
            Texture2D source = sprite.texture;
            Rect r = sprite.textureRect;
            w = Mathf.RoundToInt(r.width);
            h = Mathf.RoundToInt(r.height);
            if (w <= 0 || h <= 0) throw new InvalidOperationException("empty portrait");

            RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            Texture2D read = null;
            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                read = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
                read.ReadPixels(new Rect(r.x, r.y, w, h), 0, 0, false);
                read.Apply(false);
                Color32[] pixels = read.GetPixels32();
                float[] alpha = new float[pixels.Length];
                for (int i = 0; i < pixels.Length; i++) alpha[i] = pixels[i].a / 255f;
                return alpha;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                if (read != null) UnityEngine.Object.Destroy(read);
            }
        }

        /// <summary>Bilinear alpha at (x, y) measured from the top left in pixels; outside is transparent.</summary>
        private static float SampleTopLeft(float[] bottomUp, int w, int h, float x, float y)
        {
            float u = x - 0.5f, v = h - y - 0.5f;
            int x0 = Mathf.FloorToInt(u), y0 = Mathf.FloorToInt(v);
            float tx = u - x0, ty = v - y0;
            return Mathf.Lerp(
                Mathf.Lerp(At(bottomUp, w, h, x0, y0), At(bottomUp, w, h, x0 + 1, y0), tx),
                Mathf.Lerp(At(bottomUp, w, h, x0, y0 + 1), At(bottomUp, w, h, x0 + 1, y0 + 1), tx), ty);
        }

        private static float At(float[] m, int w, int h, int x, int y)
        {
            return x < 0 || y < 0 || x >= w || y >= h ? 0f : m[y * w + x];
        }

        private static bool[] Invert(bool[] m)
        {
            bool[] r = new bool[m.Length];
            for (int i = 0; i < m.Length; i++) r[i] = !m[i];
            return r;
        }

        /// <summary>The bounding box of the cells that are false.</summary>
        private static void Bounds(bool[] empty, int w, int h, out int x0, out int y0, out int x1, out int y1)
        {
            x0 = w;
            y0 = h;
            x1 = -1;
            y1 = -1;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (empty[y * w + x]) continue;
                    if (x < x0) x0 = x;
                    if (x > x1) x1 = x;
                    if (y < y0) y0 = y;
                    if (y > y1) y1 = y;
                }
            }

            if (x1 < 0) throw new InvalidOperationException("no skull in the portrait");
        }

        /// <summary>The 4-connected piece of <paramref name="m"/> containing (sx, sy), or the largest piece when
        /// that cell is empty.</summary>
        private static bool[] MainPiece(bool[] m, int w, int h, int sx, int sy)
        {
            int[] label = new int[m.Length];
            int best = 0, bestSize = 0, next = 0;
            Stack<int> stack = new Stack<int>();
            for (int i = 0; i < m.Length; i++)
            {
                if (!m[i] || label[i] != 0) continue;

                int id = ++next, size = 0;
                label[i] = id;
                stack.Push(i);
                while (stack.Count > 0)
                {
                    int j = stack.Pop(), x = j % w, y = j / w;
                    size++;
                    if (x > 0) Visit(m, label, stack, j - 1, id);
                    if (x < w - 1) Visit(m, label, stack, j + 1, id);
                    if (y > 0) Visit(m, label, stack, j - w, id);
                    if (y < h - 1) Visit(m, label, stack, j + w, id);
                }

                if (size > bestSize)
                {
                    bestSize = size;
                    best = id;
                }
            }

            int start = sx >= 0 && sy >= 0 && sx < w && sy < h ? label[sy * w + sx] : 0;
            int keep = start != 0 ? start : best;
            bool[] r = new bool[m.Length];
            for (int i = 0; i < m.Length; i++) r[i] = label[i] == keep;
            return r;
        }

        private static void Visit(bool[] m, int[] label, Stack<int> stack, int k, int id)
        {
            if (!m[k] || label[k] != 0) return;
            label[k] = id;
            stack.Push(k);
        }
    }
}
