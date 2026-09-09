using System;
using System.Collections.Generic;
using UnityEngine;

namespace BioEden.NoDOF
{
    // The game batches lake hexes into one terrain mesh on layer 15. Split its
    // index buffer into material slots instead of classifying the entire renderer.
    internal sealed class LakeWaterMesh : IDisposable
    {
        private readonly MeshFilter filter;
        private readonly Renderer renderer;
        private readonly Mesh original;
        private readonly Material[] originals;
        private readonly Mesh split;
        private readonly List<Material> clones = new List<Material>();
        private readonly int[][] triangles;
        private readonly int[][] features;
        private readonly List<int> clean = new List<int>();
        private readonly List<int> colored = new List<int>();
        private readonly Dictionary<int, bool> previous = new Dictionary<int, bool>();
        private bool initialized;

        public LakeWaterMesh(Renderer renderer, Func<Vector3, int> resolveFeature, Func<Material, Material> grayscale)
        {
            this.renderer = renderer;
            filter = renderer.GetComponent<MeshFilter>();
            original = filter != null ? filter.sharedMesh : null;
            if (original == null || !original.isReadable) throw new InvalidOperationException("Lake mesh is not readable.");
            originals = renderer.sharedMaterials;
            if (original.subMeshCount != originals.Length) throw new InvalidOperationException("Lake material/submesh count mismatch.");
            var vertices = original.vertices;
            triangles = new int[original.subMeshCount][];
            features = new int[original.subMeshCount][];
            for (int s = 0; s < triangles.Length; s++)
            {
                triangles[s] = original.GetTriangles(s);
                features[s] = new int[triangles[s].Length / 3];
                for (int t = 0; t < features[s].Length; t++)
                {
                    int i = t * 3;
                    Vector3 center = (vertices[triangles[s][i]] + vertices[triangles[s][i + 1]] + vertices[triangles[s][i + 2]]) / 3f;
                    features[s][t] = resolveFeature(renderer.transform.TransformPoint(center));
                    if (!previous.ContainsKey(features[s][t])) previous[features[s][t]] = false;
                }
            }
            if (previous.Count == 0 || (previous.Count == 1 && previous.ContainsKey(-1)))
                throw new InvalidOperationException("No lake triangles could be mapped to a water feature.");
            split = UnityEngine.Object.Instantiate(original);
            try
            {
                split.name = original.name + " (BioEden lake material partition)";
                split.subMeshCount = originals.Length * 2;
                var assigned = new Material[originals.Length * 2];
                for (int s = 0; s < originals.Length; s++)
                {
                    assigned[s * 2] = originals[s];
                    var clone = grayscale(originals[s]);
                    clones.Add(clone);
                    assigned[s * 2 + 1] = clone;
                }
                for (int s = 0; s < triangles.Length; s++)
                {
                    split.SetTriangles(triangles[s], s * 2, false);
                    split.SetTriangles(Array.Empty<int>(), s * 2 + 1, false);
                }
                split.bounds = original.bounds;
                renderer.sharedMaterials = assigned;
                filter.sharedMesh = split;
            }
            catch { Dispose(); throw; }
            Debug.Log("[BioEden.NoDOF] Lake partition: " + renderer.name + ", features=" + previous.Count + ", hasUnmappedTriangles=" + previous.ContainsKey(-1));
        }

        public void Refresh(Func<int, bool> isClean)
        {
            bool changed = !initialized;
            var keys = new List<int>(previous.Keys);
            foreach (int f in keys)
            {
                bool now = isClean(f);
                if (now != previous[f]) { previous[f] = now; changed = true; }
            }
            if (!changed) return;
            int cleanCount = 0, coloredCount = 0;
            for (int s = 0; s < triangles.Length; s++)
            {
                WaterTriangles.Partition(triangles[s], features[s], f => previous[f], clean, colored);
                cleanCount += clean.Count / 3;
                coloredCount += colored.Count / 3;
                split.SetTriangles(colored, s * 2, false);
                split.SetTriangles(clean, s * 2 + 1, false);
            }
            split.bounds = original.bounds;
            initialized = true;
            Debug.Log("[BioEden.NoDOF] Lake water triangles: clean=" + cleanCount + ", colored=" + coloredCount);
        }

        public void UpdateCleanMaterials(Action<Material> update)
        {
            foreach (var clone in clones) update(clone);
        }

        public void Dispose()
        {
            if (filter != null && filter.sharedMesh == split) filter.sharedMesh = original;
            if (renderer != null) renderer.sharedMaterials = originals;
            UnityEngine.Object.Destroy(split);
            foreach (var clone in clones) UnityEngine.Object.Destroy(clone);
        }
    }
}
