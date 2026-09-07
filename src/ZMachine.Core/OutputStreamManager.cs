namespace ZMachine.Core;

/// <summary>
/// Manages the four Z-Machine output streams, dispatching printed text
/// to all active streams. Stream 3 (memory table) is special: when active,
/// it suppresses all other streams.
/// </summary>
/// <remarks>
/// ZSpec S7.1 — Four output streams:
///   1 = screen (always initially on)
///   2 = transcript file
///   3 = memory table (V5+, nestable up to 16 levels)
///   4 = player input recording file
/// ZSpec S7.2 — When stream 3 is active, text goes ONLY to stream 3.
/// ZSpec11 "@output_stream" — Stream 3 can be nested up to 16 levels,
///   each with its own table address.
/// </remarks>
public class OutputStreamManager
{
    /// <summary>Called to send text to the screen (stream 1).</summary>
    public Action<string>? ScreenPrint { get; set; }

    /// <summary>Called to send text to the transcript file (stream 2).</summary>
    public Action<string>? TranscriptPrint { get; set; }

    /// <summary>Called to send text to the command recording file (stream 4).</summary>
    public Action<string>? CommandPrint { get; set; }

    private readonly Memory _memory;
    private bool _stream1Active = true;
    private bool _stream2Active;
    private bool _stream4Active;

    // ZSpec11 "@output_stream" — Stream 3 can be nested up to 16 levels.
    private const int MaxStream3Nesting = 16;
    private readonly int[] _stream3Stack = new int[MaxStream3Nesting];
    private int _stream3Depth;

    public OutputStreamManager(Memory memory)
    {
        _memory = memory;
    }

    /// <summary>
    /// Enables or disables an output stream. For stream 3, a positive
    /// stream number pushes a new table address onto the stack; a negative
    /// stream number (-3) pops the current level.
    /// </summary>
    /// <param name="stream">
    /// Positive to enable, negative to disable. Magnitude is the stream
    /// number (1-4).
    /// </param>
    /// <param name="tableAddress">
    /// For stream 3: the address of the memory table to write to.
    /// Ignored for other streams.
    /// </param>
    /// <remarks>
    /// ZSpec S7.1.2 — Enabling/disabling uses signed stream numbers:
    /// positive to select (enable), negative to deselect (disable).
    /// ZSpec11 "@output_stream" — Stream 3 with a table address pushes
    /// a new nesting level; deselecting pops one level.
    /// </remarks>
    public void SelectStream(int stream, int tableAddress = 0)
    {
        bool enable = stream > 0;
        int streamNum = Math.Abs(stream);

        switch (streamNum)
        {
            case 1:
                _stream1Active = enable;
                break;

            case 2:
                _stream2Active = enable;
                break;

            case 3:
                if (enable)
                {
                    if (_stream3Depth >= MaxStream3Nesting)
                        throw new InvalidOperationException(
                            "Stream 3 nesting exceeds 16 levels.");

                    _stream3Stack[_stream3Depth] = tableAddress;
                    // ZSpec S7.1.2.1 — Initialize the character count word to 0.
                    _memory.WriteWord(tableAddress, 0);
                    _stream3Depth++;
                }
                else
                {
                    if (_stream3Depth > 0)
                        _stream3Depth--;
                }
                break;

            case 4:
                _stream4Active = enable;
                break;
        }
    }

    /// <summary>Whether stream 3 is currently active (any nesting level).</summary>
    public bool IsStream3Active => _stream3Depth > 0;

    /// <summary>Whether stream 2 (transcript) is enabled.</summary>
    public bool IsTranscriptActive => _stream2Active;

    /// <summary>
    /// Prints text to all active output streams. When stream 3 is active,
    /// text goes ONLY to stream 3 (all other streams are suppressed).
    /// </summary>
    /// <remarks>
    /// ZSpec S7.2 — "While stream 3 is selected, no text is sent to any
    /// other stream." This applies per-character for the current nesting
    /// level.
    /// </remarks>
    public void Print(string text)
    {
        if (_stream3Depth > 0)
        {
            WriteToStream3(text);
            return;
        }

        if (_stream1Active)
            ScreenPrint?.Invoke(text);

        if (_stream2Active)
            TranscriptPrint?.Invoke(text);

        if (_stream4Active)
            CommandPrint?.Invoke(text);
    }

    /// <summary>
    /// Prints a single character to all active output streams.
    /// </summary>
    public void PrintChar(char c)
    {
        Print(c.ToString());
    }

    /// <summary>
    /// Writes text to the current stream 3 memory table. The table format
    /// is: word 0 = character count, then ZSCII bytes starting at offset 2.
    /// </summary>
    /// <remarks>
    /// ZSpec S7.1.2.1 — Stream 3 table format: the first word holds the
    /// number of characters written so far. Characters follow as ZSCII
    /// bytes starting at byte 2 of the table.
    /// </remarks>
    private void WriteToStream3(string text)
    {
        int tableAddr = _stream3Stack[_stream3Depth - 1];
        int count = _memory.ReadWord(tableAddr);

        foreach (char c in text)
        {
            _memory.WriteByte(tableAddr + 2 + count, (byte)c);
            count++;
        }

        _memory.WriteWord(tableAddr, (ushort)count);
    }
}
