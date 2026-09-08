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
