namespace ZMachine.Core;

/// <summary>
/// Implements Z-Machine text output opcodes. All output flows through
/// an <see cref="OutputStreamManager"/> to respect stream selection
/// (screen, transcript, memory table, command recording).
/// </summary>
/// <remarks>
/// ZSpec S15 — Text output instructions.
/// ZSpec S3 — Z-string encoding format.
/// </remarks>
public class TextOutputOps
{
    private readonly Memory _memory;
    private readonly TextDecoder _textDecoder;
    private readonly TextEncoder _textEncoder;
    private readonly OutputStreamManager _outputStreams;
    private readonly int _version;

    public TextOutputOps(
        Memory memory,
        TextDecoder textDecoder,
        TextEncoder textEncoder,
        OutputStreamManager outputStreams,
        int version)
    {
        _memory = memory;
        _textDecoder = textDecoder;
        _textEncoder = textEncoder;
        _outputStreams = outputStreams;
        _version = version;
    }

    /// <summary>
    /// @print — decodes the inline Z-string at the given address and
    /// prints it. Returns the number of bytes consumed so the caller
    /// can advance PC past the string.
    /// </summary>
    /// <remarks>
    /// ZSpec S15 — @print: "Print the quoted (literal) Z-encoded string."
    /// The Z-string immediately follows the opcode byte(s).
    /// </remarks>
    public int Print(int address)
    {
        var (text, byteLen) = _textDecoder.DecodeZString(address);
        _outputStreams.Print(text);
        return byteLen;
    }

    /// <summary>
    /// @print_ret — decodes the inline Z-string, prints it followed by
    /// a newline, then returns true (1) from the current routine.
    /// Returns the byte length consumed. The caller must handle the
    /// routine return.
    /// </summary>
    public int PrintRet(int address)
    {
        var (text, byteLen) = _textDecoder.DecodeZString(address);
        _outputStreams.Print(text);
        _outputStreams.Print("\n");
        return byteLen;
    }

    /// <summary>
    /// @print_addr addr — prints the Z-string at the given byte address.
    /// </summary>
    public void PrintAddr(ushort address)
    {
        var (text, _) = _textDecoder.DecodeZString(address);
        _outputStreams.Print(text);
    }

    /// <summary>
    /// @print_paddr packed-addr — prints the Z-string at the unpacked
    /// string address. The caller must unpack the address before calling.
    /// </summary>
    public void PrintPAddr(int unpackedAddress)
    {
        var (text, _) = _textDecoder.DecodeZString(unpackedAddress);
        _outputStreams.Print(text);
    }

    /// <summary>
    /// @print_char zscii-code — prints a single ZSCII character.
    /// </summary>
    /// <remarks>ZSpec S15 — @print_char: "Print a ZSCII character."</remarks>
    public void PrintChar(ushort zsciiCode)
    {
        _outputStreams.PrintChar((char)zsciiCode);
    }

    /// <summary>
    /// @print_num value — prints a signed decimal number.
    /// </summary>
    /// <remarks>ZSpec S15 — @print_num: "Print (signed) number in decimal."</remarks>
    public void PrintNum(ushort value)
    {
        _outputStreams.Print(((short)value).ToString());
    }

    /// <summary>
    /// @new_line — prints a newline character.
    /// </summary>
    public void NewLine()
    {
        _outputStreams.Print("\n");
    }

    /// <summary>
    /// @print_table table width [height] [skip] — prints a rectangular
    /// table of ZSCII characters. Each row is width bytes from the table,
    /// followed by a newline. Rows are separated by skip bytes in memory
    /// (default 0). Height defaults to 1.
    /// </summary>
    /// <remarks>
    /// ZSpec S15 — @print_table: "Print a rectangle of text from a table
    /// of ZSCII characters." V5+ only.
    /// </remarks>
    public void PrintTable(ushort table, ushort width, ushort height, ushort skip)
    {
        int addr = table;
        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                byte ch = _memory.ReadByte(addr + col);
                _outputStreams.PrintChar((char)ch);
            }

            addr += width + skip;

            if (row < height - 1)
                _outputStreams.Print("\n");
        }
    }

    /// <summary>
    /// @print_unicode char-code (EXT, V5+) — prints a Unicode character
    /// from the Basic Multilingual Plane. Control codes (0-31, 127-159)
    /// are rejected.
    /// </summary>
    /// <remarks>
    /// ZSpec11 "@print_unicode" — "Prints a Unicode character. Only
    /// characters in the Basic Multilingual Plane are required."
    /// </remarks>
    public void PrintUnicode(ushort charCode)
    {
        if (charCode <= 31 || (charCode >= 127 && charCode <= 159))
            return;

        _outputStreams.PrintChar((char)charCode);
    }

    /// <summary>
    /// @encode_text (V5+) — encodes a ZSCII string from memory into
    /// Z-character dictionary form and writes it to a destination address.
    /// </summary>
    /// <remarks>
    /// ZSpec S15 — @encode_text zscii-text length from coded-text:
    /// Encodes length bytes starting at zscii-text+from into dictionary
    /// encoding at coded-text (6 bytes for V5+).
    /// </remarks>
    public void EncodeText(ushort zsciiText, ushort length, ushort from, ushort codedText)
    {
        var chars = new char[length];
        for (int i = 0; i < length; i++)
            chars[i] = (char)_memory.ReadByte(zsciiText + from + i);

        string text = new(chars);
        byte[] encoded = _textEncoder.EncodeForDictionary(text);

        for (int i = 0; i < encoded.Length && i < 6; i++)
            _memory.WriteByte(codedText + i, encoded[i]);
    }
}
