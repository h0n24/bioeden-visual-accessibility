using BioEden.NoDOF;

static class WaterChecks
{
    public static void Run()
    {
        Require(WaterMaterialNames.IsSource("Mat_Terrain_Water_source_clean"), "Source pool material");
        Require(WaterMaterialNames.IsSource("Mat_Terrain_Water_Source_fall_clean"), "Second spring material");
        Require(WaterMaterialNames.IsSource("Mat_Terrain_Water_Fall_Circle"), "Waterfall ring material");
        Require(WaterMaterialNames.IsLake("Mat_Terrain_Water_Lake (Instance)"), "Generated lake material");
        foreach (string name in new[] { "Mat_Forest_water_clean", "mat_forest_water_source_clean", "mat_water_sytem_clean", "Mat_water_river_cleaner_clean", "Mat_sanctuary_water" })
            Require(!WaterMaterialNames.IsWater(name), "Do not change vegetation, stones, or buildings: " + name);
        // Two different lakes share one renderer. Unknown border triangles stay
        // colored; clean and polluted triangles must never overlap or disappear.
        int[] indices = { 2, 0, 1, 3, 5, 4, 8, 6, 7, 9, 11, 10 };
        int[] features = { 10, 20, 10, -1 };
        var clean = new List<int>();
        var colored = new List<int>();
        WaterTriangles.Partition(indices, features, f => f == 10, clean, colored);
        Require(clean.SequenceEqual(new[] { 2, 0, 1, 8, 6, 7 }), "Clean lake indices/winding");
        Require(colored.SequenceEqual(new[] { 3, 5, 4, 9, 11, 10 }), "Polluted lake and unknown edge indices");
        // A cleaner finishes while F1 is still enabled.
        WaterTriangles.Partition(indices, features, f => true, clean, colored);
        Require(clean.Count == 9 && colored.SequenceEqual(new[] { 9, 11, 10 }), "Live cleanup");
        WaterTriangles.Partition(indices, features, f => false, clean, colored);
        Require(clean.Count == 0 && colored.SequenceEqual(indices), "Return to original topology");
        Require(indices.SequenceEqual(new[] { 2, 0, 1, 3, 5, 4, 8, 6, 7, 9, 11, 10 }), "Original index buffer remains untouched");
        Console.WriteLine("PASS: shared lake mesh separates clean/polluted water, preserves winding, unknown edges, and live cleanup transitions.");
    }

    static void Require(bool ok, string name) { if (!ok) throw new Exception(name); }
}
