namespace ZMachine.Core;

/// <summary>
/// Implements the @read (sread/aread) opcode logic: writes input to the
/// text buffer, converts to lowercase, tokenizes, and writes the parse
/// buffer. Handles V1-4 and V5+ text buffer format differences.
/// </summary>
/// <remarks>
/// ZSpec S15 — Text buffer format:
///   V1-4: byte 0 = max chars, text starts at byte 1, null-terminated.
///   V5+:  byte 0 = max chars, byte 1 = char count (set by interpreter),
///          text starts at byte 2, not null-terminated.
/// ZSpec11 "@read" — @read stores the terminating character (13 for
/// enter) in V5+. Must return 13 for enter.
/// </remarks>
public class ReadHandler
{
    private readonly int _version;
    private readonly Memory _memory;
    private readonly Tokenizer _tokenizer;
    private readonly Dictionary _dictionary;

    public ReadHandler(int version, Memory memory, Tokenizer tokenizer, Dictionary dictionary)
    {
        _version = version;
        _memory = memory;
        _tokenizer = tokenizer;
        _dictionary = dictionary;
    }

    /// <summary>
    /// Processes a line of input: writes to the text buffer, converts to
    /// lowercase, and tokenizes into the parse buffer.
    /// </summary>
    /// <param name="input">The raw input string from the input stream.</param>
    /// <param name="textBufferAddr">Address of the text buffer.</param>
    /// <param name="parseBufferAddr">
    /// Address of the parse buffer (0 to skip tokenization, V5+).
    /// </param>
    /// <returns>
    /// The terminating character (13 for enter). In V5+, this is stored
    /// as the result of the @read instruction.
    /// </returns>
    public int ProcessRead(string input, int textBufferAddr, int parseBufferAddr)
    {
        string lowered = input.ToLowerInvariant();

        WriteTextBuffer(lowered, textBufferAddr);

        if (parseBufferAddr != 0)
        {
            int textOffset = _version <= 4 ? 1 : 2;
            _tokenizer.Tokenize(lowered, _dictionary, _memory, parseBufferAddr,
                textBufferOffset: textOffset);
        }

        return 13;
    }

    /// <summary>
    /// Writes the input text to the text buffer in the appropriate format.
    /// </summary>
    private void WriteTextBuffer(string text, int textBufferAddr)
    {
        int maxChars = _memory.ReadByte(textBufferAddr);

        if (text.Length > maxChars)
            text = text[..maxChars];

        if (_version <= 4)
        {
            // V1-4: text starts at byte 1, null-terminated.
            for (int i = 0; i < text.Length; i++)
                _memory.WriteByte(textBufferAddr + 1 + i, (byte)text[i]);
            _memory.WriteByte(textBufferAddr + 1 + text.Length, 0);
        }
        else
        {
            // V5+: byte 1 = char count, text starts at byte 2, not null-terminated.
            _memory.WriteByte(textBufferAddr + 1, (byte)text.Length);
            for (int i = 0; i < text.Length; i++)
                _memory.WriteByte(textBufferAddr + 2 + i, (byte)text[i]);
        }
    }
}
