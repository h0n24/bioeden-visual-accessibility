using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.VFX;

namespace BioEden.NoDOF
{
    // Uses the game's fullscreen saturation material, then selectively redraws
    // player structures, mineral objects, and polluted water above it. Clean
    // water is also given a temporary grayscale material because the game's
    // water shader can render after the fullscreen adjustment pass.
    public sealed class FilterController : MonoBehaviour
    {
        public const int PreserveLayer = 31;
        // The game's water inspection displays the feature average as a whole
        // percentage (P0). Keep the same zero band so a feature shown as 0%
        // is treated as clean even when its raw average is a tiny fraction.
        private const float WaterDisplayZeroThreshold = 0.005f;
        private static FilterController instance;
        private static bool filterEnabled;
        private static bool startupGateComplete;
        private static int hotkey;
        private static readonly List<Material> materials = new List<Material>();
        private readonly Dictionary<GameObject, int> preservedLayers = new Dictionary<GameObject, int>();
        private readonly Dictionary<Camera, int> cameraMasks = new Dictionary<Camera, int>();
        private readonly Dictionary<Renderer, Material[]> cleanWaterMaterials = new Dictionary<Renderer, Material[]>();
        private readonly List<Material> waterMaterialClones = new List<Material>();
        private sealed class HiddenAtmosphere
        {
            public VisualEffect Effect;
            public Renderer Renderer;
            public bool WasPaused;
            public bool WasHidden;
        }
        private readonly List<HiddenAtmosphere> hiddenAtmosphere = new List<HiddenAtmosphere>();
        private Component worldCameraInput;
        private float nextCameraSearch;
        private readonly List<Renderer> waterRenderers = new List<Renderer>();
        private readonly Dictionary<Renderer, LakeWaterMesh> lakeMeshes = new Dictionary<Renderer, LakeWaterMesh>();
        private readonly Dictionary<object, int> waterFeatureAtCoord = new Dictionary<object, int>();
        private readonly Dictionary<int, float> waterPollution = new Dictionary<int, float>();
        private object waterGrid;
        private Array waterFeatures;
        private Button button;
        private Image icon;
        private Text label;
        private GameObject iconRoot;
        private PreserveColorFeature preserveFeature;
        private Type playerType;
        private object player;
        private MethodInfo playerPos2Coord;
        private PropertyInfo worldGridIndexer;
        private float nextPreserveRefresh;
        private float lastSaturation = -1f;
        private bool preserveFeatureInstalled;
        private bool waterScanDone;
        private bool cloudScanDone;
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
            Ensure();
            bool inGame = instance.IsInGameWorld();
            // Settings are also applied while the main menu and loading scenes are
            // active. Persist those values through the game's settings system, but
            // never enable the runtime filter before the playable map is ready.
            if (!inGame)
            {
                filterEnabled = false;
                instance.RefreshIcon();
                return;
            }
            // A saved On value must not make a fresh game start with the overlay.
            // Once the first playable frame has passed, menu changes can apply it.
            if (!startupGateComplete)
            {
                startupGateComplete = true;
                filterEnabled = false;
                instance.RefreshIcon();
                return;
            }
            filterEnabled = value;
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
                if (waterScanDone)
                {
                    filterEnabled = false;
                    RefreshMaterials(true);
                    RestorePreservedLayers();
                }
                return;
            }

