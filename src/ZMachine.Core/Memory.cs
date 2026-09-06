namespace ZMachine.Core;

/// <summary>
/// The Z-Machine memory model: a big-endian byte array divided into three
/// regions — dynamic (writable), static (read-only at runtime), and high
/// (code and strings, also read-only). A pristine copy of the original
/// file is retained for @restart and Quetzal XOR compression.
/// </summary>
/// <remarks>
/// ZSpec S1 — Memory map layout.
/// ZSpec11 "Memory layout" — V6/V7 limited to 512K, not 320K.
/// ZSpec11 "Padding" — Padding beyond header-declared length excluded from checksum.
/// </remarks>
public class Memory
{
    /// <summary>
    /// Maximum story file sizes by version (in bytes).
    /// V1–3: 128K, V4–5: 256K, V6–7: 512K, V8: 512K.
    /// </summary>
    private static readonly int[] MaxStorySize =
    [
        0,       // version 0 (unused)
        128 * 1024, // V1
        128 * 1024, // V2
        128 * 1024, // V3
        256 * 1024, // V4
        256 * 1024, // V5
        512 * 1024, // V6 — ZSpec11 "Memory layout": 512K, not 320K
        512 * 1024, // V7 — ZSpec11 "Memory layout": 512K, not 320K
        512 * 1024, // V8
    ];

    private byte[] _bytes = [];

    /// <summary>
    /// Immutable copy of the original story file bytes, used for @restart
    /// and Quetzal CMem XOR compression (Quetzal S3.2).
    /// </summary>
    public byte[] OriginalBytes { get; private set; } = [];

    /// <summary>The Z-Machine version number (1–8), read from header byte 0.</summary>
    public int Version { get; private set; }

    /// <summary>
    /// Start of dynamic memory. Always 0 — the dynamic region begins at
    /// the first byte of the file and extends up to (but not including)
    /// StaticBase.
    /// </summary>
    public int DynamicBase => 0;

    /// <summary>
    /// Start of static memory. Read from header word at $0E.
    /// Writes to addresses at or above this value are illegal at runtime.
    /// </summary>
    public int StaticBase { get; private set; }

    /// <summary>
    /// Start of high memory (code and packed strings). Read from header
    /// word at $04. This region is never directly accessible to the game
    /// via memory read/write opcodes.
    /// </summary>
    public int HighBase { get; private set; }

    /// <summary>
    /// The file length as declared in the header (bytes $1A–$1B), unpacked
    /// according to the version-specific multiplier. Zero if the header
    /// field is zero (some very early V1–V3 files omit this).
    /// </summary>
    public int FileLength { get; private set; }

    /// <summary>
    /// The checksum declared in the header (bytes $1C–$1D). This is the
    /// sum of all bytes from $40 to the end of the file (at the header-
    /// declared length), modulo 65536. Padding is excluded.
    /// </summary>
    public ushort HeaderChecksum { get; private set; }

    /// <summary>Total number of bytes in the loaded story file.</summary>
    public int Size => _bytes.Length;

    /// <summary>
    /// Loads a story file from disk.
    /// </summary>
    /// <exception cref="FileNotFoundException">Thrown if the file does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the file fails validation.</exception>
    public void LoadStory(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Story file not found.", path);

        LoadStory(File.ReadAllBytes(path));
    }

    /// <summary>
    /// Loads a story from a byte array. Validates version, size limits,
    /// and memory region boundaries before accepting the file.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if validation fails.</exception>
    public void LoadStory(byte[] data)
    {
        if (data.Length < 64)
            throw new InvalidOperationException(
                $"Story file too small ({data.Length} bytes). Minimum is 64 bytes for the header.");

        int version = data[0];
        if (version < 1 || version > 8)
            throw new InvalidOperationException(
                $"Unsupported Z-Machine version {version}. Versions 1–8 are supported.");

        if (data.Length > MaxStorySize[version])
            throw new InvalidOperationException(
                $"Story file ({data.Length} bytes) exceeds maximum for V{version} ({MaxStorySize[version]} bytes).");

        // ZSpec S1 — Static memory base from header word $0E
        int staticBase = ReadWordAt(data, 0x0E);
        if (staticBase == 0 || staticBase > data.Length)
            throw new InvalidOperationException(
                $"Invalid static memory base ${staticBase:X4} for a {data.Length}-byte file.");

        // ZSpec S1 — High memory base from header word $04
        int highBase = ReadWordAt(data, 0x04);

        // ZSpec S1.1.2 — File length from header word $1A, with version-dependent packing
        int declaredLength = UnpackFileLength(ReadWordAt(data, 0x1A), version);

        // ZSpec11 "Padding" — If the file is longer than the declared length, the
        // extra bytes are padding. Some Infocom files have non-zero padding, which
        // must be excluded from checksum calculations.
        if (declaredLength > 0 && data.Length < declaredLength)
            throw new InvalidOperationException(
                $"Story file ({data.Length} bytes) is shorter than header-declared length ({declaredLength} bytes).");

        Version = version;
        StaticBase = staticBase;
        HighBase = highBase;
        FileLength = declaredLength;
        HeaderChecksum = ReadWordAt(data, 0x1C);

        _bytes = new byte[data.Length];
        Array.Copy(data, _bytes, data.Length);

        OriginalBytes = new byte[data.Length];
        Array.Copy(data, OriginalBytes, data.Length);
    }

