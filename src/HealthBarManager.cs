using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GameFontStyle = PerfectRandom.Sulfur.Core.FontStyle;

namespace EnemyHealthBars
{
    /// <summary>
    /// Owns the overlay canvas, the bar pool and the per-enemy cache. Reads the game's own enemy list,
    /// never writes game state, and needs no game event to notice damage: comparing health works the
    /// same for local damage and for a co-op client, where damage from other players does not run the
    /// local damage path.
    /// </summary>
    internal sealed class HealthBarManager : MonoBehaviour
    {
        private const string BattleImprovementsGuid = "BattleImprove";

        private const int MaxBars = 32;
        private const float ScanInterval = 0.1f;
        private const float SightInterval = 0.15f;
        private const float ShowSeconds = 6f;
        private const float FadeSeconds = 0.75f;
        private const float AlphaPerSecond = 8f;
        private const float TrailDelayPerSecond = 0.6f;
        private const float HealthEpsilon = 0.0005f;
        private const float ScreenMargin = 0.03f;
        private const float BarHeightOnScreen = 0.05f;
        private const float MinDistanceScale = 0.65f;
        private const float StealMargin = 3f;
        private const float FontRetrySeconds = 1f;
        private const float ErrorLogSeconds = 10f;

        private static HealthBarManager _instance;

        private readonly Dictionary<int, TrackedEnemy> _byId = new Dictionary<int, TrackedEnemy>();
        private readonly List<TrackedEnemy> _tracked = new List<TrackedEnemy>();
        private readonly Stack<HealthBarView> _freeViews = new Stack<HealthBarView>();

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private int _viewsCreated;
        private int _scan;
        private float _nextScanTime;

        private TMP_FontAsset _font;
        private bool _languageDirty = true;
        private float _nextFontAttempt;
        private AsyncAssetLoading _languageSource;

        private float _nextErrorLog;
        private bool _overlapChecked;

        public static void EnsureExists()
        {
            if (_instance != null) return;

            GameObject go = new GameObject("Enemy Health Bars");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<HealthBarManager>();
        }

        public static void Shutdown()
        {
            if (_instance == null) return;

            HealthBarManager instance = _instance;
            _instance = null;
            Destroy(instance.gameObject);
        }

