namespace ZMachine.Core;

/// <summary>
/// Represents a parsed IFF FORM — the top-level container in an IFF file.
/// Contains the form type (e.g. "IFZS" for Quetzal, "IFRS" for Blorb)
/// and all parsed chunks.
/// </summary>
/// <remarks>
/// Quetzal S8.5 — FORM structure: 'FORM' + length + sub-ID + chunks.
/// </remarks>
public class IffForm
{
    /// <summary>The 4-character FORM sub-type (e.g. "IFZS", "IFRS").</summary>
    public string FormType { get; }

    /// <summary>All chunks in the FORM, in file order.</summary>
    public IReadOnlyList<IffChunk> Chunks { get; }

    /// <summary>Warnings generated during parsing (e.g. duplicate chunks).</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>Creates a new IffForm.</summary>
    public IffForm(string formType, IReadOnlyList<IffChunk> chunks,
        IReadOnlyList<string>? warnings = null)
    {
        FormType = formType;
        Chunks = chunks;
        Warnings = warnings ?? [];
    }

    /// <summary>
    /// Returns the first chunk with the given type ID, or null.
    /// Quetzal S8.8 — use the first when only one is expected.
    /// </summary>
    public IffChunk? GetChunk(string typeId)
        => Chunks.FirstOrDefault(c => c.TypeId == typeId);

    /// <summary>Returns all chunks with the given type ID.</summary>
    public IEnumerable<IffChunk> GetChunks(string typeId)
        => Chunks.Where(c => c.TypeId == typeId);
}

/// <summary>
/// A single chunk in an IFF file. Contains a 4-character type ID,
/// raw data bytes, and an optional inner form type for nested FORMs.
/// </summary>
/// <remarks>
/// Quetzal S8.3 — chunk: ID (4 bytes) + length (4 bytes) + data.
/// Blorb "AIFF Sounds" — nested FORM has inner formtype.
/// </remarks>
public class IffChunk
{
    /// <summary>The 4-character chunk type ID (e.g. "IFhd", "CMem").</summary>
    public string TypeId { get; }

    /// <summary>
    /// The raw chunk data. For nested FORMs, this is the data after
    /// the inner form type (the sub-chunks), not including the
    /// FORM header or inner type ID.
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// The data length as reported in the chunk header.
    /// For nested FORMs, includes the 4-byte inner type ID.
    /// </summary>
    public uint Length => InnerFormType != null
        ? (uint)(Data.Length + 4)
        : (uint)Data.Length;

    /// <summary>
    /// For nested FORM chunks: the inner form type (e.g. "AIFF").
    /// Null for regular (non-FORM) chunks.
    /// </summary>
    public string? InnerFormType { get; }

    /// <summary>Creates a regular (non-FORM) chunk.</summary>
    public IffChunk(string typeId, byte[] data)
    {
        TypeId = typeId;
        Data = data;
        InnerFormType = null;
    }

    /// <summary>Creates a chunk with an optional inner form type (for nested FORMs).</summary>
    public IffChunk(string typeId, byte[] data, string? innerFormType)
    {
        TypeId = typeId;
        Data = data;
        InnerFormType = innerFormType;
    }

    /// <summary>
    /// Reads the data as ASCII text (for AUTH, ANNO, (c) chunks).
    /// Quetzal S7.2 — text chunks contain simple ASCII.
    /// </summary>
    public string GetText()
        => System.Text.Encoding.ASCII.GetString(Data);
}
