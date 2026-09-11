namespace ZMachine.Core;

/// <summary>
/// Parses a Blorb 2.0.4 resource file (IFF FORM type 'IFRS') and
/// provides indexed access to picture, sound, data, and executable
/// resources by usage and number.
/// </summary>
/// <remarks>
/// Blorb "Overall Structure" — FORM 'IFRS' with RIdx as first chunk.
/// Blorb "Contents of the Resource Index Chunk" — 4-byte count + 12-byte entries.
/// </remarks>
public class BlorbReader
{
    private readonly IffForm _form;
    private readonly Dictionary<(string Usage, int Number), IffChunk> _resources = new();
    private readonly Dictionary<(string Usage, int Number), string> _resourceTypes = new();
    private readonly List<string> _warnings;

    /// <summary>The underlying IFF form for advanced consumers.</summary>
    public IffForm Form => _form;

    /// <summary>
    /// Parsed color palette, or null if no 'Plte' chunk is present.
    /// Blorb "The Color Palette Chunk".
    /// </summary>
    public BlorbPalette? Palette { get; private set; }

    /// <summary>
    /// Parsed resolution data from the 'Reso' chunk, or null if absent.
    /// Blorb "The Resolution Chunk" — image scaling via ERF.
    /// </summary>
    public ResolutionInfo? Resolution { get; private set; }

    /// <summary>
    /// Resource release number from the 'RelN' chunk, or 0 if absent.
    /// Blorb "The Release Number Chunk" — passed to @picture_data 0.
    /// </summary>
    public int ReleaseNumber { get; private set; }

    /// <summary>
    /// Frontispiece picture resource number from the 'Fspc' chunk, or -1 if absent.
    /// Blorb "The Frontispiece Chunk".
    /// </summary>
    public int FrontispiecePicture { get; private set; } = -1;

    /// <summary>
    /// Game identifier data from the 'IFhd' chunk (13 bytes: release/serial/checksum/PC),
    /// or null if absent. Blorb "The Game Identifier Chunk".
    /// </summary>
    public byte[]? GameIdentifier { get; private set; }

    /// <summary>
    /// Metadata XML from the 'IFmd' chunk, or null if absent.
    /// Blorb "Metadata" — UTF-8 encoded XML.
    /// </summary>
    public string? MetadataXml { get; private set; }

    /// <summary>
    /// Author name from the 'AUTH' chunk, or null if absent.
    /// </summary>
    public string? Author { get; private set; }

    /// <summary>
    /// Copyright message from the '(c) ' chunk, or null if absent.
    /// </summary>
    public string? Copyright { get; private set; }

    /// <summary>
    /// Annotation texts from 'ANNO' chunks (may be empty).
    /// </summary>
    public IReadOnlyList<string> Annotations { get; private set; } = [];

    /// <summary>
    /// Resource descriptions from the 'RDes' chunk. Keyed by (usage, number).
    /// Blorb "The Resource Description Chunk".
    /// </summary>
    public IReadOnlyDictionary<(string Usage, int Number), string> ResourceDescriptions
        => _resourceDescriptions;
    private readonly Dictionary<(string Usage, int Number), string> _resourceDescriptions = new();

    /// <summary>
    /// Number of 'Pict' resources in the index.
    /// Used by the interpreter to set header graphics capability flags.
    /// </summary>
    public int PictureCount { get; private set; }

    /// <summary>
    /// Number of 'Snd ' resources in the index.
    /// Used by the interpreter to set header sound capability flags.
    /// </summary>
    public int SoundCount { get; private set; }

    /// <summary>Warnings generated during parsing.</summary>
    public IReadOnlyList<string> Warnings => _warnings;

    private BlorbReader(IffForm form, List<string> warnings)
    {
        _form = form;
        _warnings = warnings;
    }

    /// <summary>
    /// Loads a Blorb file from a stream.
    /// </summary>
    /// <param name="stream">A readable stream containing Blorb data.</param>
    /// <returns>A parsed <see cref="BlorbReader"/> with indexed resources.</returns>
    /// <exception cref="InvalidDataException">
    /// The data is not a valid Blorb file (wrong FORM type, missing RIdx, etc.).
    /// </exception>
    public static BlorbReader Load(Stream stream)
    {
        var form = IffReader.Parse(stream);
        return LoadFromForm(form);
    }