        private void Awake()
        {
            GameObject canvasObject = new GameObject("Enemy Health Bars Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);

            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceOverlay;
            // Under the game's own screens, so a menu or the HUD always covers a bar.
            _canvas.sortingOrder = -100;
            _canvas.enabled = false;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            _canvasRect = (RectTransform)canvasObject.transform;
        }

        private void OnDestroy()
        {
            if (_languageSource != null)
            {
                _languageSource.onLanguageChange =
                    (AsyncAssetLoading.OnLanguageChange)Delegate.Remove(
                        _languageSource.onLanguageChange,
                        new AsyncAssetLoading.OnLanguageChange(OnLanguageChange));
            }
        }

        // After the frame's animation and movement, so a bar does not trail the head it follows.
        private void LateUpdate()
        {
            try
            {
                Tick();
            }
            catch (Exception e)
            {
                _canvas.enabled = false;

                if (Time.unscaledTime >= _nextErrorLog)
                {
                    _nextErrorLog = Time.unscaledTime + ErrorLogSeconds;
                    Plugin.Log.LogError("Health bar update failed: " + e);
                }
            }
        }

        private void Tick()
        {
            WarnAboutOverlapOnce();
            FollowLanguageChanges();

            GameManager game = StaticInstance<GameManager>.Instance;
            Unit player;
            Camera camera;
            if (!TryGetLiveView(game, out player, out camera))
            {
                _canvas.enabled = false;
                return;
            }

            _canvas.enabled = true;

            if (_languageDirty || _font == null) RefreshFont();

            float now = Time.time;
            if (now >= _nextScanTime)
            {
                _nextScanTime = now + ScanInterval;
                Scan(game.aliveNpcs, now);
            }

            UpdateBars(game, player, camera, now);
        }

        /// <summary>
        /// True only while the player is looking at the world through their own camera. The game state
        /// alone is not enough. A co-op session never pauses, so the inventory, the pause menu and
        /// dialogue keep it at Running, and the controller lock is what they have in common. It also
        /// covers holding an interact key and being caught by a tornado, which hides the bars for that
        /// moment: a small price, since the player is locked anyway. The camera test covers a cutscene,
        /// which does not always stay in the Cinematic state: releasing any lock during one sets the
        /// state to Running. Scenes that only hide the HUD count too.
        /// </summary>
        private static bool TryGetLiveView(GameManager game, out Unit player, out Camera camera)
        {
            player = null;
            camera = null;

            if (game == null || game.gameState != GameState.Running || game.awaitingStartLevel) return false;
            if (game.PlayerControllerLocked) return false;

            Player script = game.PlayerScript;
            player = game.PlayerUnit;
            // Alive, not IsAlive: a downed co-op player is Incapacitated, which IsAlive accepts.
            if (script == null || player == null || player.UnitState != UnitState.Alive) return false;
            if (script.HudHiddenBy(LockStatePadlock.Cinematic) || script.HudHiddenBy(LockStatePadlock.Vehicle)) return false;

            camera = game.currentCamera;
            return camera != null && camera == script.playerCamera && camera.isActiveAndEnabled;
        }

        private void Scan(List<Npc> npcs, float now)
        {
            _scan++;

            for (int i = 0; i < npcs.Count; i++)
            {
                // The list can hold destroyed units until the next level generates.
                Npc npc = npcs[i];
                if (npc == null) continue;

                TrackedEnemy enemy;
                if (!_byId.TryGetValue(npc.GetInstanceID(), out enemy))
                {
                    if (!EnemyRules.CanEverShow(npc)) continue;

                    enemy = new TrackedEnemy(npc);
                    _byId.Add(enemy.Id, enemy);
                    _tracked.Add(enemy);
                }

                enemy.SeenInScan = _scan;

                float health = npc.normalizedHealth;
                if (float.IsNaN(health)) continue;

                // The share of health, not the amount: a max health change rescales the amount
                // without any damage being done. The reference only falls once a loss registers,
                // so slow damage over time adds up instead of being lost between two scans.
                if (health < enemy.LastHealth - HealthEpsilon)
                {
                    enemy.LastHurtTime = now;
                    if (Plugin.DebugLog.Value)
                        Plugin.Log.LogInfo("[Bars] hurt " + npc.name + " " + enemy.LastHealth + " -> " + health);
                    enemy.LastHealth = health;
                }
                else if (health > enemy.LastHealth)
                {
                    enemy.LastHealth = health;
                }
            }

            for (int i = _tracked.Count - 1; i >= 0; i--)
            {
                TrackedEnemy enemy = _tracked[i];
                if (enemy.SeenInScan == _scan && enemy.Npc != null) continue;

                ReleaseView(enemy);
                _byId.Remove(enemy.Id);
                _tracked[i] = _tracked[_tracked.Count - 1];
                _tracked.RemoveAt(_tracked.Count - 1);
            }
        }

        private void UpdateBars(GameManager game, Unit player, Camera camera, float now)
        {
            Vector3 eye = camera.transform.position;
            float maxDistance = Mathf.Clamp(Plugin.MaxDistance.Value, 5f, 100f);
            float barScale = Mathf.Clamp(Plugin.BarScale.Value, 0.5f, 2f);
            bool showName = Plugin.ShowName.Value;
            bool nearby = Plugin.Mode.Value == BarDisplayMode.Nearby;
            int occlusionMask = game.enemyOcclusionLayers;
            TrackedEnemy nearestWaiting = null;

            for (int i = 0; i < _tracked.Count; i++)
            {
                TrackedEnemy enemy = _tracked[i];
                if (enemy.Npc == null)
                {
                    ReleaseView(enemy);
                    continue;
                }

                float timeAlpha = nearby ? 1f : DamagedAlpha(now - enemy.LastHurtTime);
                if (timeAlpha <= 0f)
                {
                    ReleaseView(enemy);
                    continue;
                }

                Vector3 anchor = enemy.Anchor();
                float distance = Vector3.Distance(eye, anchor);
                enemy.Distance = distance;
                if (distance > maxDistance || !EnemyRules.CanShowNow(enemy, player))
                {
                    ReleaseView(enemy);
                    continue;
                }

                Vector3 viewport = camera.WorldToViewportPoint(anchor);
                KeepBarOnScreen(ref viewport, camera, enemy, barScale);
                if (viewport.z <= 0f || !IsOnScreen(viewport))
                {
                    ReleaseView(enemy);
                    continue;
                }

                if (now >= enemy.NextSightCheck)
                {
                    // Spread by id so a crowd does not cast all its rays in the same frame.
                    enemy.NextSightCheck = now + SightInterval + (enemy.Id & 7) * 0.01f;
                    enemy.InSight = HasLineOfSight(eye, enemy, anchor, occlusionMask);
                }

                float targetAlpha = enemy.InSight ? timeAlpha : 0f;
                enemy.Alpha = Mathf.MoveTowards(enemy.Alpha, targetAlpha, AlphaPerSecond * Time.deltaTime);
                if (enemy.Alpha <= 0f && targetAlpha <= 0f)
                {
                    ReleaseView(enemy);
                    continue;
                }

                if (enemy.View == null && !TryAcquireView(enemy))
                {
                    if (nearestWaiting == null || distance < nearestWaiting.Distance) nearestWaiting = enemy;
                    continue;
                }

                float health = enemy.Npc.normalizedHealth;
                health = float.IsNaN(health) ? 0f : Mathf.Clamp01(health);
                enemy.Trail = Mathf.Max(health, Mathf.MoveTowards(enemy.Trail, health, TrailDelayPerSecond * Time.deltaTime));

                float distanceScale = Mathf.Lerp(1f, MinDistanceScale, distance / maxDistance);
                Vector2 position = new Vector2(
                    viewport.x * _canvasRect.rect.width,
                    viewport.y * _canvasRect.rect.height);

                string name = null;
                if (showName)
                {
                    // LocalizedIdentifier, not GetActorName: the latter marks untranslated names
                    // with a debug suffix in developer mode.
                    if (enemy.Name == null) enemy.Name = enemy.Npc.unitSO.LocalizedIdentifier();
                    name = enemy.Name;
                }

                enemy.View.Show(position, barScale * distanceScale, enemy.Alpha, health, enemy.Trail, name);
            }

            if (nearestWaiting != null && TryTakeFromFarther(nearestWaiting.Distance))
                TryAcquireView(nearestWaiting);
        }

        /// <summary>
        /// Standing next to an enemy puts its head at or above the top of the screen while the enemy
        /// fills the view. The bar is held below the edge, with room for the name, for as long as any
        /// of the enemy is in view.
        /// </summary>
        private static void KeepBarOnScreen(ref Vector3 viewport, Camera camera, TrackedEnemy enemy, float barScale)
        {
            float highest = 1f - BarHeightOnScreen * barScale;
            if (viewport.z <= 0f || viewport.y <= highest) return;

            if (viewport.y <= 1f + ScreenMargin || IsInView(camera, enemy.Npc.GetPositionToAimAt())
                                                 || IsInView(camera, enemy.Npc.transform.position))
            {
                viewport.y = highest;
            }
        }

        private static bool IsInView(Camera camera, Vector3 world)
        {
            Vector3 viewport = camera.WorldToViewportPoint(world);
            return viewport.z > 0f && IsOnScreen(viewport);
        }

        private static float DamagedAlpha(float secondsSinceHurt)
        {
            if (secondsSinceHurt <= ShowSeconds) return 1f;
            return Mathf.Clamp01(1f - (secondsSinceHurt - ShowSeconds) / FadeSeconds);
        }

        private static bool IsOnScreen(Vector3 viewport)
        {
            return viewport.x >= -ScreenMargin && viewport.x <= 1f + ScreenMargin
                   && viewport.y >= -ScreenMargin && viewport.y <= 1f + ScreenMargin;
        }

        /// <summary>
        /// Uses the layers enemies themselves use for sight. Two points, because the game's aim point
        /// is a fixed height over the unit's feet: under a low overhang it sits in the ceiling, and on
        /// a tall unit it can be behind cover the head clears. The second point is the head rather
        /// than the bar, which floats above it and would see over cover the enemy is behind.
        /// </summary>
        private static bool HasLineOfSight(Vector3 eye, TrackedEnemy enemy, Vector3 anchor, int mask)
        {
            Vector3 head = anchor - Vector3.up * TrackedEnemy.GapAboveHead;
            return IsClear(eye, enemy.Npc.GetPositionToAimAt(), mask) || IsClear(eye, head, mask);
        }

        private static bool IsClear(Vector3 from, Vector3 to, int mask)
        {
            Vector3 direction = to - from;
            float length = direction.magnitude;
            if (length < 0.01f) return true;

            return !Physics.Raycast(from, direction / length, length, mask, QueryTriggerInteraction.Ignore);
        }

        private bool TryAcquireView(TrackedEnemy enemy)
        {
            if (_freeViews.Count == 0 && _viewsCreated < MaxBars)
            {
                HealthBarView created = new HealthBarView(_canvasRect);
                created.SetFont(_font);
                _viewsCreated++;
                _freeViews.Push(created);
            }

            if (_freeViews.Count == 0) return false;

            enemy.View = _freeViews.Pop();
            if (Plugin.DebugLog.Value)
                Plugin.Log.LogInfo("[Bars] show " + enemy.Npc.name + " at " + enemy.LastHealth);
            return true;
        }

        /// <summary>
        /// With every bar in use, the nearest enemy without one takes it from a clearly farther enemy.
        /// Runs once a frame, for that enemy alone, and only across a margin, so two enemies at the
        /// same range do not trade a bar back and forth and a crowd does not search the list per enemy.
        /// </summary>
        private bool TryTakeFromFarther(float distance)
        {
            TrackedEnemy farthest = null;
            float farthestDistance = distance + StealMargin;

            for (int i = 0; i < _tracked.Count; i++)
            {
                TrackedEnemy other = _tracked[i];
                if (other.View == null || other.Distance <= farthestDistance) continue;

                farthest = other;
                farthestDistance = other.Distance;
            }

            if (farthest == null) return false;

            ReleaseView(farthest);
            return true;
        }

        private void ReleaseView(TrackedEnemy enemy)
        {
            enemy.Alpha = 0f;

            if (enemy.View == null) return;

            enemy.View.Hide();
            _freeViews.Push(enemy.View);
            enemy.View = null;
        }

        private void FollowLanguageChanges()
        {
            if (_languageSource != null) return;

            AsyncAssetLoading loading = StaticInstance<AsyncAssetLoading>.Instance;
            if (loading == null) return;

            _languageSource = loading;
            loading.onLanguageChange =
                (AsyncAssetLoading.OnLanguageChange)Delegate.Combine(
                    loading.onLanguageChange,
                    new AsyncAssetLoading.OnLanguageChange(OnLanguageChange));
        }

        private void OnLanguageChange()
        {
            _languageDirty = true;
        }

        /// <summary>
        /// Takes the font the game itself resolved for the current language, which is the only way to
        /// have every script the game supports. It also drops the cached names, which are translated.
        /// </summary>
        private void RefreshFont()
        {
            if (Time.unscaledTime < _nextFontAttempt) return;
            _nextFontAttempt = Time.unscaledTime + FontRetrySeconds;

            AsyncAssetLoading loading = StaticInstance<AsyncAssetLoading>.Instance;
            if (loading == null) return;

            int style = (int)GameFontStyle.RoundSemibold;
            if (loading.fontCache[style] == null) FontLocalizer.LoadFontCache();

            TMP_FontAsset font = loading.fontCache[style];
            if (font == null) return;

            _font = font;
            _languageDirty = false;

            foreach (HealthBarView view in _freeViews) view.SetFont(font);
            for (int i = 0; i < _tracked.Count; i++)
            {
                _tracked[i].Name = null;
                _tracked[i].View?.SetFont(font);
            }
        }

        private void WarnAboutOverlapOnce()
        {
            if (_overlapChecked) return;
            _overlapChecked = true;

            if (Chainloader.PluginInfos.ContainsKey(BattleImprovementsGuid))
            {
                Plugin.Log.LogWarning("Battle Improvements is installed and has its own enemy health bar. " +
                                      "Turn off EnableHealthBar in its settings to avoid two bars on each enemy.");
            }
        }
    }
}
