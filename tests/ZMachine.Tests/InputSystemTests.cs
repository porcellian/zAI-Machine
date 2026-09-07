namespace ZMachine.Tests;

using ZMachine.Core;
using ZMachine.IO;

/// <summary>
/// Tests for the input system: IInputStream implementations, InputStreamManager,
/// ReadHandler, and key mapping. Uses mock/file streams for deterministic testing.
/// </summary>
public class InputSystemTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string Zork1Path = Path.Combine(RepoRoot, "stories/zork1.z3");

    #region FileInputStream

    [Fact]
    public void FileInputStream_ReadLine_ReturnsFirstLine()
    {
        var stream = new FileInputStream(["open mailbox", "read leaflet"]);

        var (text, term) = stream.ReadLine(80);
        Assert.Equal("open mailbox", text);
        Assert.Equal(13, term);
    }

    [Fact]
    public void FileInputStream_ReadLine_Advances()
    {
        var stream = new FileInputStream(["first", "second"]);

        stream.ReadLine(80);
        var (text, _) = stream.ReadLine(80);
        Assert.Equal("second", text);
    }

    [Fact]
    public void FileInputStream_ReadLine_Exhausted()
    {
        var stream = new FileInputStream(["only"]);

        stream.ReadLine(80);
        Assert.False(stream.HasMore);

        var (text, term) = stream.ReadLine(80);
        Assert.Equal("", text);
        Assert.Equal(0, term);
    }

    [Fact]
    public void FileInputStream_ReadLine_TruncatesToMaxLength()
    {
        var stream = new FileInputStream(["abcdefghij"]);

        var (text, _) = stream.ReadLine(5);
        Assert.Equal("abcde", text);
    }

    [Fact]
    public void FileInputStream_ReadChar_ReturnsFirstChar()
    {
        var stream = new FileInputStream(["y"]);

        int ch = stream.ReadChar();
        Assert.Equal('y', ch);
    }

    [Fact]
    public void FileInputStream_HasMore()
    {
        var stream = new FileInputStream(["a", "b"]);

        Assert.True(stream.HasMore);
        stream.ReadLine(80);
        Assert.True(stream.HasMore);
        stream.ReadLine(80);
        Assert.False(stream.HasMore);
    }

    #endregion

    #region InputStreamManager

    [Fact]
    public void InputStreamManager_DefaultsToStream0()
    {
        var keyboard = new MockInputStream("hello");
        var mgr = new InputStreamManager(keyboard);

        Assert.Equal(0, mgr.ActiveStream);
        var (text, _) = mgr.ReadLine(80);
        Assert.Equal("hello", text);
    }

    [Fact]
    public void InputStreamManager_SwitchToStream1()
    {
        var keyboard = new MockInputStream("keyboard");
        var file = new FileInputStream(["file command"]);
        var mgr = new InputStreamManager(keyboard);

        mgr.SelectStream(1, file);
        Assert.Equal(1, mgr.ActiveStream);

        var (text, _) = mgr.ReadLine(80);
        Assert.Equal("file command", text);
    }

    [Fact]
    public void InputStreamManager_RevertsOnExhaustion()
    {
        var keyboard = new MockInputStream("keyboard");
        var file = new FileInputStream(["file"]);
        var mgr = new InputStreamManager(keyboard);

        mgr.SelectStream(1, file);
        mgr.ReadLine(80); // consumes "file"

        // File exhausted — should revert to stream 0.
        Assert.Equal(0, mgr.ActiveStream);
        var (text, _) = mgr.ReadLine(80);
        Assert.Equal("keyboard", text);
    }

    [Fact]
    public void InputStreamManager_SwitchBackToStream0()
    {
        var keyboard = new MockInputStream("keyboard");
        var file = new FileInputStream(["file1", "file2"]);
        var mgr = new InputStreamManager(keyboard);

        mgr.SelectStream(1, file);
        mgr.ReadLine(80);

        mgr.SelectStream(0);
        Assert.Equal(0, mgr.ActiveStream);

        var (text, _) = mgr.ReadLine(80);
        Assert.Equal("keyboard", text);
    }

    [Fact]
    public void InputStreamManager_ReadChar_FromFile()
    {
        var keyboard = new MockInputStream("k");
        var file = new FileInputStream(["y"]);
        var mgr = new InputStreamManager(keyboard);

        mgr.SelectStream(1, file);
        int ch = mgr.ReadChar();
        Assert.Equal('y', ch);
    }

    #endregion

    #region ReadHandler — V3 Text Buffer

    [Fact]
    public void ReadHandler_V3_WritesTextBuffer()
    {
        var (handler, memory) = CreateReadHandler(3);

        int textBuf = 0x0060;
        int parseBuf = 0x0080;
        memory.WriteByte(textBuf, 20);   // max 20 chars
        memory.WriteByte(parseBuf, 10);  // max 10 words

        handler.ProcessRead("OPEN MAILBOX", textBuf, parseBuf);

        // V3: text at byte 1, lowercased, null-terminated.
        Assert.Equal((byte)'o', memory.ReadByte(textBuf + 1));
        Assert.Equal((byte)'p', memory.ReadByte(textBuf + 2));
        Assert.Equal((byte)'e', memory.ReadByte(textBuf + 3));
        Assert.Equal((byte)'n', memory.ReadByte(textBuf + 4));
        Assert.Equal((byte)' ', memory.ReadByte(textBuf + 5));
        Assert.Equal((byte)'m', memory.ReadByte(textBuf + 6));
        Assert.Equal(0, memory.ReadByte(textBuf + 13)); // null terminator
    }

    [Fact]
    public void ReadHandler_V3_Tokenizes()
    {
        var (handler, memory) = CreateReadHandler(3);

        int textBuf = 0x0060;
        int parseBuf = 0x0080;
        memory.WriteByte(textBuf, 20);
        memory.WriteByte(parseBuf, 10);

        handler.ProcessRead("open mailbox", textBuf, parseBuf);

        // Parse buffer: 2 words.
        Assert.Equal(2, memory.ReadByte(parseBuf + 1));

        // Word 1: "open" at dict 0x4688.
        Assert.Equal(0x4688, memory.ReadWord(parseBuf + 2));
        // Word 2: "mailbox" at dict 0x453F.
        Assert.Equal(0x453F, memory.ReadWord(parseBuf + 6));
    }

    [Fact]
    public void ReadHandler_V3_ReturnsEnter()
    {
        var (handler, memory) = CreateReadHandler(3);

        int textBuf = 0x0060;
        int parseBuf = 0x0080;
        memory.WriteByte(textBuf, 20);
        memory.WriteByte(parseBuf, 10);

        int result = handler.ProcessRead("look", textBuf, parseBuf);
        Assert.Equal(13, result);
    }

    [Fact]
    public void ReadHandler_V3_LowercasesInput()
    {
        var (handler, memory) = CreateReadHandler(3);

        int textBuf = 0x0060;
        int parseBuf = 0x0080;
        memory.WriteByte(textBuf, 20);
        memory.WriteByte(parseBuf, 10);

        handler.ProcessRead("LOOK", textBuf, parseBuf);

        Assert.Equal((byte)'l', memory.ReadByte(textBuf + 1));
        Assert.Equal((byte)'o', memory.ReadByte(textBuf + 2));
        Assert.Equal((byte)'o', memory.ReadByte(textBuf + 3));
        Assert.Equal((byte)'k', memory.ReadByte(textBuf + 4));
    }

    #endregion

    #region ReadHandler — V5 Text Buffer

    [Fact]
    public void ReadHandler_V5_WritesTextBuffer()
    {
        var (handler, memory) = CreateReadHandler(5);

        int textBuf = 0x0060;
        int parseBuf = 0x0080;
        memory.WriteByte(textBuf, 20);
        memory.WriteByte(parseBuf, 10);

        handler.ProcessRead("look", textBuf, parseBuf);

        // V5+: byte 1 = char count, text starts at byte 2, no null terminator.
        Assert.Equal(4, memory.ReadByte(textBuf + 1)); // char count
        Assert.Equal((byte)'l', memory.ReadByte(textBuf + 2));
        Assert.Equal((byte)'o', memory.ReadByte(textBuf + 3));
        Assert.Equal((byte)'o', memory.ReadByte(textBuf + 4));
        Assert.Equal((byte)'k', memory.ReadByte(textBuf + 5));
    }

    [Fact]
    public void ReadHandler_V5_SkipTokenization()
    {
        var (handler, memory) = CreateReadHandler(5);

        int textBuf = 0x0060;
        memory.WriteByte(textBuf, 20);

        // parseBufferAddr = 0 means skip tokenization.
        handler.ProcessRead("look", textBuf, 0);

        // Text should still be written.
        Assert.Equal(4, memory.ReadByte(textBuf + 1));
    }

    [Fact]
    public void ReadHandler_V5_TruncatesLongInput()
    {
        var (handler, memory) = CreateReadHandler(5);

        int textBuf = 0x0060;
        memory.WriteByte(textBuf, 5); // max 5 chars
        memory.WriteByte(0x0080, 10);

        handler.ProcessRead("abcdefghij", textBuf, 0x0080);

        Assert.Equal(5, memory.ReadByte(textBuf + 1));
        Assert.Equal((byte)'e', memory.ReadByte(textBuf + 6));
    }

    #endregion

    #region Key Mapping

    [Fact]
    public void MapKeyToZscii_Enter()
    {
        var key = new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);
        Assert.Equal(13, ConsoleInputStream.MapKeyToZscii(key));
    }

    [Fact]
    public void MapKeyToZscii_CursorKeys()
    {
        Assert.Equal(129, ConsoleInputStream.MapKeyToZscii(
            new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, false, false, false)));
        Assert.Equal(130, ConsoleInputStream.MapKeyToZscii(
            new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false)));
        Assert.Equal(131, ConsoleInputStream.MapKeyToZscii(
            new ConsoleKeyInfo('\0', ConsoleKey.LeftArrow, false, false, false)));
        Assert.Equal(132, ConsoleInputStream.MapKeyToZscii(
            new ConsoleKeyInfo('\0', ConsoleKey.RightArrow, false, false, false)));
    }

    [Fact]
    public void MapKeyToZscii_FunctionKeys()
    {
        Assert.Equal(133, ConsoleInputStream.MapKeyToZscii(
            new ConsoleKeyInfo('\0', ConsoleKey.F1, false, false, false)));
        Assert.Equal(144, ConsoleInputStream.MapKeyToZscii(
            new ConsoleKeyInfo('\0', ConsoleKey.F12, false, false, false)));
    }

    [Fact]
    public void MapKeyToZscii_PrintableAscii()
    {
        Assert.Equal('a', ConsoleInputStream.MapKeyToZscii(
            new ConsoleKeyInfo('a', ConsoleKey.A, false, false, false)));
        Assert.Equal(' ', ConsoleInputStream.MapKeyToZscii(
            new ConsoleKeyInfo(' ', ConsoleKey.Spacebar, false, false, false)));
    }

    [Fact]
    public void MapKeyToZscii_Escape()
    {
        Assert.Equal(27, ConsoleInputStream.MapKeyToZscii(
            new ConsoleKeyInfo('\x1b', ConsoleKey.Escape, false, false, false)));
    }

    #endregion

    #region Helpers

    private static (ReadHandler, Memory) CreateReadHandler(int version)
    {
        var memory = new Memory();

        if (version == 3)
        {
            memory.LoadStory(File.ReadAllBytes(Zork1Path));
        }
        else
        {
            int dataSize = 0x0200;
            byte[] data = new byte[dataSize];
            data[0] = (byte)version;
            data[0x0E] = (byte)(dataSize >> 8);
            data[0x0F] = (byte)(dataSize & 0xFF);
            memory.LoadStory(data);
        }

        var encoder = new TextEncoder(version);
        var dictionary = new Dictionary(memory, version, encoder);

        if (version == 3)
        {
            int dictAddr = memory.ReadWord(0x08);
            dictionary.Parse(dictAddr);
        }

        var tokenizer = new Tokenizer(version, encoder);
        var handler = new ReadHandler(version, memory, tokenizer, dictionary);

        return (handler, memory);
    }

    private class MockInputStream : IInputStream
    {
        private readonly string _response;
        public MockInputStream(string response) => _response = response;
        public bool HasMore => true;

        public (string Text, int TerminatingChar) ReadLine(int maxLength, int timeoutTenths = 0)
        {
            string text = _response.Length > maxLength ? _response[..maxLength] : _response;
            return (text, 13);
        }

        public int ReadChar(int timeoutTenths = 0) =>
            _response.Length > 0 ? _response[0] : 0;
    }

    private static string FindRepoRoot()
    {
        string dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName!;
        }
        throw new InvalidOperationException("Could not find repository root.");
    }

    #endregion
}