    /// <summary>
    /// Loads a Blorb file from a byte array.
    /// </summary>
    public static BlorbReader Load(byte[] data)
    {
        var form = IffReader.Parse(data);
        return LoadFromForm(form);
    }

    /// <summary>
    /// Returns true if a resource with the given usage and number exists.
    /// </summary>
    public bool HasResource(string usage, int number)
        => _resources.ContainsKey((usage, number));

    /// <summary>
    /// Returns the raw data for the resource with the given usage and number.
    /// For AIFF sounds (nested FORM), returns the reconstructed AIFF file bytes.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No resource with that usage/number.</exception>
    public byte[] GetResource(string usage, int number)
    {
        var chunk = _resources[(usage, number)];

        // Blorb "AIFF Sounds" — nested FORM needs full IFF wrapper
        if (chunk.InnerFormType != null)
            return ReconstructForm(chunk);

        return chunk.Data;
    }

    /// <summary>
    /// Returns the chunk type for the resource (e.g. "PNG ", "JPEG",
    /// "AIFF", "OGGV", "MOD ", "ZCOD", "TEXT", "BINA").
    /// For nested FORMs, returns the inner form type.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No resource with that usage/number.</exception>
    public string GetResourceType(string usage, int number)
        => _resourceTypes[(usage, number)];

    private static BlorbReader LoadFromForm(IffForm form)
    {
        if (form.FormType != "IFRS")
            throw new InvalidDataException(
                $"Expected FORM type 'IFRS', got '{form.FormType}'.");

        if (form.Chunks.Count == 0 || form.Chunks[0].TypeId != "RIdx")
            throw new InvalidDataException(
                "First chunk must be 'RIdx' (resource index).");

        var warnings = new List<string>(form.Warnings);
        var reader = new BlorbReader(form, warnings);

        var offsetToChunk = BuildOffsetMap(form);
        reader.ParseRIdx(form.Chunks[0], offsetToChunk);
        reader.ParsePalette(form);
        reader.ParseResolution(form);
        reader.ParseMetadata(form);

        return reader;
    }

    /// <summary>
    /// Builds a map from file offset → chunk, used to resolve
    /// RIdx entries. Offset is from start of file (byte 0 = 'F' in FORM).
    /// </summary>
    private static Dictionary<int, IffChunk> BuildOffsetMap(IffForm form)
    {
        var map = new Dictionary<int, IffChunk>();
        int offset = 12; // FORM(4) + length(4) + type(4)

        foreach (var chunk in form.Chunks)
        {
            map[offset] = chunk;
            uint chunkLen = chunk.Length;
            offset += 8 + (int)chunkLen;
            // IFF padding for odd-length chunks
            if (chunkLen % 2 != 0)
                offset++;
        }

        return map;
    }

    /// <summary>
    /// Parses the RIdx chunk and populates the resource index.
    /// Blorb "Contents of the Resource Index Chunk" — 4-byte count + 12-byte entries.
    /// </summary>
    private void ParseRIdx(IffChunk ridx, Dictionary<int, IffChunk> offsetToChunk)
    {
        byte[] data = ridx.Data;
        if (data.Length < 4)
            throw new InvalidDataException("RIdx chunk too short.");

        int count = ReadInt32BE(data, 0);
        if (data.Length < 4 + count * 12)
            throw new InvalidDataException(
                $"RIdx chunk too short for {count} entries.");

        var seen = new HashSet<(string, int)>();

        for (int i = 0; i < count; i++)
        {
            int entryOffset = 4 + i * 12;
            string usage = System.Text.Encoding.ASCII.GetString(data, entryOffset, 4);
            int number = ReadInt32BE(data, entryOffset + 4);
            int start = ReadInt32BE(data, entryOffset + 8);

            var key = (usage, number);

            if (!seen.Add(key))
            {
                _warnings.Add(
                    $"Duplicate RIdx entry ({usage}, {number}); using first.");
                continue;
            }

            if (!offsetToChunk.TryGetValue(start, out var chunk))
            {
                _warnings.Add(
                    $"RIdx entry ({usage}, {number}) points to offset {start} " +
                    "which does not match any chunk.");
                continue;
            }

            _resources[key] = chunk;

            // Blorb "AIFF Sounds" — nested FORM uses inner form type
            _resourceTypes[key] = chunk.InnerFormType ?? chunk.TypeId;
        }

        PictureCount = _resources.Keys.Count(k => k.Usage == BlorbUsage.Picture);
        SoundCount = _resources.Keys.Count(k => k.Usage == BlorbUsage.Sound);
    }

