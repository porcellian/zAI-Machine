namespace ZMachine.Core;

/// <summary>
/// Writes Z-Machine state to a Quetzal 1.4 save file (IFF FORM 'IFZS').
/// Produces IFhd, CMem (or UMem), and Stks chunks in spec order.
/// </summary>
/// <remarks>
/// Quetzal S2–S5 — Overall structure, memory compression, stack frames, IFhd.
/// Quetzal S7 — Optional AUTH/ANNO chunks.
/// </remarks>
public static class QuetzalWriter
{
    /// <summary>
    /// Saves the current interpreter state to a Quetzal file.
    /// </summary>
    /// <param name="stream">A writable stream for the save file.</param>
    /// <param name="machine">The interpreter whose state to save.</param>
    /// <param name="savePC">
    /// The PC to store in IFhd. For V1–3 this points to the branch data
    /// after the SAVE instruction; for V4+ it points to the store byte.
    /// Quetzal S5.8.
    /// </param>
    /// <param name="useCompression">
    /// If true (default), writes a CMem chunk with XOR+RLE compression.
    /// If false, writes an uncompressed UMem chunk. Quetzal S3.6.
    /// </param>
    public static void Save(Stream stream, Interpreter machine, int savePC,
        bool useCompression = true)
    {
        var memory = machine.Memory;
        int version = memory.ReadByte(0x00);

        var chunks = new List<IffChunk>();

        // Quetzal S5.4 — IFhd must come before CMem/UMem and Stks
        chunks.Add(BuildIFhd(memory, savePC));

        // Quetzal S3 — dynamic memory
        if (useCompression)
            chunks.Add(BuildCMem(memory));
        else
            chunks.Add(BuildUMem(memory));

        // Quetzal S4 — stack frames
        chunks.Add(BuildStks(machine.State, version));

        IffWriter.Write(stream, "IFZS", chunks);
    }

    /// <summary>
    /// Saves to a byte array (convenience for testing).
    /// </summary>
    public static byte[] SaveToArray(Interpreter machine, int savePC,
        bool useCompression = true)
    {
        using var ms = new MemoryStream();
        Save(ms, machine, savePC, useCompression);
        return ms.ToArray();
    }

    /// <summary>
    /// Builds the IFhd chunk (13 bytes): release, serial, checksum, PC.
    /// Quetzal S5.4.
    /// </summary>
    private static IffChunk BuildIFhd(Memory memory, int savePC)
    {
        var data = new byte[13];

        // Quetzal S5.4.3 — release number from header $02
        data[0] = memory.OriginalBytes[0x02];
        data[1] = memory.OriginalBytes[0x03];

        // Quetzal S5.4.4 — serial number from header $12 (6 bytes)
        for (int i = 0; i < 6; i++)
            data[2 + i] = memory.OriginalBytes[0x12 + i];

        // Quetzal S5.4.5 — checksum from header $1C
        ushort checksum = (ushort)(memory.OriginalBytes[0x1C] << 8 |
                                   memory.OriginalBytes[0x1D]);

        // Quetzal S5.5 — calculate checksum if not present in header
        if (checksum == 0)
            checksum = CalculateChecksum(memory.OriginalBytes);

        data[8] = (byte)(checksum >> 8);
        data[9] = (byte)(checksum & 0xFF);

        // Quetzal S5.4.6 — PC as 3-byte big-endian
        data[10] = (byte)((savePC >> 16) & 0xFF);
        data[11] = (byte)((savePC >> 8) & 0xFF);
        data[12] = (byte)(savePC & 0xFF);

        return new IffChunk("IFhd", data);
    }

    /// <summary>
    /// Builds the CMem chunk with XOR + run-length compression.
    /// Quetzal S3.2–S3.7.
    /// </summary>
    private static IffChunk BuildCMem(Memory memory)
    {
        int dynamicLen = memory.StaticBase;
        var compressed = new List<byte>();

        // Quetzal S3.2 — XOR current with original
        int runLength = 0;
        for (int i = 0; i < dynamicLen; i++)
        {
            byte xored = (byte)(memory.ReadByte(i) ^ memory.OriginalBytes[i]);

            if (xored == 0)
            {
                runLength++;
            }
            else
            {
                // Flush any pending zero run
                FlushZeroRun(compressed, runLength);
                runLength = 0;
                compressed.Add(xored);
            }
        }

        // Quetzal S3.4 — trailing zeros can be omitted

        return new IffChunk("CMem", compressed.ToArray());
    }

