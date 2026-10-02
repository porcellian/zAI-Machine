using ZMachine.Core;
using ZMachine.Tests.Harness;

namespace ZMachine.Tests;

/// <summary>
/// Executes the V6-only opcodes added for Shogun against a tiny hand-built
/// version 6 story: @push_stack, @pop_stack, V6 @pull with a user stack,
/// @print_form, @make_menu and @mouse_window, plus the ZSCII 11 sentence
/// space. Each test assembles a few instructions into the main routine and
/// runs them until @quit.
/// </summary>
public class V6StackAndFormOpcodeTests
{
    private const int GlobalsAddr = 0x100;
    private const int UserStackAddr = 0x200;
    private const int TableAddr = 0x300;
    private const int MainAddr = 0x900;

    /// <summary>
    /// Builds a minimal V6 story whose main routine (no locals) contains the
    /// given code, followed by @quit. Routine/string offsets are zero, so the
    /// packed main address is simply MainAddr / 4.
    /// </summary>
    private static byte[] BuildStory(byte[] code, Action<byte[]>? setup = null)
    {
        var story = new byte[0x1000];
        story[0x00] = 6;                         // ZSpec S11 — version
        WriteWord(story, 0x04, 0x800);           // high memory base
        WriteWord(story, 0x06, MainAddr / 4);    // V6: packed address of main routine
        WriteWord(story, 0x08, 0x600);           // dictionary
        WriteWord(story, 0x0A, 0x400);           // object table
        WriteWord(story, 0x0C, GlobalsAddr);     // globals
        WriteWord(story, 0x0E, 0x800);           // static memory base
        WriteWord(story, 0x1A, story.Length / 8); // file length (V6 divides by 8)

        // Empty dictionary: no separators, 4-byte entries, zero entries.
        story[0x600] = 0;
        story[0x601] = 4;
        WriteWord(story, 0x602, 0);

        story[MainAddr] = 0; // no locals
        code.CopyTo(story, MainAddr + 1);
        story[MainAddr + 1 + code.Length] = 0xBA; // quit

        setup?.Invoke(story);
        return story;
    }

    private static void WriteWord(byte[] data, int addr, int value)
    {
        data[addr] = (byte)(value >> 8);
        data[addr + 1] = (byte)value;
    }

    private static (Interpreter Machine, CaptureScreen Screen) Run(byte[] story)
    {
        var screen = new CaptureScreen();
        var machine = new Interpreter();
        machine.Load(story, new ScriptedInputStream(Array.Empty<string>()), screen);
        for (int i = 0; i < 1000 && machine.Running; i++)
            machine.Step();
        Assert.False(machine.Running, "story did not reach @quit");
        return (machine, screen);
    }

    // "store G00 #01" (2OP:13, small/small) — marks that a branch fell through.
    private static readonly byte[] MarkG00 = [0x0D, 0x10, 0x01];

    /// <summary>
    /// ZSpec S15 — @push_stack writes into the slot indexed by the free count,
    /// decrements the count and branches on success.
    /// </summary>
    [Fact]
    public void PushStack_WithFreeSlot_StoresValueAndBranches()
    {
        // EXT:24 push_stack #1234 #0200 ?(skip the marker)
        byte[] code = [0xBE, 0x18, 0x0F, 0x12, 0x34, 0x02, 0x00, 0xC5, .. MarkG00];
        var (m, _) = Run(BuildStory(code, s => WriteWord(s, UserStackAddr, 2)));

        Assert.Equal(0x1234, m.Memory.ReadWord(UserStackAddr + 4));
        Assert.Equal(1, m.Memory.ReadWord(UserStackAddr));
        Assert.Equal(0, m.Memory.ReadWord(GlobalsAddr)); // branch taken
    }

    /// <summary>
    /// ZSpec S15 — a full user stack (no free slots) is left untouched and
    /// @push_stack does not branch.
    /// </summary>
    [Fact]
    public void PushStack_WhenFull_DoesNotBranch()
    {
        byte[] code = [0xBE, 0x18, 0x0F, 0x12, 0x34, 0x02, 0x00, 0xC5, .. MarkG00];
        var (m, _) = Run(BuildStory(code, s => WriteWord(s, UserStackAddr, 0)));

        Assert.Equal(0, m.Memory.ReadWord(UserStackAddr));
        Assert.Equal(1, m.Memory.ReadWord(GlobalsAddr)); // fell through
    }

