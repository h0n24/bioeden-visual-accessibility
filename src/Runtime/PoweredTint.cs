using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BioEden.NoDOF
{
    // Temporary per-renderer palettes. Shared game assets and simulation state
    // are never edited; clones are reused while a structure remains powered.
    internal sealed class PoweredTint
    {
        private sealed class Palette { internal Material[] Original, Tinted; }
        private readonly Dictionary<Renderer, Palette> palettes = new Dictionary<Renderer, Palette>();
        private readonly HashSet<Renderer> wanted = new HashSet<Renderer>();
        private readonly List<Renderer> stale = new List<Renderer>();
        internal void Begin() => wanted.Clear();
        internal void Add(Renderer renderer)
        {
            if (renderer == null || !(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) return;
            wanted.Add(renderer);
            if (palettes.ContainsKey(renderer)) return;
            var original = renderer.sharedMaterials;
            var tinted = new Material[original.Length];
            for (int i = 0; i < original.Length; i++)
            {
                var source = original[i];
                if (source == null) continue;
                var clone = new Material(source) { name = source.name + " (powered filter)" };
                for (int p = 0; p < source.shader.GetPropertyCount(); p++)
                {
                    if (source.shader.GetPropertyType(p) != ShaderPropertyType.Color) continue;
                    string name = source.shader.GetPropertyName(p);
                    // Emission and non-palette shader controls retain their values.
                    if (!name.StartsWith("_Color") && name != "_BaseColor") continue;
                    Color c = source.GetColor(name);
                    float gray = c.grayscale;
                    var saturated = new Color(Mathf.Clamp01(gray + (c.r-gray)*1.3f), Mathf.Clamp01(gray + (c.g-gray)*1.3f), Mathf.Clamp01(gray + (c.b-gray)*1.3f), c.a);
                    float value = Mathf.Max(c.r, Mathf.Max(c.g,c.b));
                    var gold = new Color(value, value*.86f, value*.12f, c.a);
                    clone.SetColor(name, Color.Lerp(saturated, gold, .45f));
                }
                tinted[i] = clone;
            }
            palettes.Add(renderer, new Palette { Original = original, Tinted = tinted });
            renderer.sharedMaterials = tinted;
        }
        internal void End()
        {
            stale.Clear();
            foreach (var pair in palettes) if (pair.Key == null || !wanted.Contains(pair.Key)) stale.Add(pair.Key);
            foreach (var renderer in stale) Restore(renderer);
        }
        internal void Clear() { wanted.Clear(); End(); }
        private void Restore(Renderer renderer)
        {
            var palette = palettes[renderer];
            if (renderer != null)
            {
                var current = renderer.sharedMaterials;
                for (int i=0; i<current.Length && i<palette.Tinted.Length; i++)
                    if (current[i] == palette.Tinted[i]) current[i] = palette.Original[i];
                renderer.sharedMaterials = current;
            }
            foreach (var material in palette.Tinted) if (material != null) Object.Destroy(material);
            palettes.Remove(renderer);
        }
    }
}

