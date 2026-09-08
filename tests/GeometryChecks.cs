using BioEden.NoDOF;

internal static class GeometryChecks
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static bool Close(float a, float b) => Math.Abs(a - b) < 0.001f;
    public static void Run()
    {
        Check(FeatureMath.ZoomLimit(-100f, false) == -100f, "Off must preserve negative zoom distance.");
        Check(Close(FeatureMath.ZoomLimit(-100f, true), -135f), "Extended zoom distance.");
        float previousMin = 0, previousMax = 0;
        for (int i = 0; i <= 1000; i++)
        {
            float zoom = i / 1000f;
            float min = FeatureMath.PitchLimit(35f, zoom, 70f, true);
            float max = FeatureMath.PitchLimit(55f, zoom, 82f, true);
            Check(min <= max && max < 90f && min >= previousMin && max >= previousMax, "Pitch must stay ordered and increase smoothly.");
            Check(FeatureMath.PitchLimit(35f, zoom, 70f, false) == 35f, "Off restores pitch exactly throughout range.");
            if (zoom <= 0.5f) Check(min == 35f && max == 55f, "Close view must retain original pitch.");
            previousMin = min; previousMax = max;
        }
        Check(Close(previousMin, 70f) && Close(previousMax, 82f), "Far pitch limits.");
        Check(FeatureMath.PitchLimit(85f, 1f, 82f, true) == 85f, "Do not lower an already steeper angle.");
        float ax = -100, ay = 50, bx = 200, by = 50;
        Check(FeatureMath.ClipLine(ref ax, ref ay, ref bx, ref by, 0, 0, 100, 100) && Close(ax, 0) && Close(bx, 100), "Crossing segment must clip to viewport.");
        ax = -20; ay = -20; bx = -10; by = -10;
        Check(!FeatureMath.ClipLine(ref ax, ref ay, ref bx, ref by, 0, 0, 100, 100), "Offscreen segment should be rejected.");
        ax = float.NaN; ay = 0; bx = 10; by = 10;
        Check(!FeatureMath.ClipLine(ref ax, ref ay, ref bx, ref by, 0, 0, 100, 100), "Invalid projection should be rejected.");
        var random = new Random(721);
        for (int i = 0; i < 10000; i++)
        {
            float x1 = random.Next(-2000, 2000), y1 = random.Next(-2000, 2000);
            float x2 = random.Next(-2000, 2000), y2 = random.Next(-2000, 2000);
            ax = x1; ay = y1; bx = x2; by = y2;
            bool forward = FeatureMath.ClipLine(ref ax, ref ay, ref bx, ref by, 100, 50, 900, 650);
            float rx = x2, ry = y2, sx = x1, sy = y1;
            bool reverse = FeatureMath.ClipLine(ref rx, ref ry, ref sx, ref sy, 100, 50, 900, 650);
            Check(forward == reverse, "Clipping must be direction independent.");
            if (!forward) continue;
            Check(Close(ax, sx) && Close(ay, sy) && Close(bx, rx) && Close(by, ry), "Reversed endpoints must match.");
            Check(ax >= 99.999f && ax <= 900.001f && bx >= 99.999f && bx <= 900.001f && ay >= 49.999f && ay <= 650.001f && by >= 49.999f && by <= 650.001f, "Clipped geometry must stay inside viewport.");
        }
        Console.WriteLine("PASS: zoom/pitch restoration, smooth pitch bounds, viewport clipping and 10,000 reversed-segment checks.");
    }
}