    /// <summary>
    /// ZSpec S15 — V6 @pull with a user stack increments the free count and
    /// stores the value from the slot it now indexes.
    /// </summary>
    [Fact]
    public void PullV6_FromUserStack_StoresTopValue()
    {
        // VAR:9 pull #0200 -> G01
        byte[] code = [0xE9, 0x3F, 0x02, 0x00, 0x11];
        var (m, _) = Run(BuildStory(code, s =>
        {
            WriteWord(s, UserStackAddr, 1);
            WriteWord(s, UserStackAddr + 4, 0xBEEF);
        }));

        Assert.Equal(0xBEEF, m.Memory.ReadWord(GlobalsAddr + 2));
        Assert.Equal(2, m.Memory.ReadWord(UserStackAddr));
    }

    /// <summary>
    /// V6 @pull without operands pops the game stack into its store variable.
    /// </summary>
    [Fact]
    public void PullV6_FromGameStack_StoresPoppedValue()
    {
        // push #4321 ; pull -> G01
        byte[] code = [0xE8, 0x3F, 0x43, 0x21, 0xE9, 0xFF, 0x11];
        var (m, _) = Run(BuildStory(code));

        Assert.Equal(0x4321, m.Memory.ReadWord(GlobalsAddr + 2));
    }

    /// <summary>
    /// ZSpec S15 — @pop_stack with a user stack frees that many slots.
    /// </summary>
    [Fact]
    public void PopStack_UserStack_AddsToFreeCount()
    {
        // EXT:21 pop_stack #02 #0200
        byte[] code = [0xBE, 0x15, 0x4F, 0x02, 0x02, 0x00];
        var (m, _) = Run(BuildStory(code, s => WriteWord(s, UserStackAddr, 3)));

        Assert.Equal(5, m.Memory.ReadWord(UserStackAddr));
    }

    /// <summary>
    /// ZSpec S15 — @print_form prints each length-prefixed line, separated by
    /// new lines, until a zero length.
    /// </summary>
    [Fact]
    public void PrintForm_PrintsLinesFromTable()
    {
        // EXT:26 print_form #0300
        byte[] code = [0xBE, 0x1A, 0x3F, 0x03, 0x00];
        var (_, screen) = Run(BuildStory(code, s =>
        {
            WriteWord(s, TableAddr, 2);
            s[TableAddr + 2] = (byte)'h';
            s[TableAddr + 3] = (byte)'i';
            WriteWord(s, TableAddr + 4, 3);
            s[TableAddr + 6] = (byte)'y';
            s[TableAddr + 7] = (byte)'o';
            s[TableAddr + 8] = (byte)'u';
            WriteWord(s, TableAddr + 9, 0);
        }));

        Assert.Equal("hi\nyou", screen.Output.Replace("\r\n", "\n"));
    }

    /// <summary>
    /// @make_menu reports failure (no branch) because there is no menu bar,
    /// which makes games fall back to typed commands.
    /// </summary>
    [Fact]
    public void MakeMenu_DoesNotBranch()
    {
        // EXT:27 make_menu #0001 #0000 ?(skip the marker)
        byte[] code = [0xBE, 0x1B, 0x0F, 0x00, 0x01, 0x00, 0x00, 0xC5, .. MarkG00];
        var (m, _) = Run(BuildStory(code));

        Assert.Equal(1, m.Memory.ReadWord(GlobalsAddr));
    }

    /// <summary>
    /// Regression: Shogun's first instruction, @mouse_window -1 (EXT:23), used
    /// to be dispatched as @read_mouse and crash writing to $FFFF.
    /// </summary>
    [Fact]
    public void MouseWindow_MinusOne_RunsWithoutWritingMemory()
    {
        byte[] code = [0xBE, 0x17, 0x3F, 0xFF, 0xFF];
        var (m, _) = Run(BuildStory(code));

        Assert.False(m.Running);
    }

    /// <summary>
    /// ZSpec S3.8.2.1 — ZSCII 11 (sentence space) is shown as a space.
    /// </summary>
    [Fact]
    public void SentenceSpace_PrintsAsSpace()
    {
        // print_char #2E ; print_char #0B ; print_char #41
        byte[] code = [0xE5, 0x7F, 0x2E, 0xE5, 0x7F, 0x0B, 0xE5, 0x7F, 0x41];
        var (_, screen) = Run(BuildStory(code));

        Assert.Equal(". A", screen.Output);
    }
}
