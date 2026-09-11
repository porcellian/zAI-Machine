namespace ZMachine.Core;

/// <summary>
/// Parsed resolution data from a Blorb 'Reso' chunk. Contains the
/// standard/min/max window sizes and per-image scaling entries.
/// </summary>
/// <remarks>
/// Blorb "The Resolution Chunk" — defines how images scale relative
/// to the game window size via the Elbow Room Factor (ERF).
/// </remarks>
public class ResolutionInfo
{
    /// <summary>Standard (designed-for) window width in screen pixels.</summary>
    public int StandardWidth { get; init; }

    /// <summary>Standard (designed-for) window height in screen pixels.</summary>
    public int StandardHeight { get; init; }

    /// <summary>Minimum window width hint (0 = no limit).</summary>
    public int MinWidth { get; init; }

    /// <summary>Minimum window height hint (0 = no limit).</summary>
    public int MinHeight { get; init; }

    /// <summary>Maximum window width hint (0 = no limit).</summary>
    public int MaxWidth { get; init; }

    /// <summary>Maximum window height hint (0 = no limit).</summary>
    public int MaxHeight { get; init; }

    /// <summary>Per-image scaling entries, keyed by picture resource number.</summary>
    public IReadOnlyDictionary<int, ImageScalingEntry> Entries { get; init; }
        = new Dictionary<int, ImageScalingEntry>();
}

/// <summary>
/// Scaling ratios for a single image from the Blorb 'Reso' chunk.
/// Each ratio is a fraction (numerator/denominator). Zero/zero means
/// "no limit" for min (zero) or max (infinity).
/// </summary>
/// <remarks>
/// Blorb "The Resolution Chunk" — stdratio, minratio, maxratio as
/// numerator/denominator pairs.
/// </remarks>
public readonly struct ImageScalingEntry
{
    /// <summary>Standard ratio numerator.</summary>
    public int StandardNum { get; init; }

    /// <summary>Standard ratio denominator.</summary>
    public int StandardDen { get; init; }

    /// <summary>Minimum ratio numerator (0/0 = no minimum).</summary>
    public int MinNum { get; init; }

    /// <summary>Minimum ratio denominator.</summary>
    public int MinDen { get; init; }

    /// <summary>Maximum ratio numerator (0/0 = no maximum/infinity).</summary>
    public int MaxNum { get; init; }

    /// <summary>Maximum ratio denominator.</summary>
    public int MaxDen { get; init; }

    /// <summary>Standard ratio as a double. Returns 1.0 if denominator is 0.</summary>
    public double StandardRatio => StandardDen != 0 ? (double)StandardNum / StandardDen : 1.0;

    /// <summary>
    /// Minimum ratio as a nullable double. Null means no minimum (zero).
    /// </summary>
    public double? MinRatio => (MinNum == 0 && MinDen == 0) ? null : (double)MinNum / MinDen;

    /// <summary>
    /// Maximum ratio as a nullable double. Null means no maximum (infinity).
    /// </summary>
    public double? MaxRatio => (MaxNum == 0 && MaxDen == 0) ? null : (double)MaxNum / MaxDen;

    /// <summary>
    /// True if min and max ratios are the same non-null value, meaning
    /// this image has a fixed scaling ratio (ERF is ignored).
    /// </summary>
    public bool IsFixed
    {
        get
        {
            var min = MinRatio;
            var max = MaxRatio;
            return min.HasValue && max.HasValue && Math.Abs(min.Value - max.Value) < 1e-10;
        }
    }
}

/// <summary>
/// Computes scaled image dimensions using the Blorb resolution system.
/// </summary>
/// <remarks>
/// Blorb "The Resolution Chunk" — ERF = min(wx/px, wy/py);
/// R = clamp(ERF * stdratio, minratio, maxratio).
/// </remarks>
public static class ImageScaler
{
    /// <summary>
    /// Computes the Elbow Room Factor for the given window and standard sizes.
    /// ERF = min(wx/px, wy/py).
    /// </summary>
    public static double ComputeERF(int windowWidth, int windowHeight,
                                     int standardWidth, int standardHeight)
    {
        if (standardWidth <= 0 || standardHeight <= 0)
            return 1.0;

        double erfX = (double)windowWidth / standardWidth;
        double erfY = (double)windowHeight / standardHeight;
        return Math.Min(erfX, erfY);
    }

    /// <summary>
    /// Computes the scaling ratio R for an image given the ERF and its entry.
    /// R = clamp(ERF * stdratio, minratio, maxratio).
    /// If minratio == maxratio, R is that fixed value (ERF ignored).
    /// </summary>
    public static double ComputeRatio(double erf, ImageScalingEntry entry)
    {
        if (entry.IsFixed)
            return entry.MinRatio!.Value;

        double r = erf * entry.StandardRatio;

        if (entry.MinRatio.HasValue && r < entry.MinRatio.Value)
            r = entry.MinRatio.Value;

        if (entry.MaxRatio.HasValue && r > entry.MaxRatio.Value)
            r = entry.MaxRatio.Value;

        return r;
    }

    /// <summary>
    /// Computes the scaled dimensions of an image given its raw size,
    /// the window size, and the resolution info. Returns raw size if
    /// no Reso entry exists for this image (non-scalable, 1:1).
    /// </summary>
    public static (int Width, int Height) ComputeScaledSize(
        int rawWidth, int rawHeight,
        int windowWidth, int windowHeight,
        ResolutionInfo? reso, int pictureNumber)
    {
        if (reso == null || !reso.Entries.ContainsKey(pictureNumber))
            return (rawWidth, rawHeight);

        var entry = reso.Entries[pictureNumber];
        double erf = ComputeERF(windowWidth, windowHeight,
                                reso.StandardWidth, reso.StandardHeight);
        double r = ComputeRatio(erf, entry);

        int scaledW = Math.Max(1, (int)Math.Round(rawWidth * r));
        int scaledH = Math.Max(1, (int)Math.Round(rawHeight * r));
        return (scaledW, scaledH);
    }
}
