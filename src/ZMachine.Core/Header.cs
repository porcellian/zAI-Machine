namespace ZMachine.Core;

/// <summary>
/// Parses and provides typed access to the 64-byte Z-Machine header and
/// the optional header extension table. Also handles capability
/// negotiation — writing interpreter capabilities back into the header
/// so the game knows what features are available.
/// </summary>
/// <remarks>
/// ZSpec S11 — Header format.
/// ZSpec11 "Header capabilities bits" — Flags 1/2/3 semantics.
/// ZSpec11 "Header Extension" — Extension table words 4–6.
/// </remarks>
public class Header
{
    private readonly Memory _memory;

    /// <summary>
    /// Parses the header from a loaded story file.
    /// </summary>
    public Header(Memory memory)
    {
        _memory = memory;

        Version = memory.ReadByte(0x00);
        Flags1 = memory.ReadByte(0x01);
        ReleaseNumber = memory.ReadWord(0x02);
        HighMemoryBase = memory.ReadWord(0x04);
        InitialPC = memory.ReadWord(0x06);
        DictionaryAddress = memory.ReadWord(0x08);
        ObjectTableAddress = memory.ReadWord(0x0A);
        GlobalVariablesAddress = memory.ReadWord(0x0C);
        StaticMemoryBase = memory.ReadWord(0x0E);
        Flags2 = memory.ReadWord(0x10);

        // ZSpec S11 — Serial number is 6 ASCII bytes at $12–$17
        var serialBytes = new byte[6];
        for (int i = 0; i < 6; i++)
            serialBytes[i] = memory.ReadByte(0x12 + i);
        SerialNumber = System.Text.Encoding.ASCII.GetString(serialBytes);

        AbbreviationTableAddress = memory.ReadWord(0x18);
        FileLength = memory.ReadWord(0x1A);
        Checksum = memory.ReadWord(0x1C);

        // ZSpec S11 — Interpreter number/version at $1E/$1F (set by interpreter)
        InterpreterNumber = memory.ReadByte(0x1E);
        InterpreterVersion = memory.ReadByte(0x1F);

        // ZSpec S11 — Screen dimensions set by interpreter at $20–$27
        ScreenHeightLines = memory.ReadByte(0x20);
        ScreenWidthChars = memory.ReadByte(0x21);
        ScreenWidthUnits = memory.ReadWord(0x22);
        ScreenHeightUnits = memory.ReadWord(0x24);
        FontWidth = memory.ReadByte(0x26);
        FontHeight = memory.ReadByte(0x27);

        // ZSpec S11 — V6/V7 routine and string offsets at $28–$2B
        RoutinesOffset = memory.ReadWord(0x28);
        StringsOffset = memory.ReadWord(0x2A);

        DefaultBackgroundColor = memory.ReadByte(0x2C);
        DefaultForegroundColor = memory.ReadByte(0x2D);

        // ZSpec S11 — V5+ addresses
        TerminatingCharsAddress = memory.ReadWord(0x2E);
        OutputStream3Width = memory.ReadWord(0x30);

        // ZSpec S11 — Standard revision at $32/$33 (two separate bytes, not a word)
        StandardRevisionMajor = memory.ReadByte(0x32);
        StandardRevisionMinor = memory.ReadByte(0x33);

        AlphabetTableAddress = memory.ReadWord(0x34);
        HeaderExtensionAddress = memory.ReadWord(0x36);

        if (HeaderExtensionAddress > 0)
            Extension = new HeaderExtension(memory, HeaderExtensionAddress);
    }

    // --- Read-only fields parsed from the story file ---

    /// <summary>Z-Machine version number (1–8). Header byte $00.</summary>
    public int Version { get; }

    /// <summary>
    /// Flags 1 bitfield. Header byte $01.
    /// V1–3: bits 1=status line type, 4=no status line, 5=screen splitting,
    ///        6=variable-pitch default.
    /// V4+:  bits 0=colors, 2=bold, 3=italic, 4=fixed-space, 7=timed input;
    ///        V6 adds 1=pictures, 5=sound.
    /// </summary>
    public byte Flags1 { get; private set; }

