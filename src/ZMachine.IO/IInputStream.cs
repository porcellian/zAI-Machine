namespace ZMachine.IO;

/// <summary>
/// Abstraction for Z-Machine input, covering line input (@read) and
/// single-character input (@read_char), with optional timed input support.
/// </summary>
/// <remarks>
/// ZSpec S10 — Input comes from keyboard (stream 0) or file playback
/// (stream 1). Timed input (V4+) uses a callback routine that fires
/// after a timeout; if the callback returns true, input is cancelled.
/// </remarks>
public interface IInputStream
{
    /// <summary>
    /// Reads a line of text input from the user.
    /// </summary>
    /// <param name="maxLength">Maximum number of characters to accept.</param>
    /// <returns>The input string (without the terminating newline).</returns>
    string ReadLine(int maxLength);

    /// <summary>
    /// Reads a single keypress and returns its ZSCII code.
    /// </summary>
    /// <returns>ZSCII code of the pressed key (e.g., 13 for Enter, 129–154 for cursor/function keys).</returns>
    int ReadChar();
}