    /// <summary>
    /// Parses the optional 'Plte' (color palette) chunk.
    /// Blorb "The Color Palette Chunk" — length 1 = direct color,
    /// positive multiple of 3 = RGB list.
    /// </summary>
    private void ParsePalette(IffForm form)
    {
        var plte = form.GetChunk("Plte");
        if (plte == null) return;

        byte[] data = plte.Data;

        if (data.Length == 1)
        {
            if (data[0] != 16 && data[0] != 32)
            {
                _warnings.Add(
                    $"Plte direct-color value {data[0]} is not 16 or 32.");
                return;
            }
            Palette = new BlorbPalette(data[0]);
        }
        else if (data.Length > 0 && data.Length % 3 == 0)
        {
            int entryCount = data.Length / 3;
            var colors = new (byte R, byte G, byte B)[entryCount];
            for (int i = 0; i < entryCount; i++)
                colors[i] = (data[i * 3], data[i * 3 + 1], data[i * 3 + 2]);
            Palette = new BlorbPalette(colors);
        }
        else
        {
            _warnings.Add(
                $"Plte chunk has illegal length {data.Length} " +
                "(expected 1 or a positive multiple of 3).");
        }
    }

    /// <summary>
    /// Parses the optional 'Reso' (resolution) chunk for image scaling.
    /// Blorb "The Resolution Chunk" — 24-byte header + 28 bytes per entry.
    /// </summary>
    private void ParseResolution(IffForm form)
    {
        var reso = form.GetChunk("Reso");
        if (reso == null) return;

        byte[] data = reso.Data;
        if (data.Length < 24)
        {
            _warnings.Add($"Reso chunk too short ({data.Length} bytes, need 24).");
            return;
        }

        int px = ReadInt32BE(data, 0);
        int py = ReadInt32BE(data, 4);
        int minx = ReadInt32BE(data, 8);
        int miny = ReadInt32BE(data, 12);
        int maxx = ReadInt32BE(data, 16);
        int maxy = ReadInt32BE(data, 20);

        if (px <= 0 || py <= 0)
        {
            _warnings.Add($"Reso standard size must be non-zero (got {px}×{py}).");
            return;
        }

        var entries = new Dictionary<int, ImageScalingEntry>();
        int offset = 24;
        while (offset + 28 <= data.Length)
        {
            int number = ReadInt32BE(data, offset);
            entries[number] = new ImageScalingEntry
            {
                StandardNum = ReadInt32BE(data, offset + 4),
                StandardDen = ReadInt32BE(data, offset + 8),
                MinNum = ReadInt32BE(data, offset + 12),
                MinDen = ReadInt32BE(data, offset + 16),
                MaxNum = ReadInt32BE(data, offset + 20),
                MaxDen = ReadInt32BE(data, offset + 24),
            };
            offset += 28;
        }

        Resolution = new ResolutionInfo
        {
            StandardWidth = px,
            StandardHeight = py,
            MinWidth = minx,
            MinHeight = miny,
            MaxWidth = maxx,
            MaxHeight = maxy,
            Entries = entries,
        };
    }

    /// <summary>
    /// Parses optional metadata chunks: IFhd, RelN, Fspc, RDes, IFmd, AUTH, (c), ANNO.
    /// </summary>
    private void ParseMetadata(IffForm form)
    {
        // Blorb "The Game Identifier Chunk"
        var ifhd = form.GetChunk("IFhd");
        if (ifhd != null)
            GameIdentifier = ifhd.Data;

        // Blorb "The Release Number Chunk" — 2-byte big-endian value
        var reln = form.GetChunk("RelN");
        if (reln != null && reln.Data.Length >= 2)
            ReleaseNumber = (reln.Data[0] << 8) | reln.Data[1];

        // Blorb "The Frontispiece Chunk" — 4-byte picture number
        var fspc = form.GetChunk("Fspc");
        if (fspc != null && fspc.Data.Length >= 4)
            FrontispiecePicture = ReadInt32BE(fspc.Data, 0);

        // Blorb "Metadata" — UTF-8 XML
        var ifmd = form.GetChunk("IFmd");
        if (ifmd != null)
            MetadataXml = System.Text.Encoding.UTF8.GetString(ifmd.Data);

        // AUTH, (c), ANNO
        var auth = form.GetChunk("AUTH");
        if (auth != null)
            Author = auth.GetText();

        var copy = form.GetChunk("(c) ");
        if (copy != null)
            Copyright = copy.GetText();

        var annos = form.GetChunks("ANNO").ToList();
        if (annos.Count > 0)
            Annotations = annos.Select(a => a.GetText()).ToList();

        // Blorb "The Resource Description Chunk"
        var rdes = form.GetChunk("RDes");
        if (rdes != null)
            ParseResourceDescriptions(rdes);
    }

