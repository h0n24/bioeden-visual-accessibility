using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace BioEden.NoDOF
{
    public static partial class Runtime
    {
        private static bool outlineEnabled = true;
        private static bool extendedZoom = true;
        private static bool userDofEnabled;
        private static object lastWorldZoom;
        private static FieldInfo zoomOwner;
        private static FieldInfo panOwner;
        private static MethodInfo resetZoom;
        private static FieldInfo structureFilter, explorationFilter;
        private static bool outlineErrorLogged, zoomErrorLogged;
        public static bool OutlineEnabled => outlineEnabled;

        internal static void SetOutline(bool value)
        {
            if (outlineEnabled != value)
            {
                outlineEnabled = value;
                RangeOutline.RefreshAll();
                Debug.Log("[BioEden.NoDOF] Antenna Range Outline: " + (value ? "On" : "Off"));
            }
        }

        internal static void SetExtendedZoom(bool value)
        {
            if (extendedZoom == value) return;
            extendedZoom = value;
            enabled = userDofEnabled && !extendedZoom;
            // Preserve current distance where possible and clamp back to the normal
            // limit when disabled. The game recalculates its normalized zoom value.
            try
            {
                if (lastWorldZoom is UnityEngine.Object live && live != null)
                    resetZoom?.Invoke(lastWorldZoom, null);
            }
            catch (Exception e) { LogZoomError(e); }
            Debug.Log("[BioEden.NoDOF] Extended Zoom: " + (value ? "On (+35%)" : "Off"));
        }

        internal static void SetFilter(bool value) => FilterController.SetEnabled(value);
        internal static void SetFilterHotkey(int value) => FilterController.SetHotkey(value);
        internal static void EnsureFilterController() => FilterController.Ensure();

        public static float ExtendZoom(float original, object module)
        {
            try
            {
                if (zoomOwner == null)
                {
                    zoomOwner = module.GetType().GetField("cameraInput", BindingFlags.Instance | BindingFlags.NonPublic);
                    resetZoom = module.GetType().GetMethod("CameraReset", BindingFlags.Instance | BindingFlags.NonPublic);
                }
                var owner = zoomOwner?.GetValue(module);
                // Do not extend the dome interior, topographic map or cinematics.
                if (owner?.GetType().FullName != "Biomes.Cam.CameraInputIngame") return original;
                lastWorldZoom = module;
                return FeatureMath.ZoomLimit(original, extendedZoom);
            }
            catch (Exception e) { LogZoomError(e); return original; }
        }

        public static float AdjustPitchMin(float original, object module, float zoom01) => AdjustPitch(original, module, zoom01, 70f);
        public static float AdjustPitchMax(float original, object module, float zoom01) => AdjustPitch(original, module, zoom01, 82f);

        private static float AdjustPitch(float original, object module, float zoom01, float farAngle)
        {
            if (!extendedZoom) return original;
            try
            {
                if (panOwner == null) panOwner = module.GetType().GetField("cameraInput", BindingFlags.Instance | BindingFlags.NonPublic);
                if (panOwner?.GetValue(module)?.GetType().FullName != "Biomes.Cam.CameraInputIngame") return original;
                return FeatureMath.PitchLimit(original, zoom01, farAngle, true);
            }
            catch (Exception e) { LogZoomError(e); return original; }
        }

        private static void LogZoomError(Exception e)
        {
            if (zoomErrorLogged) return;
            zoomErrorLogged = true;
            Debug.LogError("[BioEden.NoDOF] Extended Zoom failed; keeping original limit: " + e);
        }

        public static void RefreshBorder(object manager, MeshFilter filter)
        {
            try
            {
                if (filter == null) return;
                if (structureFilter == null)
                {
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    structureFilter = manager.GetType().GetField("gridStructureMeshFilter", flags);
                    explorationFilter = manager.GetType().GetField("gridExplorationMeshFilter", flags);
                    if (structureFilter == null || explorationFilter == null) throw new MissingFieldException("Antenna border mesh fields not found.");
                }
                if (filter != (MeshFilter)structureFilter.GetValue(manager) && filter != (MeshFilter)explorationFilter.GetValue(manager)) return;
                var outline = filter.GetComponent<RangeOutline>();
                if (outline == null) outline = filter.gameObject.AddComponent<RangeOutline>();
                outline.SetSource(filter);
            }
            catch (Exception e)
            {
                if (!outlineErrorLogged) { outlineErrorLogged = true; Debug.LogError("[BioEden.NoDOF] Antenna outline failed; original border retained: " + e); }
            }
        }
    }
}