    /// <summary>
    /// Encodes a run of zero bytes as zero+count pairs.
    /// Each pair encodes count+1 zeros (max 256 zeros per pair).
    /// </summary>
    private static void FlushZeroRun(List<byte> output, int count)
    {
        while (count > 0)
        {
            int batch = Math.Min(count, 256);
            output.Add(0x00);
            output.Add((byte)(batch - 1));
            count -= batch;
        }
    }

    /// <summary>
    /// Builds the UMem chunk (uncompressed dynamic memory dump).
    /// Quetzal S3.8.
    /// </summary>
    private static IffChunk BuildUMem(Memory memory)
    {
        int dynamicLen = memory.StaticBase;
        var data = new byte[dynamicLen];
        for (int i = 0; i < dynamicLen; i++)
            data[i] = memory.ReadByte(i);

        return new IffChunk("UMem", data);
    }

    /// <summary>
    /// Builds the Stks chunk with all stack frames, oldest first.
    /// Quetzal S4.
    /// </summary>
    private static IffChunk BuildStks(MachineState state, int version)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        var frames = state.CallStack.GetFramesBottomUp().ToList();

        if (version != 6 && frames.Count > 0)
        {
            // Quetzal S4.11 — non-V6: first frame is the dummy frame
            var bottomFrame = frames[0];
            WriteDummyFrame(w, bottomFrame);

            // Write remaining frames normally
            for (int i = 1; i < frames.Count; i++)
                WriteFrame(w, frames[i]);
        }
        else
        {
            foreach (var frame in frames)
                WriteFrame(w, frame);
        }

        return new IffChunk("Stks", ms.ToArray());
    }

    /// <summary>
    /// Writes the dummy frame for non-V6 games.
    /// Quetzal S4.11 — all fields zero except eval stack.
    /// </summary>
    private static void WriteDummyFrame(BinaryWriter w, CallFrame frame)
    {
        // 3-byte return PC = 0
        w.Write((byte)0);
        w.Write((byte)0);
        w.Write((byte)0);

        // Flags = 0 (no locals)
        w.Write((byte)0);

        // Store variable = 0
        w.Write((byte)0);

        // Argument flags = 0
        w.Write((byte)0);

        // Eval stack count
        var evalStack = frame.EvalStack.ToArray();
        WriteWord(w, (ushort)evalStack.Length);

        // No locals for dummy frame

        // Eval stack values (oldest first = reverse of stack order)
        for (int i = evalStack.Length - 1; i >= 0; i--)
            WriteWord(w, evalStack[i]);
    }

    /// <summary>
    /// Writes a single stack frame in Quetzal format.
    /// Quetzal S4.3.
    /// </summary>
    private static void WriteFrame(BinaryWriter w, CallFrame frame)
    {
        // Quetzal S4.3.1 — 3-byte return PC
        w.Write((byte)((frame.ReturnPC >> 16) & 0xFF));
        w.Write((byte)((frame.ReturnPC >> 8) & 0xFF));
        w.Write((byte)(frame.ReturnPC & 0xFF));

        // Quetzal S4.3.2 — flags: 000pvvvv
        // p = discard result, vvvv = local count (0-15)
        byte flags = (byte)(frame.LocalCount & 0x0F);
        if (frame.DiscardResult)
            flags |= 0x10;
        w.Write(flags);

        // Quetzal S4.3.3 — store variable
        w.Write(frame.StoreVariable);

        // Quetzal S4.3.4 — argument flags: 0gfedcba
        byte argFlags = 0;
        for (int i = 0; i < Math.Min(frame.ArgumentCount, 7); i++)
            argFlags |= (byte)(1 << i);
        w.Write(argFlags);

        // Quetzal S4.3.5 — eval stack word count
        var evalStack = frame.EvalStack.ToArray();
        WriteWord(w, (ushort)evalStack.Length);

        // Quetzal S4.3.6 — local variables (1-indexed in frame)
        for (int i = 1; i <= frame.LocalCount; i++)
            WriteWord(w, frame.Locals[i]);

        // Quetzal S4.3.7 — eval stack (oldest first)
        for (int i = evalStack.Length - 1; i >= 0; i--)
            WriteWord(w, evalStack[i]);
    }

    private static void WriteWord(BinaryWriter w, ushort value)
    {
        w.Write((byte)(value >> 8));
        w.Write((byte)(value & 0xFF));
    }

    /// <summary>
    /// Calculates the story file checksum for games without one.
    /// Quetzal S5.5 — sum of all bytes from $40 onwards, mod 65536.
    /// </summary>
    private static ushort CalculateChecksum(byte[] storyData)
    {
        uint sum = 0;
        for (int i = 0x40; i < storyData.Length; i++)
            sum += storyData[i];
        return (ushort)(sum & 0xFFFF);
    }
}
