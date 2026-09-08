namespace BioEden.NoDOF
{
    internal static class CloudColorMath
    {
        public static float Luma(float red, float green, float blue) =>
            red * 0.299f + green * 0.587f + blue * 0.114f;
    }
}
