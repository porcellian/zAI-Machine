namespace ZMachine.Core;

/// <summary>
/// Writes IFF (Interchange File Format) files. Produces FORM containers
/// with properly padded chunks and auto-calculated lengths.
/// </summary>
/// <remarks>
/// Quetzal S8 — IFF format: FORM header, chunk layout, odd-length padding.
/// Blorb "The IFF Format" — chunk length excludes 8-byte header.
/// </remarks>
public static class IffWriter
{
    /// <summary>
    /// Writes a complete IFF FORM to the given stream.
    /// </summary>
    /// <param name="stream">A writable stream.</param>
    /// <param name="formType">The 4-character FORM sub-type (e.g. "IFZS", "IFRS").</param>
    /// <param name="chunks">The chunks to include in the FORM.</param>
    public static void Write(Stream stream, string formType, IEnumerable<IffChunk> chunks)
    {
        if (formType.Length != 4)
            throw new ArgumentException("Form type must be exactly 4 characters.", nameof(formType));

        var chunkList = chunks as IList<IffChunk> ?? chunks.ToList();

        // Quetzal S8.5 — FORM length = 4 (sub-type) + all chunk sizes
        uint formDataLength = 4;
        foreach (var chunk in chunkList)
            formDataLength += ChunkSizeOnDisk(chunk);

        using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);

        WriteTypeId(writer, "FORM");
        WriteUInt32BE(writer, formDataLength);
        WriteTypeId(writer, formType);

        foreach (var chunk in chunkList)
            WriteChunk(writer, chunk);
    }

    /// <summary>
    /// Writes a complete IFF FORM to a byte array.
    /// </summary>
    public static byte[] WriteToArray(string formType, IEnumerable<IffChunk> chunks)
    {
        using var stream = new MemoryStream();
        Write(stream, formType, chunks);
        return stream.ToArray();
    }

    private static void WriteChunk(BinaryWriter writer, IffChunk chunk)
    {
        WriteTypeId(writer, chunk.TypeId);

        // Blorb "AIFF Sounds" — nested FORM includes inner type in data
        uint dataLength = chunk.InnerFormType != null
            ? (uint)(chunk.Data.Length + 4)
            : (uint)chunk.Data.Length;

        WriteUInt32BE(writer, dataLength);

        if (chunk.InnerFormType != null)
            WriteTypeId(writer, chunk.InnerFormType);

        writer.Write(chunk.Data);

        // Quetzal S8.4.1 — pad byte for odd-length chunks
        if (dataLength % 2 != 0)
            writer.Write((byte)0);
    }

    /// <summary>
    /// Returns the total bytes this chunk occupies on disk,
    /// including header and optional pad byte.
    /// </summary>
    private static uint ChunkSizeOnDisk(IffChunk chunk)
    {
        uint dataLength = chunk.InnerFormType != null
            ? (uint)(chunk.Data.Length + 4)
            : (uint)chunk.Data.Length;

        // 4 (type) + 4 (length) + data + optional pad
        uint size = 8 + dataLength;
        if (dataLength % 2 != 0)
            size++;
        return size;
    }

    private static void WriteTypeId(BinaryWriter writer, string id)
    {
        writer.Write(System.Text.Encoding.ASCII.GetBytes(id));
    }

    /// <summary>Writes a big-endian 32-bit unsigned integer.</summary>
    private static void WriteUInt32BE(BinaryWriter writer, uint value)
    {
        writer.Write((byte)(value >> 24));
        writer.Write((byte)(value >> 16));
        writer.Write((byte)(value >> 8));
        writer.Write((byte)value);
    }
}
