namespace ZMachine.Core;

/// <summary>
/// Reads IFF (Interchange File Format) files into an <see cref="IffForm"/>
/// structure. Handles both Quetzal (IFZS) and Blorb (IFRS) containers,
/// including nested FORM chunks (e.g. AIFF sounds inside Blorb).
/// </summary>
/// <remarks>
/// Quetzal S8 — IFF format basics: chunks, FORM, padding.
/// Blorb "The IFF Format" — FORM structure, chunk layout.
/// </remarks>
public static class IffReader
{
    /// <summary>
    /// Parses an IFF file from a stream and returns the top-level FORM.
    /// </summary>
    /// <param name="stream">A readable, seekable stream positioned at the start of the IFF data.</param>
    /// <returns>The parsed <see cref="IffForm"/>.</returns>
    /// <exception cref="InvalidDataException">The stream does not contain valid IFF data.</exception>
    public static IffForm Parse(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        return ParseForm(reader);
    }

    /// <summary>
    /// Parses an IFF file from a byte array.
    /// </summary>
    public static IffForm Parse(byte[] data)
    {
        using var stream = new MemoryStream(data);
        return Parse(stream);
    }

    private static IffForm ParseForm(BinaryReader reader)
    {
        string outerType = ReadTypeId(reader);
        if (outerType != "FORM")
            throw new InvalidDataException(
                $"Expected 'FORM' chunk, got '{outerType}'.");

        uint formLength = ReadUInt32BE(reader);
        long formDataStart = reader.BaseStream.Position;

        string formType = ReadTypeId(reader);

        var chunks = new List<IffChunk>();
        var seenTypes = new HashSet<string>();
        var warnings = new List<string>();

        // Quetzal S8.6 — chunks follow the form sub-ID
        long endPos = formDataStart + formLength;
        while (reader.BaseStream.Position < endPos)
        {
            long chunkStart = reader.BaseStream.Position;

            if (reader.BaseStream.Position + 8 > endPos)
                break;

            string chunkType = ReadTypeId(reader);
            uint chunkLength = ReadUInt32BE(reader);

            long dataStart = reader.BaseStream.Position;

            // Quetzal S8.8 — duplicate chunk warning
            if (!IsDuplicateAllowed(chunkType) && !seenTypes.Add(chunkType))
            {
                warnings.Add(
                    $"Duplicate '{chunkType}' chunk at offset {chunkStart}; ignoring.");

                // Skip the chunk data + padding
                SkipChunkData(reader, chunkLength, endPos);
                continue;
            }

            IffChunk chunk;

            // Blorb "AIFF Sounds" — nested FORM has sub-type
            if (chunkType == "FORM")
            {
                string innerFormType = ReadTypeId(reader);
                byte[] innerData = ReadBytes(reader, (int)(chunkLength - 4));
                chunk = new IffChunk(chunkType, innerData, innerFormType);
            }
            else
            {
                byte[] data = ReadBytes(reader, (int)chunkLength);
                chunk = new IffChunk(chunkType, data);
            }

            chunks.Add(chunk);

            // Quetzal S8.4.1 — skip padding byte for odd-length chunks
            long bytesRead = reader.BaseStream.Position - dataStart;
            if (chunkLength % 2 != 0 && reader.BaseStream.Position < endPos)
                reader.BaseStream.Position++;
        }

        return new IffForm(formType, chunks, warnings);
    }

    /// <summary>
    /// ANNO chunks may appear multiple times per Quetzal S7.5/S8.8.
    /// </summary>
    private static bool IsDuplicateAllowed(string chunkType)
        => chunkType == "ANNO";

    private static string ReadTypeId(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        if (bytes.Length < 4)
            throw new InvalidDataException("Unexpected end of IFF data.");
        return System.Text.Encoding.ASCII.GetString(bytes);
    }

    /// <summary>Reads a big-endian 32-bit unsigned integer.</summary>
    private static uint ReadUInt32BE(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        if (bytes.Length < 4)
            throw new InvalidDataException("Unexpected end of IFF data.");
        return (uint)(bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3]);
    }

    private static byte[] ReadBytes(BinaryReader reader, int count)
    {
        if (count <= 0) return [];
        byte[] data = reader.ReadBytes(count);
        if (data.Length < count)
            throw new InvalidDataException(
                $"Expected {count} bytes, got {data.Length}.");
        return data;
    }

    private static void SkipChunkData(BinaryReader reader, uint chunkLength, long endPos)
    {
        long skipTarget = reader.BaseStream.Position + chunkLength;
        // Pad byte for odd length
        if (chunkLength % 2 != 0)
            skipTarget++;
        if (skipTarget > endPos)
            skipTarget = endPos;
        reader.BaseStream.Position = skipTarget;
    }
}
