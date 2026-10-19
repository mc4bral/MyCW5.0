using System.Drawing;

namespace CW.Common.Helpers;

public static class ColorHelper
{
    /// <summary>
    /// Returns the perceived brightness of a color on a 0–255 scale,
    /// using the human eye's weighted sensitivity to R, G, and B.
    /// Values below 128 are generally considered "dark".
    /// </summary>
    public static int PerceivedBrightness(this Color color)
    {
        return (int)Math.Sqrt(
            color.R * color.R * .299 +
            color.G * color.G * .587 +
            color.B * color.B * .114);
    }
}