    /// <summary>Release number. Header word $02.</summary>
    public ushort ReleaseNumber { get; }

    /// <summary>Base address of high memory. Header word $04.</summary>
    public ushort HighMemoryBase { get; }

    /// <summary>
    /// Initial program counter (V1–5: byte address) or packed address
    /// of the initial "main" routine (V6). Header word $06.
    /// </summary>
    public ushort InitialPC { get; }

    /// <summary>Byte address of the dictionary. Header word $08.</summary>
    public ushort DictionaryAddress { get; }

    /// <summary>Byte address of the object table. Header word $0A.</summary>
    public ushort ObjectTableAddress { get; }

    /// <summary>Byte address of the global variables table. Header word $0C.</summary>
    public ushort GlobalVariablesAddress { get; }

    /// <summary>Base address of static memory. Header word $0E.</summary>
    public ushort StaticMemoryBase { get; }

    /// <summary>
    /// Flags 2 bitfield. Header word $10.
    /// Bits: 0=transcripting, 1=fixed-pitch forced, 3=pictures wanted,
    /// 4=undo wanted, 5=mouse wanted, 7=sound wanted, 8=menus wanted (V6).
    /// </summary>
    public ushort Flags2 { get; private set; }

    /// <summary>Serial number — 6 ASCII characters, usually YYMMDD. Header bytes $12–$17.</summary>
    public string SerialNumber { get; }

    /// <summary>Byte address of the abbreviation table. Header word $18.</summary>
    public ushort AbbreviationTableAddress { get; }

    /// <summary>File length raw packed value. Header word $1A.</summary>
    public ushort FileLength { get; }

    /// <summary>File checksum. Header word $1C.</summary>
    public ushort Checksum { get; }

    /// <summary>Interpreter number. Header byte $1E. Set by interpreter.</summary>
    public byte InterpreterNumber { get; }

    /// <summary>Interpreter version. Header byte $1F. Set by interpreter.</summary>
    public byte InterpreterVersion { get; }

    /// <summary>Screen height in lines. Header byte $20. Set by interpreter.</summary>
    public byte ScreenHeightLines { get; }

    /// <summary>Screen width in characters. Header byte $21. Set by interpreter.</summary>
    public byte ScreenWidthChars { get; }

    /// <summary>Screen width in units (V5+). Header word $22. Set by interpreter.</summary>
    public ushort ScreenWidthUnits { get; }

    /// <summary>Screen height in units (V5+). Header word $24. Set by interpreter.</summary>
    public ushort ScreenHeightUnits { get; }

    /// <summary>
    /// Font width in units (V6) or font height in units (V5).
    /// Header byte $26. Set by interpreter.
    /// </summary>
    public byte FontWidth { get; }

    /// <summary>
    /// Font height in units (V6) or font width in units (V5).
    /// Header byte $27. Set by interpreter.
    /// </summary>
    public byte FontHeight { get; }

    /// <summary>Routines offset divided by 8 (V6–V7 only). Header word $28.</summary>
    public ushort RoutinesOffset { get; }

    /// <summary>Static strings offset divided by 8 (V6–V7 only). Header word $2A.</summary>
    public ushort StringsOffset { get; }

    /// <summary>Default background color number. Header byte $2C.</summary>
    public byte DefaultBackgroundColor { get; }

    /// <summary>Default foreground color number. Header byte $2D.</summary>
    public byte DefaultForegroundColor { get; }

    /// <summary>Address of terminating characters table (V5+). Header word $2E.</summary>
    public ushort TerminatingCharsAddress { get; }

    /// <summary>Total width in pixels of text sent to output stream 3 (V6). Header word $30.</summary>
    public ushort OutputStream3Width { get; }

    /// <summary>Standard revision major version. Header byte $32.</summary>
    public byte StandardRevisionMajor { get; }

    /// <summary>Standard revision minor version. Header byte $33.</summary>
    public byte StandardRevisionMinor { get; }