            if (!startupGateComplete)
            {
                startupGateComplete = true;
                filterEnabled = false;
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
            EnsurePreserveCameras();
            foreach (string typeName in new[]
            {
                "Biomes.Structures.StructureIngame",
                "Biomes.Furnitures.FurnitureIngame",
                "Biomes.Domes.DomeIngame",
                "Biomes.Domes.DomeSpaceIngame",
                "Biomes.TechSanctuaries.TechSanctuaryInGame",
                "MineralHandler"
            }) AddRenderersUnder(Type.GetType(typeName + ", Assembly-CSharp"));

            RefreshWaterContext();
            waterPollution.Clear();
            if (forceWaterScan || !waterScanDone)
            {
                waterRenderers.Clear();
                int lakeCount = 0, sourceCount = 0;
                foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (renderer == null) continue;
                    bool lake = false, water = false;
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material == null) continue;
                        // These are the actual generated terrain water materials,
                        // including source rings on the Default layer. Building
                        // water/glass materials deliberately do not match.
                        if (WaterMaterialNames.IsLake(material.name)) lake = true;
                        if (WaterMaterialNames.IsWater(material.name)) water = true;
                        if (WaterMaterialNames.IsSource(material.name)) sourceCount++;
                    }
                    if (!water) continue;
                    if (lake)
                    {
                        if (!lakeMeshes.ContainsKey(renderer))
                        {
                            try { lakeMeshes.Add(renderer, new LakeWaterMesh(renderer, ResolveWaterFeature, CreateGrayscaleWaterMaterial)); }
                            catch (Exception e) { Debug.LogError("[BioEden.NoDOF] Lake mesh: " + e.Message); }
                        }
                        lakeCount++;
                    }
                    else waterRenderers.Add(renderer);
                }
                Debug.Log("[BioEden.NoDOF] Water discovery: lakes=" + lakeCount + ", river/source renderers=" + waterRenderers.Count + ", source rings=" + sourceCount);
                waterScanDone = true;
            }
            foreach (var pair in lakeMeshes)
            {
                if (pair.Key == null) continue;
                pair.Value.Refresh(f => TryGetFeaturePollution(f, out float amount) && amount < WaterDisplayZeroThreshold);
                // Both lake material slots must reach the same color pass. Clean
                // triangles already use the grayscale palette; polluted ones use
                // the unmodified palette. Never redraw the original mixed mesh.
                AddPreservedRenderer(pair.Key);
            }
            foreach (var renderer in waterRenderers)
                if (renderer != null) SetWaterRendererPreservation(renderer, WaterIsPolluted(renderer));
            RefreshCloudColors(forceWaterScan);
        }

        private void RefreshCloudColors(bool forceScan)
        {
            if (cloudScanDone) return;
            foreach (var effect in UnityEngine.Object.FindObjectsByType<VisualEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (effect == null || effect.visualEffectAsset == null) continue;
                string graph = effect.visualEffectAsset.name;
                // Exact shipped ambient graph families; do not hide building
                // exhaust, construction feedback, or fog-of-war rendering.
                if (!(graph.StartsWith("VFX_CloudsFog", StringComparison.Ordinal) ||
                      graph.StartsWith("VFX_GroundFog", StringComparison.Ordinal) ||
                      graph.StartsWith("VFX_Tundra_SmokeAsh", StringComparison.Ordinal))) continue;
                var renderer = effect.GetComponent<Renderer>();
                hiddenAtmosphere.Add(new HiddenAtmosphere {
                    Effect = effect, Renderer = renderer,
                    WasPaused = effect.pause,
                    WasHidden = renderer != null && renderer.forceRenderingOff
                });
                effect.pause = true;
                if (renderer != null) renderer.forceRenderingOff = true;
            }
            cloudScanDone = true;
            Debug.Log("[BioEden.NoDOF] Hidden ambient cloud/fog effects: " + hiddenAtmosphere.Count);
        }

        private void AddRenderersUnder(Type componentType)
        {
            if (componentType == null) return;
            foreach (var obj in UnityEngine.Object.FindObjectsByType(componentType, FindObjectsSortMode.None))
                if (obj is Component component)
                    foreach (var renderer in component.GetComponentsInChildren<Renderer>(true)) AddPreservedRenderer(renderer);
        }

        private void EnsurePreserveCameras()
        {
            int bit = 1 << PreserveLayer;
            foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (!cameraMasks.ContainsKey(camera)) cameraMasks.Add(camera, camera.cullingMask);
                camera.cullingMask |= bit;
            }
        }

        private void AddPreservedRenderer(Renderer renderer)
        {
            if (renderer == null || renderer.gameObject.layer == LayerMask.NameToLayer("UI")) return;
            var go = renderer.gameObject;
            if (!preservedLayers.ContainsKey(go)) preservedLayers.Add(go, go.layer);
            go.layer = PreserveLayer;
        }

        private void SetWaterRendererPreservation(Renderer renderer, bool preserve)
        {
            if (renderer == null) return;
            if (preserve)
            {
                RestoreCleanWaterMaterial(renderer);
                // Source prefabs mix stone and water submeshes. Their water
                // material already retains color under AdjustImage; do not
                // redraw the whole prefab and accidentally color its stones.
                if (!IsWaterSource(renderer)) AddPreservedRenderer(renderer);
                return;
            }

            // A renderer can have been classified while pollution was non-zero
            // and become clean later. Remove that stale override on every water
            // refresh so a clean lake/river cannot stay in the color pass.
            var go = renderer.gameObject;
            if (preservedLayers.TryGetValue(go, out int originalLayer))
            {
                go.layer = originalLayer;
                preservedLayers.Remove(go);
            }
            ApplyCleanWaterMaterial(renderer);
        }

        private void RestorePreservedLayers()
        {
            foreach (var lake in lakeMeshes.Values) lake.Dispose();
            lakeMeshes.Clear();
            waterRenderers.Clear();
            waterFeatureAtCoord.Clear();
            waterPollution.Clear();
            waterGrid = null;
            waterFeatures = null;
            player = null;
            foreach (var item in hiddenAtmosphere)
            {
                if (item.Effect != null) item.Effect.pause = item.WasPaused;
                if (item.Renderer != null) item.Renderer.forceRenderingOff = item.WasHidden;
            }
            hiddenAtmosphere.Clear();
            cloudScanDone = false;
            foreach (var pair in cleanWaterMaterials)
            {
                if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
            }
            cleanWaterMaterials.Clear();
            foreach (var clone in waterMaterialClones) if (clone != null) Destroy(clone);
            waterMaterialClones.Clear();
            foreach (var pair in preservedLayers)
                if (pair.Key != null) pair.Key.layer = pair.Value;
            preservedLayers.Clear();
            foreach (var pair in cameraMasks)
                if (pair.Key != null) pair.Key.cullingMask = pair.Value;
            cameraMasks.Clear();
            waterScanDone = false;
        }

        private void ApplyCleanWaterMaterial(Renderer renderer)
        {
            if (cleanWaterMaterials.ContainsKey(renderer)) return;
            var originals = renderer.sharedMaterials;
            var assigned = (Material[])originals.Clone();
            for (int i = 0; i < assigned.Length; i++)
            {
                if (assigned[i] == null || !WaterMaterialNames.IsWater(assigned[i].name)) continue;
                assigned[i] = CreateGrayscaleWaterMaterial(assigned[i]);
                waterMaterialClones.Add(assigned[i]);
            }
            cleanWaterMaterials.Add(renderer, originals);
            renderer.sharedMaterials = assigned;
        }

        private static Material CreateGrayscaleWaterMaterial(Material original)
        {
            var clone = new Material(original) { name = original.name + " (BioEden grayscale water)" };
            var shader = clone.shader;
            if (shader != null)
            {
                for (int i = 0; i < shader.GetPropertyCount(); i++)
                {
                    if (shader.GetPropertyType(i) != ShaderPropertyType.Color) continue;
                    string property = shader.GetPropertyName(i);
                    Color color = clone.GetColor(property);
                    float gray = color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
                    clone.SetColor(property, new Color(gray, gray, gray, color.a));
                }
                if (clone.HasProperty("_Saturation")) clone.SetFloat("_Saturation", 0f);
            }
            return clone;
        }

        private void RestoreCleanWaterMaterial(Renderer renderer)
        {
            if (renderer == null || !cleanWaterMaterials.TryGetValue(renderer, out Material[] originals)) return;
            var assigned = renderer.sharedMaterials;
            renderer.sharedMaterials = originals;
            cleanWaterMaterials.Remove(renderer);
            foreach (var clone in assigned)
                if (clone != null && waterMaterialClones.Remove(clone)) Destroy(clone);
        }

        private static bool IsWaterSource(Renderer renderer)
        {
            foreach (var material in renderer.sharedMaterials)
                if (material != null && WaterMaterialNames.IsSource(material.name)) return true;
            return false;
        }

        private bool WaterIsPolluted(Renderer renderer)
        {
            try
            {
                // Spring prefabs and waterfall rings are children of the river;
                // its small bounds can land on the surrounding stone/land hex.
                // Use its owning river surface to decide the source state.
                if (IsWaterSource(renderer))
                {
                    for (var parent = renderer.transform.parent; parent != null; parent = parent.parent)
                    {
                        var owner = parent.GetComponent<Renderer>();
                        if (owner != null && owner.sharedMaterial != null && owner.sharedMaterial.name.StartsWith("Mat_Terrain_Water_River", StringComparison.Ordinal))
                            return WaterIsPolluted(owner);
                    }
                }
                Bounds bounds = renderer.bounds;
                Vector3 x = Vector3.right * bounds.extents.x * 0.65f;
                Vector3 z = Vector3.forward * bounds.extents.z * 0.65f;
                foreach (Vector3 sample in new[] { bounds.center, bounds.center + x, bounds.center - x, bounds.center + z, bounds.center - z })
                {
                    // Renderer bounds can extend beyond the playable grid. Those
                    // samples are unknown and must not make clean water appear
                    // polluted. Use every valid slot we can resolve instead.
                    if (!TryGetWaterPollution(sample, out float pollution)) continue;
                    if (pollution >= WaterDisplayZeroThreshold) return true;
                }
                return false;
            }
            catch { return false; }
        }

        private void RefreshWaterContext()
        {
            var gameType = Type.GetType("Biomes.Game, Assembly-CSharp");
            object game = FindProperty(gameType, "Singleton")?.GetValue(null);
            player = FindProperty(gameType, "Plyr")?.GetValue(game);
            playerType = player?.GetType();
            playerPos2Coord = playerType?.GetMethod("Pos2Coord", BindingFlags.Instance | BindingFlags.Public);
            object grid = FindProperty(playerType, "WorldGrid")?.GetValue(player);
            if (!ReferenceEquals(grid, waterGrid)) waterFeatureAtCoord.Clear();
            waterGrid = grid;
            waterFeatures = FindProperty(grid?.GetType(), "Features")?.GetValue(grid) as Array;
        }

        private int ResolveWaterFeature(Vector3 position)
        {
            if (waterGrid == null || playerPos2Coord == null || waterFeatures == null) return -1;
            object coord = playerPos2Coord.Invoke(player, new object[] { position });
            if (waterFeatureAtCoord.TryGetValue(coord, out int cached)) return cached;
            if (worldGridIndexer == null) worldGridIndexer = FindIndexer(waterGrid.GetType(), coord.GetType());
            int result = -1;
            try
            {
                object slot = worldGridIndexer?.GetValue(waterGrid, new[] { coord });
                int index = Convert.ToInt32(FindField(slot?.GetType(), "featureIndex")?.GetValue(slot) ?? -1);
                if (index >= 0 && index < waterFeatures.Length)
                {
                    object feature = waterFeatures.GetValue(index);
                    if (Convert.ToBoolean(FindProperty(feature.GetType(), "IsWaterOrRiver")?.GetValue(feature) ?? false)) result = index;
                }
            }
            catch (TargetInvocationException) { /* A border triangle may lie outside the grid. */ }
            waterFeatureAtCoord[coord] = result;
            return result;
        }

        private bool TryGetWaterPollution(Vector3 position, out float value)
        {
            return TryGetFeaturePollution(ResolveWaterFeature(position), out value);
        }

        private bool TryGetFeaturePollution(int index, out float value)
        {
            value = 0f;
            if (index < 0 || waterFeatures == null || index >= waterFeatures.Length) return false;
            if (waterPollution.TryGetValue(index, out value)) return true;
            object feature = waterFeatures.GetValue(index);
            object shape = FindField(feature.GetType(), "shape")?.GetValue(feature);
            var coords = FindProperty(shape?.GetType(), "Coords")?.GetValue(shape) as System.Collections.IEnumerable;
            if (coords == null || worldGridIndexer == null) return false;
            float total = 0f;
            int count = 0;
            foreach (object coord in coords)
            {
                object slot = worldGridIndexer.GetValue(waterGrid, new[] { coord });
                var normalized = FindProperty(slot?.GetType(), "PollutionNormalized")?.GetValue(slot);
                if (normalized == null) return false;
                total += Convert.ToSingle(normalized);
                count++;
            }
            if (count == 0) return false;
            value = total / count;
            waterPollution[index] = value;
            return true;
        }

        private bool IsInGameWorld()
        {
            // Cache the live component. Searching all scene objects each frame
            // incurred overhead even with the filter disabled. Unity invalidates
            // the cached reference when its scene is unloaded.
            if (worldCameraInput == null)
            {
                if (Time.unscaledTime < nextCameraSearch) return false;
                nextCameraSearch = Time.unscaledTime + 0.5f;
                var cameraType = Type.GetType("Biomes.Cam.CameraInputIngame, Assembly-CSharp");
                if (cameraType == null) return false;
                worldCameraInput = UnityEngine.Object.FindFirstObjectByType(cameraType) as Component;
                if (worldCameraInput == null) return false;
            }
            var gameType = Type.GetType("Biomes.Game, Assembly-CSharp");
            if (gameType == null) return false;
            try
            {
                object game = FindProperty(gameType, "Singleton")?.GetValue(null);
                if (game == null) return false;
                if (game == null) return false;
                object currentPlayer = FindProperty(gameType, "Plyr")?.GetValue(game);
                object worldGrid = FindProperty(currentPlayer?.GetType(), "WorldGrid")?.GetValue(currentPlayer);
                object state = FindProperty(gameType, "StateCurrent")?.GetValue(game);
                string stateName = state?.ToString();
                bool playableState = stateName == "Play" || stateName == "PlayPost" || stateName == "Pause";
                // StateCurrent changes to Play only after the world has finished its
                // loading sequence. IsLoading is still true briefly during the Play
                // transition in this game version, so it cannot be used as the gate.
                return playableState && currentPlayer != null && worldGrid != null;
            }
            catch { return false; }
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

        private static PropertyInfo FindProperty(Type type, string name)
        {
            while (type != null)
            {
                var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
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
