namespace ZMachine.Core;

/// <summary>
/// Reads a Quetzal 1.4 save file (IFF FORM 'IFZS') and restores
/// Z-Machine state: dynamic memory, call stack, and program counter.
/// </summary>
/// <remarks>
/// Quetzal S3–S5 — Memory restore (CMem/UMem), stack frames (Stks), IFhd validation.
/// Quetzal S7.8–S7.17 — IntD chunks are preserved but not required.
/// </remarks>
public static class QuetzalReader
{
    /// <summary>
    /// Restores interpreter state from a Quetzal save file.
    /// </summary>
    /// <param name="stream">A readable stream containing Quetzal data.</param>
    /// <param name="machine">The interpreter to restore into.</param>
    /// <returns>The restored PC value from the IFhd chunk.</returns>
    /// <exception cref="QuetzalException">
    /// The save file is invalid or doesn't match the loaded story.
    /// </exception>
    public static int Restore(Stream stream, Interpreter machine)
    {
        var form = IffReader.Parse(stream);
        return RestoreFromForm(form, machine);
    }

    /// <summary>
    /// Restores from a byte array (convenience for testing).
    /// </summary>
    public static int Restore(byte[] data, Interpreter machine)
    {
        var form = IffReader.Parse(data);
        return RestoreFromForm(form, machine);
    }

    private static int RestoreFromForm(IffForm form, Interpreter machine)
    {
        if (form.FormType != "IFZS")
            throw new QuetzalException(
                $"Expected FORM type 'IFZS', got '{form.FormType}'.");

        // Quetzal S5.4 — IFhd is required
        var ifhd = form.GetChunk("IFhd")
            ?? throw new QuetzalException("Missing required 'IFhd' chunk.");

        int savePC = ValidateIFhd(ifhd, machine.Memory);

        // Quetzal S8.10 — CMem or UMem is required
        var cmem = form.GetChunk("CMem");
        var umem = form.GetChunk("UMem");
        if (cmem == null && umem == null)
            throw new QuetzalException(
                "Missing required 'CMem' or 'UMem' chunk.");

        // Quetzal S4 — Stks is required
        var stks = form.GetChunk("Stks")
            ?? throw new QuetzalException("Missing required 'Stks' chunk.");

        // Restore memory first, then stack
        if (cmem != null)
            RestoreCMem(cmem, machine.Memory);
        else
            RestoreUMem(umem!, machine.Memory);

        int version = machine.Memory.ReadByte(0x00);
        RestoreStks(stks, machine.State, version);

        // Set the restored PC
        machine.State.PC = savePC;

        return savePC;
    }

    /// <summary>
    /// Validates the IFhd chunk against the loaded story file.
    /// Quetzal S5.3 — compare release, serial, checksum.
    /// </summary>
    /// <returns>The saved PC from the chunk.</returns>
    private static int ValidateIFhd(IffChunk ifhd, Memory memory)
    {
        if (ifhd.Data.Length < 13)
            throw new QuetzalException(
                $"IFhd chunk too short: {ifhd.Data.Length} bytes, expected 13.");

        // Quetzal S5.4.3 — release number
        ushort release = (ushort)(ifhd.Data[0] << 8 | ifhd.Data[1]);
        ushort expectedRelease = memory.ReadWord(0x02);
        if (release != expectedRelease)
            throw new QuetzalException(
                $"Release number mismatch: save has {release}, story has {expectedRelease}.");

        // Quetzal S5.4.4 — serial number (6 bytes)
        for (int i = 0; i < 6; i++)
        {
            if (ifhd.Data[2 + i] != memory.OriginalBytes[0x12 + i])
                throw new QuetzalException("Serial number mismatch.");
        }

        // Quetzal S5.4.5 — checksum
        ushort checksum = (ushort)(ifhd.Data[8] << 8 | ifhd.Data[9]);
        ushort expectedChecksum = memory.ReadWord(0x1C);
        if (expectedChecksum == 0)
        {
            // Quetzal S5.5 — calculate if story has none
            uint sum = 0;
            for (int i = 0x40; i < memory.OriginalBytes.Length; i++)
                sum += memory.OriginalBytes[i];
            expectedChecksum = (ushort)(sum & 0xFFFF);
        }
        if (checksum != expectedChecksum)
            throw new QuetzalException(
                $"Checksum mismatch: save has ${checksum:X4}, story has ${expectedChecksum:X4}.");

        // Quetzal S5.4.6 — 3-byte PC
        return (ifhd.Data[10] << 16) | (ifhd.Data[11] << 8) | ifhd.Data[12];
    }