    /// <summary>Address of alphabet table (V5+). Header word $34. Zero = default alphabet.</summary>
    public ushort AlphabetTableAddress { get; }

    /// <summary>Address of header extension table (V5+). Header word $36. Zero = no extension.</summary>
    public ushort HeaderExtensionAddress { get; }

    /// <summary>Parsed header extension table, or null if not present.</summary>
    public HeaderExtension? Extension { get; }

    // --- Capability negotiation ---

    /// <summary>
    /// Writes interpreter identification and capabilities into the header.
    /// Called once after loading a story file and before execution begins.
    /// </summary>
    /// <remarks>
    /// ZSpec S11 — Interpreter sets bytes $1E/$1F, $20–$27, $32/$33,
    /// and capability bits in Flags 1.
    /// ZSpec11 "Header capabilities bits" — Set bits for available features,
    /// clear bits for unavailable ones.
    /// </remarks>
    /// <param name="interpreterNumber">
    /// Interpreter number (ZSpec S11.1.3): 6 = IBM PC compatible.
    /// </param>
    /// <param name="interpreterVersion">
    /// Interpreter version — ASCII letter recommended (e.g., 'A' = 65).
    /// </param>
    /// <param name="screenWidth">Screen width in characters.</param>
    /// <param name="screenHeight">Screen height in lines.</param>
    /// <param name="capabilities">Capability flags the interpreter supports.</param>
    public void ConfigureInterpreter(
        byte interpreterNumber,
        byte interpreterVersion,
        int screenWidth,
        int screenHeight,
        InterpreterCapabilities capabilities)
    {
        // ZSpec S11 — Interpreter identification
        _memory.WriteByte(0x1E, interpreterNumber);
        _memory.WriteByte(0x1F, interpreterVersion);

        // ZSpec S11 — Screen dimensions
        _memory.WriteByte(0x20, (byte)Math.Min(screenHeight, 255));
        _memory.WriteByte(0x21, (byte)Math.Min(screenWidth, 255));

        if (Version >= 5)
        {
            _memory.WriteWord(0x22, (ushort)screenWidth);
            _memory.WriteWord(0x24, (ushort)screenHeight);
            // Font size: 1×1 units as placeholder until font system is ready
            _memory.WriteByte(0x26, 1);
            _memory.WriteByte(0x27, 1);
        }

        // ZSpec S11 — Standard revision: 1.1
        _memory.WriteByte(0x32, 0x01);
        _memory.WriteByte(0x33, 0x01);

        SetCapabilityBits(capabilities);

        // ZSpec11 "Header Extension" — Clear all reserved Flags 3 bits
        if (Extension is not null)
            Extension.ClearReservedFlags3Bits(_memory);
    }

