using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace BioEden.NoDOF
{
    // Uses the game's fullscreen saturation material, then selectively redraws
    // only player structures, mineral objects, and polluted water above it.
    // Natural terrain and clean water therefore remain desaturated without a
    // second full-scene render.
    public sealed class FilterController : MonoBehaviour
    {
        public const int PreserveLayer = 31;
        private static FilterController instance;
        private static bool filterEnabled;
        private static int hotkey;
        private static readonly List<Material> materials = new List<Material>();
        private readonly Dictionary<GameObject, int> preservedLayers = new Dictionary<GameObject, int>();
        private Button button;
        private Image icon;
        private Text label;
        private GameObject iconRoot;
        private PreserveColorFeature preserveFeature;
        private Type playerType, gameHubType;
        private object player;
        private MethodInfo playerPos2Coord;
        private PropertyInfo worldGridIndexer;
        private float nextPreserveRefresh;
        private float lastSaturation = -1f;
        private bool preserveFeatureInstalled;
        private bool waterScanDone;
        public static bool IsEnabled => filterEnabled;

        public static void Ensure()
        {
            if (instance != null) return;
            var go = new GameObject("BioEden visual accessibility");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<FilterController>();
        }

        public static void SetEnabled(bool value)
        {
            filterEnabled = value;
            Ensure();
            if (instance.IsInGameWorld())
            {
                instance.RefreshMaterials(true);
                if (value)
                {
                    instance.InstallPreserveFeature();
                    instance.RefreshPreservedObjects(true);
                }
                else instance.RestorePreservedLayers();
            }
            instance.RefreshIcon();
            Debug.Log("[BioEden.NoDOF] Map Desaturation Filter: " + (value ? "On" : "Off"));
        }

        public static void SetHotkey(int value)
        {
            hotkey = Mathf.Clamp(value, 0, 2);
            Ensure();
            instance.RefreshIcon();
            Debug.Log("[BioEden.NoDOF] Map Filter Hotkey: F" + (hotkey + 1));
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
        }

        private void Update()
        {
            bool inGame = IsInGameWorld();
            if (!inGame)
            {
                if (iconRoot != null) iconRoot.SetActive(false);
                return;
            }

            if (iconRoot == null) CreateIcon();
            else if (!iconRoot.activeSelf) iconRoot.SetActive(true);

            KeyCode key = hotkey == 0 ? KeyCode.F1 : hotkey == 1 ? KeyCode.F2 : KeyCode.F3;
            if (Input.GetKeyDown(key)) SetEnabled(!filterEnabled);
            if (!filterEnabled) return;

            InstallPreserveFeature();
            RefreshMaterials(false);
            if (Time.unscaledTime >= nextPreserveRefresh)
            {
                nextPreserveRefresh = Time.unscaledTime + 2f;
                RefreshPreservedObjects(false);
            }
        }

        private bool IsInGameWorld()
        {
            var type = Type.GetType("Biomes.Cam.CameraInputIngame, Assembly-CSharp");
            return type != null && UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.None).Length > 0;
        }

        private void RefreshMaterials(bool force)
        {
            float saturation = filterEnabled ? 0f : 1f;
            if (!force && Mathf.Approximately(lastSaturation, saturation)) return;
            materials.Clear();
            try
            {
                var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                if (pipeline == null) return;
                MethodInfo getRenderer = pipeline.GetType().GetMethod("GetRenderer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (getRenderer == null) return;
                for (int i = 0; i < 8; i++)
                {
                    object renderer = getRenderer.Invoke(pipeline, new object[] { i });
                    if (renderer == null) continue;
                    var field = FindField(renderer.GetType(), "m_RendererFeatures");
                    var features = field?.GetValue(renderer) as System.Collections.IEnumerable;
                    if (features == null) continue;
                    foreach (object feature in features)
                        foreach (var material in FindMaterials(feature, 0, new HashSet<object>()))
                            if (material.HasProperty("_Saturation") && !materials.Contains(material)) materials.Add(material);
                }
                foreach (var material in materials) material.SetFloat("_Saturation", saturation);
                if (materials.Count > 0) lastSaturation = saturation;
            }
            catch (Exception e) { Debug.LogError("[BioEden.NoDOF] Map filter could not update: " + e.Message); }
        }

        private void InstallPreserveFeature()
        {
            if (preserveFeatureInstalled) return;
            try
            {
                var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                var getRenderer = pipeline?.GetType().GetMethod("GetRenderer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object renderer = getRenderer?.Invoke(pipeline, new object[] { 0 });
                if (renderer == null) return;
                var field = FindField(renderer.GetType(), "m_RendererFeatures");
                var list = field?.GetValue(renderer) as System.Collections.IList;
                if (list == null) return;
                foreach (object feature in list)
                    if (feature is PreserveColorFeature existing) { preserveFeature = existing; preserveFeatureInstalled = true; return; }
                preserveFeature = ScriptableObject.CreateInstance<PreserveColorFeature>();
                preserveFeature.name = "BioEden Preserve Selected Colors";
                list.Add(preserveFeature);
                preserveFeature.Create();
                preserveFeatureInstalled = true;
            }
            catch (Exception e) { Debug.LogError("[BioEden.NoDOF] Could not install selected color pass: " + e.Message); }
        }

        private void RefreshPreservedObjects(bool forceWaterScan)
        {
            if (!filterEnabled) { RestorePreservedLayers(); return; }
            var structureType = Type.GetType("Biomes.Structures.StructureIngame, Assembly-CSharp");
            var mineralType = Type.GetType("MineralHandler, Assembly-CSharp");
            AddRenderersUnder(structureType);
            AddRenderersUnder(mineralType);

            bool scanWater = forceWaterScan || !waterScanDone;
            if (scanWater)
            {
                foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (!renderer.enabled || renderer.gameObject.layer != LayerMask.NameToLayer("Water")) continue;
                    if (WaterIsPolluted(renderer)) AddPreservedRenderer(renderer);
                }
                waterScanDone = true;
            }
        }

        private void AddRenderersUnder(Type componentType)
        {
            if (componentType == null) return;
            foreach (var obj in UnityEngine.Object.FindObjectsByType(componentType, FindObjectsSortMode.None))
                if (obj is Component component)
                    foreach (var renderer in component.GetComponentsInChildren<Renderer>(true)) AddPreservedRenderer(renderer);
        }

        private void AddPreservedRenderer(Renderer renderer)
        {
            if (renderer == null || renderer.gameObject.layer == LayerMask.NameToLayer("UI")) return;
            var go = renderer.gameObject;
            if (!preservedLayers.ContainsKey(go)) preservedLayers.Add(go, go.layer);
            go.layer = PreserveLayer;
        }

        private void RestorePreservedLayers()
        {
            foreach (var pair in preservedLayers)
                if (pair.Key != null) pair.Key.layer = pair.Value;
            preservedLayers.Clear();
            waterScanDone = false;
        }

        private bool WaterIsPolluted(Renderer renderer)
        {
            try
            {
                if (player == null)
                {
                    playerType = Type.GetType("Biomes.Player, Assembly-CSharp");
                    var gameType = Type.GetType("Bag.GameHub.Game, Bag.GameHub");
                    gameHubType = Type.GetType("Bag.Heritage.GameSystem.GameHub`2, Bag.Heritage.GameSystem")?.MakeGenericType(gameType, playerType);
                    object hub = gameHubType?.GetProperty("Singleton", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
                    player = gameHubType?.GetProperty("Plyr", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(hub);
                    playerPos2Coord = playerType?.GetMethod("Pos2Coord", BindingFlags.Instance | BindingFlags.Public);
                }
                if (player == null || playerPos2Coord == null) return true;
                object coord = playerPos2Coord.Invoke(player, new object[] { renderer.bounds.center });
                object grid = playerType.GetProperty("WorldGrid", BindingFlags.Instance | BindingFlags.Public)?.GetValue(player);
                if (grid == null) return true;
                if (worldGridIndexer == null) worldGridIndexer = FindIndexer(grid.GetType(), coord.GetType());
                object slot = worldGridIndexer?.GetValue(grid, new[] { coord });
                if (slot == null) return true;
                var pollution = slot.GetType().GetField("pollution", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(slot);
                return pollution == null || ((Vector2)pollution).x > 0.0001f;
            }
            catch { return true; }
        }

        private static PropertyInfo FindIndexer(Type type, Type argument)
        {
            while (type != null)
            {
                var property = type.GetProperty("Item", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, null, new[] { argument }, null);
                if (property != null) return property;
                type = type.BaseType;
            }
            return null;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            while (type != null)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
                type = type.BaseType;
            }
            return null;
        }

        private static IEnumerable<Material> FindMaterials(object value, int depth, HashSet<object> visited)
        {
            if (value == null || depth > 3 || value is string) yield break;
            if (value is Material material) { yield return material; yield break; }
            if (!value.GetType().IsValueType && !visited.Add(value)) yield break;
            if (value is System.Collections.IEnumerable sequence)
            {
                foreach (object item in sequence)
                    foreach (var found in FindMaterials(item, depth + 1, visited)) yield return found;
                yield break;
            }
            foreach (var field in value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                object child;
                try { child = field.GetValue(value); } catch { continue; }
                foreach (var found in FindMaterials(child, depth + 1, visited)) yield return found;
            }
        }

        private void CreateIcon()
        {
            var root = new GameObject("Map filter button", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            iconRoot = root;
            DontDestroyOnLoad(root);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30000;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(2560, 1440);
            var go = new GameObject("Filter icon", typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(root.transform, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(1, 1); rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(1, 1); rect.anchoredPosition = new Vector2(-210, -10); rect.sizeDelta = new Vector2(54, 42);
            icon = go.GetComponent<Image>(); icon.color = new Color(0.72f, 0.78f, 0.82f, 0.94f);
            button = go.GetComponent<Button>(); button.onClick.AddListener(() => SetEnabled(!filterEnabled));
            var textGo = new GameObject("Planet filter symbol", typeof(RectTransform), typeof(Text)); textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.GetComponent<RectTransform>(); textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one; textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            label = textGo.GetComponent<Text>(); label.text = KeyLabel(); label.alignment = TextAnchor.MiddleCenter; label.fontSize = 18; label.fontStyle = FontStyle.Bold; label.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); label.color = Color.black; label.raycastTarget = false;
            foreach (var candidate in UnityEngine.Object.FindObjectsByType<Image>(FindObjectsSortMode.None))
                if (candidate.name.IndexOf("help", StringComparison.OrdinalIgnoreCase) >= 0 && candidate.sprite != null) { icon.sprite = candidate.sprite; icon.preserveAspect = true; break; }
            RefreshIcon();
        }

        private void OnDestroy()
        {
            RestorePreservedLayers();
            if (iconRoot != null) Destroy(iconRoot);
            if (instance == this) instance = null;
        }

        private void RefreshIcon()
        {
            if (icon == null) return;
            icon.color = filterEnabled ? new Color(0.35f, 0.85f, 1f, 1f) : new Color(0.72f, 0.78f, 0.82f, 0.94f);
            if (label != null) label.text = KeyLabel();
        }

        private string KeyLabel() => hotkey == 0 ? "F1" : hotkey == 1 ? "F2" : "F3";
    }
}