    /// <summary>Reads a single byte from the given address.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if address is out of bounds.</exception>
    public byte ReadByte(int address)
    {
        ValidateReadAddress(address);
        return _bytes[address];
    }

    /// <summary>
    /// Reads a big-endian unsigned 16-bit word from the given address.
    /// </summary>
    /// <remarks>ZSpec S1 — All multi-byte values in the Z-Machine are big-endian.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if address is out of bounds.</exception>
    public ushort ReadWord(int address)
    {
        ValidateReadAddress(address + 1);
        return (ushort)((_bytes[address] << 8) | _bytes[address + 1]);
    }

    /// <summary>
    /// Writes a single byte to dynamic memory.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if address is in static or high memory.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if address is out of bounds.</exception>
    public void WriteByte(int address, byte value)
    {
        ValidateWriteAddress(address);
        _bytes[address] = value;
    }

    /// <summary>
    /// Writes a big-endian unsigned 16-bit word to dynamic memory.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if address is in static or high memory.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if address is out of bounds.</exception>
    public void WriteWord(int address, ushort value)
    {
        ValidateWriteAddress(address);
        ValidateWriteAddress(address + 1);
        _bytes[address] = (byte)(value >> 8);
        _bytes[address + 1] = (byte)(value & 0xFF);
    }

    /// <summary>
    /// Computes the checksum of the story file: the sum of all bytes from
    /// $40 to the header-declared file length, modulo 65536. Padding beyond
    /// the declared length is excluded per ZSpec11 "Padding".
    /// </summary>
    /// <returns>The computed checksum, or 0 if the header declares no file length.</returns>
    public ushort ComputeChecksum()
    {
        // ZSpec11 "Padding" — checksum covers only bytes within the declared length.
        // Some early V1–V3 files have FileLength == 0 (header field is zero).
        int end = FileLength > 0 ? Math.Min(FileLength, _bytes.Length) : _bytes.Length;

        int sum = 0;
        for (int i = 0x40; i < end; i++)
            sum += _bytes[i];

        return (ushort)(sum & 0xFFFF);
    }

    /// <summary>
    /// Restores dynamic memory to its original state (for @restart).
    /// Only the dynamic region (0 to StaticBase-1) is overwritten.
    /// </summary>
    public void RestoreDynamicMemory()
    {
        Array.Copy(OriginalBytes, 0, _bytes, 0, StaticBase);
    }

    /// <summary>
    /// Provides direct read access to the raw byte array for performance-
    /// critical paths (instruction decoding, text decoding). Callers must
    /// not write through this span.
    /// </summary>
    public ReadOnlySpan<byte> RawBytes => _bytes;

    /// <summary>
    /// Provides direct write access to the dynamic memory region for bulk
    /// operations (Quetzal restore). Callers must respect StaticBase.
    /// </summary>
    internal Span<byte> DynamicSpan => _bytes.AsSpan(0, StaticBase);

    /// <summary>
    /// Unpacks the file length header field according to the version-
    /// specific multiplier.
    /// </summary>
    /// <remarks>
    /// ZSpec S1.1.2 — Packed file length:
    /// V1–3: value × 2, V4–5: value × 4, V6–7: value × 8, V8: value × 8.
    /// </remarks>
    private static int UnpackFileLength(ushort packed, int version)
    {
        if (packed == 0)
            return 0;

        return version switch
        {
            <= 3 => packed * 2,
            <= 5 => packed * 4,
            _    => packed * 8,
        };
    }

    /// <summary>Reads a big-endian word from a raw byte array at the given offset.</summary>
    private static ushort ReadWordAt(byte[] data, int offset)
    {
        return (ushort)((data[offset] << 8) | data[offset + 1]);
    }

    private void ValidateReadAddress(int address)
    {
        if (address < 0 || address >= _bytes.Length)
            throw new ArgumentOutOfRangeException(nameof(address),
                $"Read address ${address:X4} is outside story file bounds (0–${_bytes.Length - 1:X4}).");
    }

    private void ValidateWriteAddress(int address)
    {
        if (address < 0 || address >= _bytes.Length)
            throw new ArgumentOutOfRangeException(nameof(address),
                $"Write address ${address:X4} is outside story file bounds (0–${_bytes.Length - 1:X4}).");

        // ZSpec S1 — Writes to static or high memory are illegal at runtime
        if (address >= StaticBase)
            throw new InvalidOperationException(
                $"Write to static/high memory at ${address:X4} is illegal (static base = ${StaticBase:X4}).");
    }
}