    /// <summary>
    /// Parses the 'RDes' chunk: count + variable-length entries.
    /// Blorb "The Resource Description Chunk".
    /// </summary>
    private void ParseResourceDescriptions(IffChunk rdes)
    {
        byte[] data = rdes.Data;
        if (data.Length < 4) return;

        int count = ReadInt32BE(data, 0);
        int offset = 4;

        for (int i = 0; i < count; i++)
        {
            if (offset + 12 > data.Length) break;

            string usage = System.Text.Encoding.ASCII.GetString(data, offset, 4);
            int number = ReadInt32BE(data, offset + 4);
            int textLen = ReadInt32BE(data, offset + 8);
            offset += 12;

            if (offset + textLen > data.Length) break;

            string text = System.Text.Encoding.UTF8.GetString(data, offset, textLen);
            offset += textLen;

            _resourceDescriptions[(usage, number)] = text;
        }
    }

    /// <summary>
    /// Validates the Blorb's IFhd chunk against a loaded story's memory.
    /// Compares release number, serial number, and checksum.
    /// Blorb "The Game Identifier Chunk" — same format as Quetzal S5.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the IFhd does not match the loaded story.
    /// </exception>
    public void ValidateIFhd(Memory memory)
    {
        if (GameIdentifier == null || GameIdentifier.Length < 13)
            return;

        byte[] ifhd = GameIdentifier;

        ushort release = (ushort)((ifhd[0] << 8) | ifhd[1]);
        ushort expectedRelease = memory.ReadWord(0x02);
        if (release != expectedRelease)
            throw new InvalidOperationException(
                $"Blorb IFhd release mismatch: Blorb has {release}, story has {expectedRelease}.");

        for (int i = 0; i < 6; i++)
        {
            if (ifhd[2 + i] != memory.OriginalBytes[0x12 + i])
                throw new InvalidOperationException(
                    "Blorb IFhd serial number mismatch.");
        }

        ushort checksum = (ushort)((ifhd[8] << 8) | ifhd[9]);
        ushort expectedChecksum = memory.ReadWord(0x1C);
        if (expectedChecksum != 0 && checksum != expectedChecksum)
            throw new InvalidOperationException(
                $"Blorb IFhd checksum mismatch: Blorb has ${checksum:X4}, story has ${expectedChecksum:X4}.");
    }

    /// <summary>
    /// Reconstructs a full IFF FORM file from a nested FORM chunk.
    /// Used to return usable AIFF data from GetResource.
    /// </summary>
    private static byte[] ReconstructForm(IffChunk chunk)
    {
        string innerType = chunk.InnerFormType!;
        int totalLength = 12 + chunk.Data.Length;
        byte[] result = new byte[totalLength];

        // FORM header
        result[0] = (byte)'F'; result[1] = (byte)'O';
        result[2] = (byte)'R'; result[3] = (byte)'M';

        // Content length = inner type (4) + data
        uint contentLen = (uint)(4 + chunk.Data.Length);
        result[4] = (byte)(contentLen >> 24);
        result[5] = (byte)(contentLen >> 16);
        result[6] = (byte)(contentLen >> 8);
        result[7] = (byte)contentLen;

        // Inner form type
        byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(innerType);
        Array.Copy(typeBytes, 0, result, 8, 4);

        // Data
        Array.Copy(chunk.Data, 0, result, 12, chunk.Data.Length);

        return result;
    }

    private static int ReadInt32BE(byte[] data, int offset)
        => (data[offset] << 24) | (data[offset + 1] << 16) |
           (data[offset + 2] << 8) | data[offset + 3];
}
