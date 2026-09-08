using System;

namespace BioEden.NoDOF
{
    // Engine-independent geometry, also compiled into the regression checks.
    public static class FeatureMath
    {
        public const float ZoomMultiplier = 1.35f;
        public static float ZoomLimit(float original, bool extended) => extended ? original * ZoomMultiplier : original;

        public static float PitchLimit(float original, float zoom01, float farAngle, bool extended)
        {
            if (!extended) return original;
            float t = Math.Max(0f, Math.Min(1f, (zoom01 - 0.5f) / 0.5f));
            t = t * t * (3f - 2f * t);
            return original + (Math.Max(original, farAngle) - original) * t;
        }

        public static bool ClipLine(ref float ax, ref float ay, ref float bx, ref float by,
            float left, float bottom, float right, float top)
        {
            if (!Finite(ax) || !Finite(ay) || !Finite(bx) || !Finite(by)) return false;
            float dx = bx - ax, dy = by - ay, start = 0f, end = 1f;
            if (!Finite(dx) || !Finite(dy)) return false;
            if (!Clip(-dx, ax - left, ref start, ref end) || !Clip(dx, right - ax, ref start, ref end)
                || !Clip(-dy, ay - bottom, ref start, ref end) || !Clip(dy, top - ay, ref start, ref end)) return false;
            bx = ax + end * dx; by = ay + end * dy;
            ax += start * dx; ay += start * dy;
            return true;
        }

        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        private static bool Clip(float p, float q, ref float start, ref float end)
        {
            if (p == 0f) return q >= 0f;
            float r = q / p;
            if (p < 0f) { if (r > end) return false; start = Math.Max(start, r); }
            else { if (r < start) return false; end = Math.Min(end, r); }
            return true;
        }
    }
}
