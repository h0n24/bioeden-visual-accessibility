using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BioEden.NoDOF
{
    // Project the game's exact perimeter onto an overlay canvas. Fog of war is
    // composited by another camera, so a world-space ribbon cannot reliably win
    // against it even with ZTest Always or a late material render queue.
    public sealed class RangeOutline : MonoBehaviour
    {
        private static readonly List<RangeOutline> instances = new List<RangeOutline>();
        private readonly List<Vector3> vertices = new List<Vector3>();
        private MeshFilter source;
        private Renderer sourceRenderer;
        private GameObject overlay;
        private Canvas canvas;
        private PerimeterGraphic graphic;
        private Camera worldCamera;
        private Matrix4x4 lastView, lastProjection, lastTransform;
        private Rect lastViewport;
        private bool dirty = true, failed;

        public void SetSource(MeshFilter filter)
        {
            source = filter;
            sourceRenderer = filter.GetComponent<Renderer>();
            if (!instances.Contains(this)) instances.Add(this);
            vertices.Clear();
            if (filter.sharedMesh != null) filter.sharedMesh.GetVertices(vertices);
            // The generator writes lower endpoint pairs, then their upper copies.
            if (vertices.Count % 4 != 0) { Fail(new InvalidOperationException("Unexpected perimeter mesh layout.")); return; }
            dirty = true;
        }

        public static void RefreshAll()
        {
            for (int i = instances.Count - 1; i >= 0; i--)
            {
                if (instances[i] == null) { instances.RemoveAt(i); continue; }
                instances[i].dirty = true;
                if (!Runtime.OutlineEnabled) instances[i].Hide();
            }
        }

        private void LateUpdate()
        {
            bool show = Runtime.OutlineEnabled && !failed && source != null && source.sharedMesh != null
                && vertices.Count > 0 && (sourceRenderer == null || sourceRenderer.enabled);
            if (!show) { Hide(); return; }
            try
            {
                if (worldCamera == null || !worldCamera.isActiveAndEnabled) worldCamera = Camera.main;
                if (worldCamera == null) { Hide(); return; }
                if (overlay == null) CreateOverlay();
                if (!overlay.activeSelf) { overlay.SetActive(true); dirty = true; }
                canvas.targetDisplay = worldCamera.targetDisplay;
                var view = worldCamera.worldToCameraMatrix;
                var projection = worldCamera.projectionMatrix;
                var transformMatrix = source.transform.localToWorldMatrix;
                var viewport = worldCamera.pixelRect;
                if (!dirty && view == lastView && projection == lastProjection && transformMatrix == lastTransform && viewport == lastViewport) return;
                lastView = view; lastProjection = projection; lastTransform = transformMatrix; lastViewport = viewport;
                dirty = false;
                graphic.Points.Clear();
                for (int i = 0; i < vertices.Count / 2; i += 2)
                {
                    Vector3 a = source.transform.TransformPoint(vertices[i]);
                    Vector3 b = source.transform.TransformPoint(vertices[i + 1]);
                    float za = Vector3.Dot(a - worldCamera.transform.position, worldCamera.transform.forward);
                    float zb = Vector3.Dot(b - worldCamera.transform.position, worldCamera.transform.forward);
                    float near = Math.Max(0.01f, worldCamera.nearClipPlane);
                    if (za < near && zb < near) continue;
                    // Clip in world space before projecting a segment across the eye.
                    if (za < near) a = Vector3.Lerp(a, b, (near - za) / (zb - za));
                    else if (zb < near) b = Vector3.Lerp(b, a, (near - zb) / (za - zb));
                    Vector3 sa = worldCamera.WorldToScreenPoint(a), sb = worldCamera.WorldToScreenPoint(b);
                    float ax = sa.x, ay = sa.y, bx = sb.x, by = sb.y;
                    if (!FeatureMath.ClipLine(ref ax, ref ay, ref bx, ref by, viewport.xMin, viewport.yMin, viewport.xMax, viewport.yMax)) continue;
                    graphic.Points.Add(new Vector2(ax, ay));
                    graphic.Points.Add(new Vector2(bx, by));
                }
                graphic.SetVerticesDirty();
            }
            catch (Exception e) { Fail(e); }
        }

        private void CreateOverlay()
        {
            // ScreenSpaceOverlay must be a root object. A low sorting order places
            // it before the game's normal overlay HUD and settings canvases.
            overlay = new GameObject("BioEden antenna perimeter overlay", typeof(RectTransform), typeof(Canvas));
            canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -32000;
            var line = new GameObject("White outline and blue center", typeof(RectTransform), typeof(CanvasRenderer));
            line.transform.SetParent(overlay.transform, false);
            var rect = line.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.zero; rect.offsetMin = rect.offsetMax = Vector2.zero;
            graphic = line.AddComponent<PerimeterGraphic>();
            graphic.raycastTarget = false; // No GraphicRaycaster: never intercept input.
            Debug.Log("[BioEden.NoDOF] Screen-space antenna outline initialized.");
        }

        private void Hide() { if (overlay != null) overlay.SetActive(false); }
        private void OnDisable() { Hide(); }
        private void OnEnable() { dirty = true; }
        private void Fail(Exception e)
        {
            failed = true; Hide();
            Debug.LogError("[BioEden.NoDOF] Antenna outline failed; original border retained: " + e);
        }
        private void OnDestroy() { instances.Remove(this); if (overlay != null) Destroy(overlay); }
    }

    public sealed class PerimeterGraphic : Graphic
    {
        public readonly List<Vector2> Points = new List<Vector2>();
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            // Unity UI uses 16-bit mesh indexes. Each segment uses eight vertices.
            int count = Math.Min(Points.Count, 16000);
            for (int pass = 0; pass < 2; pass++)
            {
                float halfWidth = pass == 0 ? 2.75f : 1.0f;
                Color32 tint = pass == 0 ? new Color32(255, 255, 255, 255) : new Color32(5, 87, 209, 255);
                for (int i = 0; i < count; i += 2)
                {
                    Vector2 a = Points[i], b = Points[i + 1], d = b - a;
                    if (d.sqrMagnitude < 0.001f) continue;
                    d.Normalize();
                    Vector2 side = new Vector2(-d.y, d.x) * halfWidth;
                    a -= d * halfWidth * 0.5f; b += d * halfWidth * 0.5f;
                    int n = vh.currentVertCount;
                    vh.AddVert(a - side, tint, Vector2.zero); vh.AddVert(a + side, tint, Vector2.zero);
                    vh.AddVert(b + side, tint, Vector2.zero); vh.AddVert(b - side, tint, Vector2.zero);
                    vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
                }
            }
        }
    }
}