    /// <summary>
    /// Sets Flags 1 capability bits based on what the interpreter supports.
    /// The meaning of Flags 1 bits differs between V1–3 and V4+.
    /// </summary>
    private void SetCapabilityBits(InterpreterCapabilities caps)
    {
        byte flags1 = Flags1;

        if (Version <= 3)
        {
            // ZSpec S11 — V1–3 Flags 1 (interpreter sets bits 4–6)
            // Bit 4: Status line NOT available (set = not available)
            flags1 = SetBit(flags1, 4, false);
            // Bit 5: Screen splitting available
            flags1 = SetBit(flags1, 5, caps.HasFlag(InterpreterCapabilities.ScreenSplitting));
            // Bit 6: Variable-pitch font is default
            flags1 = SetBit(flags1, 6, caps.HasFlag(InterpreterCapabilities.VariablePitchDefault));
        }
        else
        {
            // ZSpec S11 — V4+ Flags 1 (interpreter sets bits 0–5, 7)
            flags1 = SetBit(flags1, 0, caps.HasFlag(InterpreterCapabilities.Colors));
            flags1 = SetBit(flags1, 2, caps.HasFlag(InterpreterCapabilities.Bold));
            flags1 = SetBit(flags1, 3, caps.HasFlag(InterpreterCapabilities.Italic));
            flags1 = SetBit(flags1, 4, caps.HasFlag(InterpreterCapabilities.FixedSpace));
            flags1 = SetBit(flags1, 7, caps.HasFlag(InterpreterCapabilities.TimedInput));

            if (Version >= 6)
            {
                flags1 = SetBit(flags1, 1, caps.HasFlag(InterpreterCapabilities.Pictures));
                flags1 = SetBit(flags1, 5, caps.HasFlag(InterpreterCapabilities.Sound));
            }
        }

        _memory.WriteByte(0x01, flags1);
        Flags1 = flags1;

        // ZSpec11 "Header capabilities bits" — Clear Flags 2 bits the
        // interpreter can't provide. These are "game wants" bits; the
        // interpreter clears them if it can't satisfy the request.
        if (Version >= 5)
        {
            ushort flags2 = Flags2;

            // Bit 3: pictures — clear if no pictures available
            if (!caps.HasFlag(InterpreterCapabilities.Pictures))
                flags2 = SetBit(flags2, 3, false);
            // Bit 4: undo — clear if no undo support
            if (!caps.HasFlag(InterpreterCapabilities.Undo))
                flags2 = SetBit(flags2, 4, false);
            // Bit 5: mouse — clear if no mouse support
            if (!caps.HasFlag(InterpreterCapabilities.Mouse))
                flags2 = SetBit(flags2, 5, false);
            // Bit 7: sound — clear if no sound available
            if (!caps.HasFlag(InterpreterCapabilities.Sound))
                flags2 = SetBit(flags2, 7, false);
            // Bit 8: menus (V6) — clear if no menu support
            if (Version >= 6 && !caps.HasFlag(InterpreterCapabilities.Menus))
                flags2 = SetBit(flags2, 8, false);

            _memory.WriteWord(0x10, flags2);
            Flags2 = flags2;
        }
    }

    private static byte SetBit(byte value, int bit, bool on)
    {
        if (on)
            return (byte)(value | (1 << bit));
        return (byte)(value & ~(1 << bit));
    }

    private static ushort SetBit(ushort value, int bit, bool on)
    {
        if (on)
            return (ushort)(value | (1 << bit));
        return (ushort)(value & ~(1 << bit));
    }
}

/// <summary>
/// Interpreter capability flags used during header negotiation.
/// The interpreter declares which features it supports; the header
/// parser writes the corresponding bits into Flags 1 and clears
/// unsupported "game wants" bits in Flags 2.
/// </summary>
[Flags]
public enum InterpreterCapabilities
{
    None = 0,

    /// <summary>V4+: Interpreter can display colors (Flags 1 bit 0).</summary>
    Colors = 1 << 0,

    /// <summary>V6: Interpreter can display pictures (Flags 1 bit 1, Flags 2 bit 3).</summary>
    Pictures = 1 << 1,

    /// <summary>V4+: Bold style available (Flags 1 bit 2).</summary>
    Bold = 1 << 2,

    /// <summary>V4+: Italic style available (Flags 1 bit 3).</summary>
    Italic = 1 << 3,

    /// <summary>V4+: Fixed-space style available (Flags 1 bit 4).</summary>
    FixedSpace = 1 << 4,

    /// <summary>V6: Sound effects available (Flags 1 bit 5, Flags 2 bit 7).</summary>
    Sound = 1 << 5,

    /// <summary>V4+: Timed keyboard input available (Flags 1 bit 7).</summary>
    TimedInput = 1 << 6,

    /// <summary>V1–3: Screen splitting available (Flags 1 bit 5).</summary>
    ScreenSplitting = 1 << 7,

    /// <summary>V1–3: Variable-pitch font is the default (Flags 1 bit 6).</summary>
    VariablePitchDefault = 1 << 8,

    /// <summary>V5+: Undo opcodes supported (Flags 2 bit 4).</summary>
    Undo = 1 << 9,

    /// <summary>V5+: Mouse input available (Flags 2 bit 5).</summary>
    Mouse = 1 << 10,

    /// <summary>V6: Menu system available (Flags 2 bit 8).</summary>
    Menus = 1 << 11,
}
