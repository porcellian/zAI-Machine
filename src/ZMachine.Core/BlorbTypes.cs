namespace ZMachine.Core;

/// <summary>
/// Constants for Blorb resource usage types.
/// Blorb "Contents of the Resource Index Chunk" — four usage values.
/// </summary>
public static class BlorbUsage
{
    /// <summary>Picture resource ('Pict').</summary>
    public const string Picture = "Pict";

    /// <summary>Sound resource ('Snd ').</summary>
    public const string Sound = "Snd ";

    /// <summary>Data file resource ('Data').</summary>
    public const string Data = "Data";

    /// <summary>Executable code resource ('Exec').</summary>
    public const string Executable = "Exec";
}

/// <summary>
/// Parsed color palette from a Blorb 'Plte' chunk.
/// Blorb "The Color Palette Chunk" — either an explicit RGB list
/// or a direct-color depth hint (16 or 32 bits per pixel).
/// </summary>
public class BlorbPalette
{
    /// <summary>
    /// True if the palette is a direct-color depth hint rather than
    /// an explicit color list.
    /// </summary>
    public bool IsDirectColor { get; }

    /// <summary>
    /// The recommended bits per pixel (16 or 32). Only meaningful
    /// when <see cref="IsDirectColor"/> is true.
    /// </summary>
    public int DirectColorDepth { get; }

    /// <summary>
    /// Explicit RGB color entries (1–256 entries of 3 bytes each).
    /// Null when <see cref="IsDirectColor"/> is true.
    /// </summary>
    public IReadOnlyList<(byte R, byte G, byte B)>? Colors { get; }

    /// <summary>Creates a direct-color palette hint.</summary>
    public BlorbPalette(int depth)
    {
        IsDirectColor = true;
        DirectColorDepth = depth;
    }

    /// <summary>Creates an explicit RGB color palette.</summary>
    public BlorbPalette(IReadOnlyList<(byte R, byte G, byte B)> colors)
    {
        IsDirectColor = false;
        Colors = colors;
    }
}
