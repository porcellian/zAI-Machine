namespace ZMachine.Core;

/// <summary>
/// Manages the adaptive palette ("Current Palette") for V6 Infocom
/// games that use the Blorb APal chunk. Non-adaptive pictures update
/// the palette when drawn; adaptive pictures render using the current
/// palette instead of their own PLTE chunk.
/// </summary>
/// <remarks>
/// Blorb "The Adaptive Palette Chunk" — 14-entry table covering
/// colour indices 2–15 (stored as a 16-entry array for index
/// convenience; entries 0 and 1 are not significant).
/// </remarks>
public class AdaptivePaletteManager
{
    private readonly HashSet<int> _adaptivePictures;

    /// <summary>
    /// The current palette: 16 sRGB entries. Only indices 2–15 are
    /// significant. Updated whenever a non-adaptive picture is drawn.
    /// </summary>
    private readonly (byte R, byte G, byte B)[] _currentPalette = new (byte, byte, byte)[16];

    private int _paletteVersion;

    /// <summary>
    /// Creates the manager from a parsed set of adaptive picture
    /// resource numbers (from the Blorb APal chunk).
    /// </summary>
    /// <param name="adaptivePictureNumbers">
    /// Resource numbers listed in the APal chunk. May be empty
    /// (Shogun, Journey — signals palette-changing behaviour possible).
    /// </param>
    public AdaptivePaletteManager(IEnumerable<int> adaptivePictureNumbers)
    {
        _adaptivePictures = new HashSet<int>(adaptivePictureNumbers);
    }

    /// <summary>
    /// Returns whether the given picture resource number is adaptive
    /// (listed in the APal chunk) and should use the current palette
    /// instead of its own PLTE.
    /// </summary>
    public bool IsAdaptive(int pictureNumber)
        => _adaptivePictures.Contains(pictureNumber);

    /// <summary>
    /// Number of adaptive picture entries in the APal chunk.
    /// Zero for Shogun/Journey (empty APal — signals palette behaviour).
    /// </summary>
    public int AdaptivePictureCount => _adaptivePictures.Count;

    /// <summary>
    /// Monotonically increasing version counter, incremented each time
    /// the palette changes. Consumers can compare against a cached
    /// version to detect staleness.
    /// </summary>
    public int PaletteVersion => _paletteVersion;

    /// <summary>
    /// Returns a copy of the current palette (16 entries).
    /// Only indices 2–15 are significant.
    /// </summary>
    public (byte R, byte G, byte B)[] GetCurrentPalette()
    {
        var copy = new (byte R, byte G, byte B)[16];
        Array.Copy(_currentPalette, copy, 16);
        return copy;
    }

    /// <summary>
    /// Returns the colour at the given palette index (0–15).
    /// </summary>
    public (byte R, byte G, byte B) GetColor(int index)
    {
        if (index < 0 || index >= 16)
            return (0, 0, 0);
        return _currentPalette[index];
    }

    /// <summary>
    /// Updates the current palette from a non-adaptive picture's PLTE
    /// data. The colours should already be transformed to sRGB (via
    /// the PNG's gAMA, cHRM, sRGB/iCCP chunks).
    /// </summary>
    /// <remarks>
    /// Blorb "The Adaptive Palette Chunk" — "Whenever a picture not
    /// listed in the APal chunk is plotted, its palette … should be
    /// copied into the Current Palette. If its palette has fewer than
    /// 16 entries, then only those entries of the Current Palette are
    /// changed."
    /// </remarks>
    /// <param name="plteColors">
    /// The PNG PLTE entries (up to 16), already in sRGB space.
    /// </param>
    public void UpdateFromNonAdaptivePicture((byte R, byte G, byte B)[] plteColors)
    {
        int count = Math.Min(plteColors.Length, 16);
        for (int i = 0; i < count; i++)
            _currentPalette[i] = plteColors[i];
        _paletteVersion++;
    }

    /// <summary>
    /// Resets the entire current palette to black and increments the
    /// version. Useful if the interpreter needs to clear palette state.
    /// </summary>
    public void Reset()
    {
        Array.Clear(_currentPalette);
        _paletteVersion++;
    }
}
