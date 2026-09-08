using System;

namespace BioEden.NoDOF
{
    internal static class WaterMaterialNames
    {
        public static bool IsWater(string name) => name != null && name.StartsWith("Mat_Terrain_Water_", StringComparison.OrdinalIgnoreCase);
        public static bool IsLake(string name) => name != null && name.StartsWith("Mat_Terrain_Water_Lake", StringComparison.OrdinalIgnoreCase);
        public static bool IsSource(string name) => name != null &&
            (name.StartsWith("Mat_Terrain_Water_Source", StringComparison.OrdinalIgnoreCase) ||
             name.StartsWith("Mat_Terrain_Water_Fall_Circle", StringComparison.OrdinalIgnoreCase));
    }
}
