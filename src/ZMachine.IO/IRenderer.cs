namespace ZMachine.IO;

using SkiaSharp;

/// <summary>
/// Abstraction for pixel-level rendering of the Z-Machine display.
/// All themes render through this interface; the default implementation
/// uses SkiaSharp to draw to an off-screen bitmap back buffer.
/// </summary>
/// <remarks>
/// ZSpec S8 — The screen model: characters are drawn into a grid,
/// with support for styled text (bold, italic, reverse, fixed-pitch)
/// and optional image display (V6 / Blorb resources).
/// </remarks>
public interface IRenderer
{
    /// <summary>
    /// Initializes the renderer with a given pixel size and theme.
    /// Creates the back buffer and loads the font.
    /// </summary>
    void Initialize(int widthPixels, int heightPixels, ThemeConfig theme);

    /// <summary>
    /// Draws a single character at the specified grid position with
    /// foreground/background colors and text style.
    /// </summary>
    /// <param name="col">Column in the character grid (0-based).</param>
    /// <param name="row">Row in the character grid (0-based).</param>
    /// <param name="c">The character to draw.</param>
    /// <param name="fg">Foreground color.</param>
    /// <param name="bg">Background color.</param>
    /// <param name="style">
    /// Text style bitmask: 1=Reverse, 2=Bold, 4=Italic, 8=Fixed-pitch.
    /// ZSpec S8.7.1 — @set_text_style.
    /// </param>
    void DrawCharacter(int col, int row, char c, SKColor fg, SKColor bg, int style);

    /// <summary>
    /// Fills a rectangular pixel region with a solid color.
    /// Used for clearing windows, borders, and background fills.
    /// </summary>
    void DrawRegion(int x, int y, int w, int h, SKColor color);

    /// <summary>
    /// Draws a scaled bitmap image at the specified pixel position.
    /// Used for V6 picture display and Blorb resource rendering.
    /// </summary>
    void DrawImage(int x, int y, SKBitmap image, int scaledW, int scaledH);

    /// <summary>
    /// Sets the cursor position in the character grid (0-based).
    /// The renderer draws a cursor indicator at this position.
    /// </summary>
    void SetCursorPosition(int col, int row);

    /// <summary>
    /// Flushes the back buffer to the display surface.
    /// Must be called after drawing operations to make changes visible.
    /// </summary>
    void Refresh();

    /// <summary>
    /// Gets the screen dimensions in character cells.
    /// </summary>
    (int Columns, int Rows) GetScreenSize();

    /// <summary>
    /// Gets the current back buffer bitmap for blitting to the display.
    /// Returns null if the renderer has not been initialized.
    /// </summary>
    SKBitmap? BackBuffer { get; }
}
