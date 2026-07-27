namespace Olympe.MaterialManager.Helpers;

/// <summary>
/// Manipulation de couleurs ARGB empaquetees en int (format System.Drawing.Color.ToArgb).
/// Point de verite unique pour les operations bit-a-bit auparavant dupliquees
/// dans le bridge, les ViewModels et les converters (MAINT-08).
/// </summary>
public static class ArgbUtils
{
    /// <summary>
    /// Empaquete les composantes R/G/B en int ARGB, alpha force a 255 (opaque).
    /// </summary>
    public static int PackArgb(byte r, byte g, byte b)
        => (0xFF << 24) | (r << 16) | (g << 8) | b;

    /// <summary>
    /// Extrait les composantes A/R/G/B d'un int ARGB.
    /// </summary>
    public static (byte A, byte R, byte G, byte B) UnpackArgb(int argb)
        => ((byte)((argb >> 24) & 0xFF),
            (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF),
            (byte)(argb & 0xFF));
}