    /// <summary>
    /// Restores dynamic memory from a CMem (XOR-compressed) chunk.
    /// Quetzal S3.2–S3.7.
    /// </summary>
    private static void RestoreCMem(IffChunk cmem, Memory memory)
    {
        // Start by restoring original dynamic memory
        memory.RestoreDynamicMemory();

        int dynamicLen = memory.StaticBase;
        byte[] compressed = cmem.Data;
        int srcIdx = 0;
        int dstIdx = 0;

        while (srcIdx < compressed.Length)
        {
            byte b = compressed[srcIdx++];

            if (b != 0)
            {
                // Quetzal S3.5 — decoded data larger than dynamic memory
                if (dstIdx >= dynamicLen)
                    throw new QuetzalException(
                        "CMem decoded data exceeds dynamic memory size.");

                // XOR with original (which is already restored)
                memory.WriteByte(dstIdx, (byte)(memory.ReadByte(dstIdx) ^ b));
                dstIdx++;
            }
            else
            {
                // Zero byte + length byte = run of n+1 zeros
                // Quetzal S3.5 — incomplete run
                if (srcIdx >= compressed.Length)
                    throw new QuetzalException(
                        "CMem ends with incomplete run (zero byte without length).");

                int runLen = compressed[srcIdx++] + 1;

                // Quetzal S3.5 — run exceeding dynamic memory
                if (dstIdx + runLen > dynamicLen)
                    throw new QuetzalException(
                        "CMem decoded data exceeds dynamic memory size.");

                // Zeros XOR'd with original = original, which is already there
                dstIdx += runLen;
            }
        }

        // Quetzal S3.4 — shorter decoded data means remaining is original
    }

    /// <summary>
    /// Restores dynamic memory from a UMem (uncompressed) chunk.
    /// Quetzal S3.8.
    /// </summary>
    private static void RestoreUMem(IffChunk umem, Memory memory)
    {
        int dynamicLen = memory.StaticBase;

        // Quetzal S3.6 — length must match exactly
        if (umem.Data.Length != dynamicLen)
            throw new QuetzalException(
                $"UMem length {umem.Data.Length} does not match dynamic memory size {dynamicLen}.");

        for (int i = 0; i < dynamicLen; i++)
            memory.WriteByte(i, umem.Data[i]);
    }

    /// <summary>
    /// Restores the call stack from a Stks chunk.
    /// Quetzal S4.
    /// </summary>
    private static void RestoreStks(IffChunk stks, MachineState state, int version)
    {
        state.CallStack.Clear();

        byte[] data = stks.Data;
        int offset = 0;
        bool firstFrame = true;

        while (offset + 8 <= data.Length)
        {
            // Quetzal S4.3.1 — 3-byte return PC
            int returnPC = (data[offset] << 16) | (data[offset + 1] << 8) | data[offset + 2];
            offset += 3;

            // Quetzal S4.3.2 — flags: 000pvvvv
            byte flags = data[offset++];
            bool discardResult = (flags & 0x10) != 0;
            int localCount = flags & 0x0F;

            // Quetzal S4.3.3 — store variable
            byte storeVar = data[offset++];

            // Quetzal S4.3.4 — argument flags
            byte argFlags = data[offset++];
            int argCount = 0;
            for (int i = 0; i < 7; i++)
            {
                if ((argFlags & (1 << i)) != 0)
                    argCount = i + 1;
            }

            // Quetzal S4.3.5 — eval stack word count
            if (offset + 2 > data.Length)
                throw new QuetzalException("Stks chunk truncated at eval count.");
            int evalCount = (data[offset] << 8) | data[offset + 1];
            offset += 2;

            // Quetzal S4.8 — stack overflow detection
            int frameDataSize = localCount * 2 + evalCount * 2;
            if (offset + frameDataSize > data.Length)
                throw new QuetzalException(
                    "Stks chunk truncated: frame data extends beyond chunk.");

            // Quetzal S4.11 — dummy frame for non-V6
            if (firstFrame && version != 6)
            {
                var dummyFrame = new CallFrame(0, 0, false, 0, 0);

                // Read eval stack (oldest first in file = push in order)
                for (int i = 0; i < evalCount; i++)
                {
                    ushort val = (ushort)((data[offset] << 8) | data[offset + 1]);
                    offset += 2;
                    dummyFrame.EvalStack.Push(val);
                }

                state.CallStack.PushFrame(dummyFrame);
                firstFrame = false;
                continue;
            }

            firstFrame = false;

            var frame = new CallFrame(returnPC, storeVar, discardResult,
                localCount, argCount);

            // Quetzal S4.3.6 — local variables
            for (int i = 1; i <= localCount; i++)
            {
                frame.Locals[i] = (ushort)((data[offset] << 8) | data[offset + 1]);
                offset += 2;
            }

            // Quetzal S4.3.7 — eval stack (oldest first in file)
            for (int i = 0; i < evalCount; i++)
            {
                ushort val = (ushort)((data[offset] << 8) | data[offset + 1]);
                offset += 2;
                frame.EvalStack.Push(val);
            }

            state.CallStack.PushFrame(frame);
        }
    }
}

/// <summary>
/// Exception thrown when a Quetzal save file is invalid or incompatible.
/// </summary>
public class QuetzalException : Exception
{
    /// <summary>Creates a QuetzalException with the given message.</summary>
    public QuetzalException(string message) : base(message) { }
}
