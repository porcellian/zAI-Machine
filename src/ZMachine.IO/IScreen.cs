namespace ZMachine.IO;

/// <summary>
/// Abstraction for Z-Machine screen output, covering text display, window
/// management, cursor positioning, and styling. Each screen backend (console,
/// GUI theme) implements this interface.
/// </summary>
/// <remarks>
/// ZSpec S8 — The screen model varies by version:
/// V1–3 use a status line + single scrolling window.
/// V4–5 add a split upper/lower window model.
/// V6 provides 8 fully independent windows (handled via extended methods).
/// </remarks>
public interface IScreen
{
    /// <summary>Prints a string to the current output window.</summary>
    void Print(string text);

    /// <summary>Prints a single ZSCII character to the current output window.</summary>
    void PrintChar(char c);

    /// <summary>Outputs a newline in the current window.</summary>
    void NewLine();

    /// <summary>
    /// Displays the V1–3 status line with location name and score or time.
    /// </summary>
    /// <param name="location">The short name of the object in global variable 0.</param>
    /// <param name="scoreOrTime">Formatted score/turns or hours:minutes string.</param>
    void ShowStatusLine(string location, string scoreOrTime);

    /// <summary>
    /// Splits the screen into upper and lower windows.
    /// </summary>
    /// <param name="lines">Number of lines for the upper window; 0 to unsplit.</param>
    /// <remarks>ZSpec S8.7 — @split_window</remarks>
    void SplitWindow(int lines);

    /// <summary>
    /// Selects the active output window.
    /// </summary>
    /// <param name="window">0 for lower (scrolling), 1 for upper (fixed).</param>
    void SetWindow(int window);

    /// <summary>Erases the current line in the current window.</summary>
    void EraseLine();

    /// <summary>
    /// Erases a window or performs a special erase operation.
    /// </summary>
    /// <param name="window">
    /// 0 or 1 to clear that window; -1 to clear+unsplit; -2 to clear only.
    /// </param>
    /// <remarks>ZSpec S8.7.3 — @erase_window</remarks>
    void EraseWindow(int window);

    /// <summary>
    /// Sets the cursor position in the upper window (1-based coordinates).
    /// </summary>
    /// <remarks>ZSpec S8.7.2 — @set_cursor; line and column are 1-based.</remarks>
    void SetCursor(int line, int column);

    /// <summary>
    /// Sets the text style for subsequent output.
    /// </summary>
    /// <param name="style">
    /// Bitmask: 0=Roman, 1=Reverse, 2=Bold, 4=Italic, 8=Fixed-pitch.
    /// Combinations are applied via addition.
    /// </param>
    /// <remarks>ZSpec11 "@set_text_style" — priority: Fixed > Italic > Bold > Reverse.</remarks>
    void SetTextStyle(int style);

    /// <summary>
    /// Enables or disables output buffering (word wrap) for the lower window.
    /// </summary>
    void BufferMode(bool enabled);

    /// <summary>
    /// Gets the screen dimensions in character cells.
    /// </summary>
    /// <returns>Tuple of (columns, rows).</returns>
    (int Columns, int Rows) GetScreenSize();
}
