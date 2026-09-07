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
    /// <param name="timeoutTenths">
    /// Timeout in tenths of a second (0 = no timeout). V4+ timed input.
    /// </param>
    /// <returns>
    /// The input string (without the terminating newline), and the
    /// terminating ZSCII character (13 for enter, 0 if timed out).
    /// </returns>
    (string Text, int TerminatingChar) ReadLine(int maxLength, int timeoutTenths = 0);

    /// <summary>
    /// Reads a single keypress and returns its ZSCII code.
    /// </summary>
    /// <param name="timeoutTenths">
    /// Timeout in tenths of a second (0 = no timeout). V4+ timed input.
    /// </param>
    /// <returns>
    /// ZSCII code of the pressed key (13 for Enter, 129-154 for
    /// cursor/function keys, 0 if timed out).
    /// </returns>
    int ReadChar(int timeoutTenths = 0);

    /// <summary>
    /// Whether this input stream has more input available (relevant for
    /// file playback streams that can be exhausted).
    /// </summary>
    bool HasMore { get; }
}
