using System;
using System.Collections.Generic;

namespace BioEden.NoDOF
{
    // Keep original indices and winding; only route entire triangles to a material.
    internal static class WaterTriangles
    {
        public static void Partition(int[] indices, int[] features, Func<int, bool> isClean,
            List<int> clean, List<int> colored)
        {
            if (indices.Length != features.Length * 3) throw new ArgumentException("Triangle feature count mismatch.");
            clean.Clear();
            colored.Clear();
            for (int t = 0; t < features.Length; t++)
            {
                var target = features[t] >= 0 && isClean(features[t]) ? clean : colored;
                target.Add(indices[t * 3]);
                target.Add(indices[t * 3 + 1]);
                target.Add(indices[t * 3 + 2]);
            }
        }
    }
}
