namespace ZMachine.Tests;

using ZMachine.Core;
using ZMachine.IO;

/// <summary>
/// Tests for Task 12.3 — Sound Resource Loading and Playback:
/// SoundManager dual-channel model, @sound_effect semantics,
/// callback behavior, V3 loop chunk, and Blorb Loop parsing.
/// </summary>
public class SoundManagerTests
{
    #region Test Helpers

    /// <summary>
    /// Mock audio backend that records all calls for verification.
    /// </summary>
    private class MockAudioBackend : IAudioBackend
    {
        public List<(int Number, byte[] Data, string Format)> Loaded { get; } = new();
        public List<(int Number, int Volume, int Repeats)> Played { get; } = new();
        public List<int> Stopped { get; } = new();
        public List<int> Unloaded { get; } = new();
        public bool AllUnloaded { get; private set; }

        public event Action<int>? OnPlaybackFinished;

        public void LoadSound(int number, byte[] data, string format)
            => Loaded.Add((number, data, format));

        public void Play(int number, int volume, int repeats)
            => Played.Add((number, volume, repeats));

        public void Stop(int number)
            => Stopped.Add(number);

        public void Unload(int number)
            => Unloaded.Add(number);

        public void UnloadAll()
        {
            AllUnloaded = true;
            Unloaded.Clear();
        }

        public void SimulateFinished(int number)
            => OnPlaybackFinished?.Invoke(number);
    }

    /// <summary>Creates a minimal Blorb with AIFF sound resources.</summary>
    private static byte[] BuildSoundBlorb(params (int Number, string Type, byte[] Data)[] sounds)
        => BuildSoundBlorbWithLoop(null, sounds);

    /// <summary>Creates a Blorb with sounds and an optional Loop chunk.</summary>
    private static byte[] BuildSoundBlorbWithLoop(
        (int Number, int Repeats)[]? loopEntries,
        params (int Number, string Type, byte[] Data)[] sounds)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        int count = sounds.Length;
        byte[] ridxData = new byte[4 + count * 12];
        WriteBE32(ridxData, 0, count);

        int ridxChunkLen = ridxData.Length;
        int ridxTotalLen = 8 + ridxChunkLen;
        if (ridxChunkLen % 2 != 0) ridxTotalLen++;

        // Optional Loop chunk
        byte[]? loopData = null;
        int loopTotalLen = 0;
        if (loopEntries != null && loopEntries.Length > 0)
        {
            loopData = new byte[loopEntries.Length * 8];
            for (int i = 0; i < loopEntries.Length; i++)
            {
                WriteBE32(loopData, i * 8, loopEntries[i].Number);
                WriteBE32(loopData, i * 8 + 4, loopEntries[i].Repeats);
            }
            loopTotalLen = 8 + loopData.Length;
            if (loopData.Length % 2 != 0) loopTotalLen++;
        }

        int currentOffset = 12 + ridxTotalLen + loopTotalLen;

        var soundChunks = new List<(string Type, byte[] Data)>();
        for (int i = 0; i < count; i++)
        {
            var (number, type, data) = sounds[i];
            int entryOffset = 4 + i * 12;
            byte[] usage = System.Text.Encoding.ASCII.GetBytes("Snd ");
            Array.Copy(usage, 0, ridxData, entryOffset, 4);
            WriteBE32(ridxData, entryOffset + 4, number);
            WriteBE32(ridxData, entryOffset + 8, currentOffset);

            soundChunks.Add((type, data));

            int chunkTotalLen = 8 + data.Length;
            if (data.Length % 2 != 0) chunkTotalLen++;
            currentOffset += chunkTotalLen;
        }

        int formContentLen = 4 + ridxTotalLen + loopTotalLen;
        foreach (var (_, data) in soundChunks)
        {
            formContentLen += 8 + data.Length;
            if (data.Length % 2 != 0) formContentLen++;
        }

        bw.Write(System.Text.Encoding.ASCII.GetBytes("FORM"));
        WriteBE32(bw, formContentLen);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("IFRS"));

        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIdx"));
        WriteBE32(bw, ridxData.Length);
        bw.Write(ridxData);
        if (ridxData.Length % 2 != 0) bw.Write((byte)0);

        if (loopData != null)
        {
            bw.Write(System.Text.Encoding.ASCII.GetBytes("Loop"));
            WriteBE32(bw, loopData.Length);
            bw.Write(loopData);
            if (loopData.Length % 2 != 0) bw.Write((byte)0);
        }

        for (int i = 0; i < soundChunks.Count; i++)
        {
            var (type, data) = soundChunks[i];
            byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type.PadRight(4));
            bw.Write(typeBytes);
            WriteBE32(bw, data.Length);
            bw.Write(data);
            if (data.Length % 2 != 0) bw.Write((byte)0);
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Creates AIFF inner data for a Blorb FORM chunk.
    /// Blorb "AIFF Sounds" — nested FORM with formtype 'AIFF'.
    /// The Blorb builder writes the FORM header; this returns just
    /// the inner type + minimal sub-chunk data.
    /// </summary>
    private static byte[] CreateFakeAiff()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        // Inner form type read by IffReader as InnerFormType
        bw.Write(System.Text.Encoding.ASCII.GetBytes("AIFF"));
        // Minimal COMM chunk (required by AIFF)
        bw.Write(System.Text.Encoding.ASCII.GetBytes("COMM"));
        WriteBE32(bw, 18);
        bw.Write(new byte[18]); // 18 bytes of zeros
        return ms.ToArray();
    }

    /// <summary>Creates fake Ogg data (just a tag — enough for type detection).</summary>
    private static byte[] CreateFakeOgg()
        => System.Text.Encoding.ASCII.GetBytes("OggSfakedata");

    /// <summary>Creates fake MOD data.</summary>
    private static byte[] CreateFakeMod()
        => new byte[1084]; // MOD files are at least 1084 bytes

    private static void WriteBE32(byte[] buf, int offset, int value)
    {
        buf[offset] = (byte)(value >> 24);
        buf[offset + 1] = (byte)(value >> 16);
        buf[offset + 2] = (byte)(value >> 8);
        buf[offset + 3] = (byte)value;
    }

    private static void WriteBE32(BinaryWriter bw, int value)
    {
        bw.Write((byte)(value >> 24));
        bw.Write((byte)(value >> 16));
        bw.Write((byte)(value >> 8));
        bw.Write((byte)value);
    }

    private SoundManager CreateManager(BlorbReader? blorb, MockAudioBackend backend, int version = 5)
        => new SoundManager(blorb, backend, version);

    #endregion

    #region No Blorb

    [Fact]
    public void NoBlorb_HasSounds_False()
    {
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(null, backend);
        Assert.False(mgr.HasSounds);
    }

    [Fact]
    public void NoBlorb_SoundCount_Zero()
    {
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(null, backend);
        Assert.Equal(0, mgr.SoundCount);
    }

    [Fact]
    public void NoBlorb_PlaySound_NoOp()
    {
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(null, backend);
        mgr.PlaySound(1, 8, 1, 0);
        Assert.Empty(backend.Played);
    }

    #endregion

    #region Sound Resource Properties

    [Fact]
    public void HasSounds_WithSoundResources()
    {
        byte[] aiff = CreateFakeAiff();
        byte[] blorb = BuildSoundBlorb((1, "FORM", aiff));
        var reader = BlorbReader.Load(blorb);
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(reader, backend);

        Assert.True(mgr.HasSounds);
        Assert.Equal(1, mgr.SoundCount);
    }

    #endregion

    #region Channel Classification

    [Fact]
    public void Channel_AIFF_IsEffect()
    {
        byte[] aiff = CreateFakeAiff();
        byte[] blorb = BuildSoundBlorb((1, "FORM", aiff));
        var reader = BlorbReader.Load(blorb);
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(reader, backend);

        Assert.Equal(SoundChannel.Effect, mgr.GetChannel(1));
    }

    [Fact]
    public void Channel_OGGV_IsMusic()
    {
        byte[] ogg = CreateFakeOgg();
        byte[] blorb = BuildSoundBlorb((1, "OGGV", ogg));
        var reader = BlorbReader.Load(blorb);
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(reader, backend);

        Assert.Equal(SoundChannel.Music, mgr.GetChannel(1));
    }

    [Fact]
    public void Channel_MOD_IsMusic()
    {
        byte[] mod = CreateFakeMod();
        byte[] blorb = BuildSoundBlorb((1, "MOD ", mod));
        var reader = BlorbReader.Load(blorb);
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(reader, backend);

        Assert.Equal(SoundChannel.Music, mgr.GetChannel(1));
    }

    [Fact]
    public void Channel_Nonexistent_IsNone()
    {
        byte[] aiff = CreateFakeAiff();
        byte[] blorb = BuildSoundBlorb((1, "FORM", aiff));
        var reader = BlorbReader.Load(blorb);
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(reader, backend);

        Assert.Equal(SoundChannel.None, mgr.GetChannel(999));
    }

    #endregion

    #region Dual-Channel Model

    [Fact]
    public void Effect_InterruptsEffect()
    {
        byte[] aiff1 = CreateFakeAiff();
        byte[] aiff2 = CreateFakeAiff();
        byte[] blorb = BuildSoundBlorb((1, "FORM", aiff1), (2, "FORM", aiff2));
        var reader = BlorbReader.Load(blorb);
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(reader, backend);

        mgr.PlaySound(1, 8, 1, 0);
        mgr.PlaySound(2, 8, 1, 0);

        // First effect should be stopped before second plays
        Assert.Contains(1, backend.Stopped);
        Assert.Equal(2, backend.Played.Count);
    }

    [Fact]
    public void Music_InterruptsMusic()
    {
        byte[] ogg1 = CreateFakeOgg();
        byte[] ogg2 = CreateFakeOgg();
        byte[] blorb = BuildSoundBlorb((1, "OGGV", ogg1), (2, "OGGV", ogg2));
        var reader = BlorbReader.Load(blorb);
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(reader, backend);

        mgr.PlaySound(1, 8, 1, 0);
        mgr.PlaySound(2, 8, 1, 0);

        Assert.Contains(1, backend.Stopped);
        Assert.Equal(2, backend.Played.Count);
    }

    [Fact]
    public void Effect_DoesNotInterruptMusic()
    {
        byte[] ogg = CreateFakeOgg();
        byte[] aiff = CreateFakeAiff();
        byte[] blorb = BuildSoundBlorb((1, "OGGV", ogg), (2, "FORM", aiff));
        var reader = BlorbReader.Load(blorb);
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(reader, backend);

        mgr.PlaySound(1, 8, 1, 0);
        mgr.PlaySound(2, 8, 1, 0);

        // Music (1) should NOT be stopped when effect (2) starts
        Assert.DoesNotContain(1, backend.Stopped);
        Assert.Equal(2, backend.Played.Count);
    }

    [Fact]
    public void Music_DoesNotInterruptEffect()
    {
        byte[] aiff = CreateFakeAiff();
        byte[] ogg = CreateFakeOgg();
        byte[] blorb = BuildSoundBlorb((1, "FORM", aiff), (2, "OGGV", ogg));
        var reader = BlorbReader.Load(blorb);
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(reader, backend);

        mgr.PlaySound(1, 8, 1, 0);
        mgr.PlaySound(2, 8, 1, 0);

        // Effect (1) should NOT be stopped when music (2) starts
        Assert.DoesNotContain(1, backend.Stopped);
    }

    #endregion

    #region Volume

    [Fact]
    public void Volume_255_MappedTo8()
    {
        byte[] aiff = CreateFakeAiff();
        byte[] blorb = BuildSoundBlorb((1, "FORM", aiff));
        var reader = BlorbReader.Load(blorb);
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(reader, backend);

        mgr.PlaySound(1, 255, 1, 0);

        Assert.Single(backend.Played);
        Assert.Equal(8, backend.Played[0].Volume);
    }

    [Fact]
    public void Volume_Clamped_1to8()
    {
        byte[] aiff = CreateFakeAiff();
        byte[] blorb = BuildSoundBlorb((1, "FORM", aiff));
        var reader = BlorbReader.Load(blorb);
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(reader, backend);

        mgr.PlaySound(1, 5, 1, 0);

        Assert.Single(backend.Played);
        Assert.Equal(5, backend.Played[0].Volume);
    }

    #endregion

    #region Repeats V5+

    [Fact]
    public void V5_ZeroRepeats_TreatedAsOne()
    {
        byte[] aiff = CreateFakeAiff();
        byte[] blorb = BuildSoundBlorb((1, "FORM", aiff));
        var reader = BlorbReader.Load(blorb);
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(reader, backend, version: 5);

        mgr.PlaySound(1, 8, 0, 0);

        Assert.Single(backend.Played);
        Assert.Equal(1, backend.Played[0].Repeats);
    }

    [Fact]
    public void V5_Repeats_PassedThrough()
    {
        byte[] aiff = CreateFakeAiff();
        byte[] blorb = BuildSoundBlorb((1, "FORM", aiff));
        var reader = BlorbReader.Load(blorb);
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(reader, backend, version: 5);

        mgr.PlaySound(1, 8, 3, 0);

        Assert.Single(backend.Played);
        Assert.Equal(3, backend.Played[0].Repeats);
    }

    #endregion

    #region Callbacks

    [Fact]
    public void Callback_FiredOnNaturalFinish()
    {
        byte[] aiff = CreateFakeAiff();
        byte[] blorb = BuildSoundBlorb((1, "FORM", aiff));
        var reader = BlorbReader.Load(blorb);
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(reader, backend);

        ushort callbackAddr = 0;
        mgr.OnCallback += addr => callbackAddr = addr;

        mgr.PlaySound(1, 8, 1, 0x1234);
        backend.SimulateFinished(1);

        Assert.Equal(0x1234, callbackAddr);
    }

    [Fact]
    public void Callback_NotFiredOnManualStop()
    {
        byte[] aiff = CreateFakeAiff();
        byte[] blorb = BuildSoundBlorb((1, "FORM", aiff));
        var reader = BlorbReader.Load(blorb);
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(reader, backend);

        bool callbackFired = false;
        mgr.OnCallback += _ => callbackFired = true;

        mgr.PlaySound(1, 8, 1, 0x1234);
        mgr.StopSound(1);

        Assert.False(callbackFired);
    }

    [Fact]
    public void Callback_NotFiredOnInterruption()
    {
        byte[] aiff1 = CreateFakeAiff();
        byte[] aiff2 = CreateFakeAiff();
        byte[] blorb = BuildSoundBlorb((1, "FORM", aiff1), (2, "FORM", aiff2));
        var reader = BlorbReader.Load(blorb);
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(reader, backend);

        bool callbackFired = false;
        mgr.OnCallback += _ => callbackFired = true;

        mgr.PlaySound(1, 8, 1, 0x1234);
        // Effect 2 interrupts effect 1 — callback should NOT fire
        mgr.PlaySound(2, 8, 1, 0);

        Assert.False(callbackFired);
    }

    [Fact]
    public void Callback_ZeroAddress_NotFired()
    {
        byte[] aiff = CreateFakeAiff();
        byte[] blorb = BuildSoundBlorb((1, "FORM", aiff));
        var reader = BlorbReader.Load(blorb);
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(reader, backend);

        bool callbackFired = false;
        mgr.OnCallback += _ => callbackFired = true;

        mgr.PlaySound(1, 8, 1, 0);
        backend.SimulateFinished(1);

        Assert.False(callbackFired);
    }

    #endregion

    #region Stop and Unload

    [Fact]
    public void StopSound_SpecificNumber()
    {
        byte[] aiff = CreateFakeAiff();
        byte[] blorb = BuildSoundBlorb((1, "FORM", aiff));
        var reader = BlorbReader.Load(blorb);
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(reader, backend);

        mgr.PlaySound(1, 8, 1, 0);
        mgr.StopSound(1);

        Assert.Contains(1, backend.Stopped);
    }

    [Fact]
    public void StopSound_Zero_StopsAll()
    {
        byte[] aiff = CreateFakeAiff();
        byte[] ogg = CreateFakeOgg();
        byte[] blorb = BuildSoundBlorb((1, "FORM", aiff), (2, "OGGV", ogg));
        var reader = BlorbReader.Load(blorb);
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(reader, backend);

        mgr.PlaySound(1, 8, 1, 0);
        mgr.PlaySound(2, 8, 1, 0);
        mgr.StopSound(0);

        Assert.Contains(1, backend.Stopped);
        Assert.Contains(2, backend.Stopped);
    }

    [Fact]
    public void UnloadSound_StopsAndUnloads()
    {
        byte[] aiff = CreateFakeAiff();
        byte[] blorb = BuildSoundBlorb((1, "FORM", aiff));
        var reader = BlorbReader.Load(blorb);
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(reader, backend);

        mgr.PlaySound(1, 8, 1, 0);
        mgr.UnloadSound(1);

        Assert.Contains(1, backend.Stopped);
        Assert.Contains(1, backend.Unloaded);
    }

    [Fact]
    public void UnloadSound_Zero_StopsAndUnloadsAll()
    {
        byte[] aiff = CreateFakeAiff();
        byte[] blorb = BuildSoundBlorb((1, "FORM", aiff));
        var reader = BlorbReader.Load(blorb);
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(reader, backend);

        mgr.PlaySound(1, 8, 1, 0);
        mgr.UnloadSound(0);

        Assert.True(backend.AllUnloaded);
    }

    [Fact]
    public void StopAll_BothChannels()
    {
        byte[] aiff = CreateFakeAiff();
        byte[] ogg = CreateFakeOgg();
        byte[] blorb = BuildSoundBlorb((1, "FORM", aiff), (2, "OGGV", ogg));
        var reader = BlorbReader.Load(blorb);
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(reader, backend);

        mgr.PlaySound(1, 8, 1, 0);
        mgr.PlaySound(2, 8, 1, 0);
        mgr.StopAll();

        Assert.Contains(1, backend.Stopped);
        Assert.Contains(2, backend.Stopped);
    }

    #endregion

    #region V3 Loop Chunk

    [Fact]
    public void V3_LoopChunk_PlayOnce()
    {
        byte[] aiff = CreateFakeAiff();
        var loopEntries = new[] { (Number: 1, Repeats: 1) };
        byte[] blorb = BuildSoundBlorbWithLoop(loopEntries, (1, "FORM", aiff));
        var reader = BlorbReader.Load(blorb);
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(reader, backend, version: 3);

        mgr.PlaySound(1, 8, 0, 0);

        Assert.Single(backend.Played);
        Assert.Equal(1, backend.Played[0].Repeats);
    }

    [Fact]
    public void V3_LoopChunk_LoopForever()
    {
        byte[] aiff = CreateFakeAiff();
        var loopEntries = new[] { (Number: 1, Repeats: 0) };
        byte[] blorb = BuildSoundBlorbWithLoop(loopEntries, (1, "FORM", aiff));
        var reader = BlorbReader.Load(blorb);
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(reader, backend, version: 3);

        mgr.PlaySound(1, 8, 0, 0);

        Assert.Single(backend.Played);
        Assert.Equal(255, backend.Played[0].Repeats); // 0xFF = loop forever
    }

    [Fact]
    public void V3_NoLoopEntry_PlayOnce()
    {
        byte[] aiff = CreateFakeAiff();
        // Loop chunk present but no entry for sound 1
        var loopEntries = new[] { (Number: 99, Repeats: 0) };
        byte[] blorb = BuildSoundBlorbWithLoop(loopEntries, (1, "FORM", aiff));
        var reader = BlorbReader.Load(blorb);
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(reader, backend, version: 3);

        mgr.PlaySound(1, 8, 0, 0);

        Assert.Single(backend.Played);
        Assert.Equal(1, backend.Played[0].Repeats);
    }

    #endregion

    #region BlorbReader Loop Parsing

    [Fact]
    public void BlorbReader_LoopChunk_Parsed()
    {
        byte[] aiff = CreateFakeAiff();
        var loopEntries = new[] { (Number: 1, Repeats: 1), (Number: 2, Repeats: 0) };
        byte[] blorb = BuildSoundBlorbWithLoop(loopEntries, (1, "FORM", aiff));
        var reader = BlorbReader.Load(blorb);

        Assert.NotNull(reader.LoopInfo);
        Assert.Equal(2, reader.LoopInfo!.Count);
        Assert.Equal(1, reader.LoopInfo[1]);
        Assert.Equal(0, reader.LoopInfo[2]);
    }

    [Fact]
    public void BlorbReader_NoLoopChunk_Null()
    {
        byte[] aiff = CreateFakeAiff();
        byte[] blorb = BuildSoundBlorb((1, "FORM", aiff));
        var reader = BlorbReader.Load(blorb);

        Assert.Null(reader.LoopInfo);
    }

    #endregion

    #region PrepareSound

    [Fact]
    public void PrepareSound_LoadsData()
    {
        byte[] aiff = CreateFakeAiff();
        byte[] blorb = BuildSoundBlorb((1, "FORM", aiff));
        var reader = BlorbReader.Load(blorb);
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(reader, backend);

        mgr.PrepareSound(1);

        Assert.Single(backend.Loaded);
        Assert.Equal(1, backend.Loaded[0].Number);
    }

    [Fact]
    public void PrepareSound_Nonexistent_NoOp()
    {
        byte[] aiff = CreateFakeAiff();
        byte[] blorb = BuildSoundBlorb((1, "FORM", aiff));
        var reader = BlorbReader.Load(blorb);
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(reader, backend);

        mgr.PrepareSound(999);

        Assert.Empty(backend.Loaded);
    }

    #endregion

    #region ISoundEngine Interface

    [Fact]
    public void ImplementsISoundEngine()
    {
        var backend = new MockAudioBackend();
        using var mgr = CreateManager(null, backend);
        Assert.IsAssignableFrom<ISoundEngine>(mgr);
    }

    #endregion

    #region Dispose

    [Fact]
    public void Dispose_StopsAll()
    {
        byte[] aiff = CreateFakeAiff();
        byte[] blorb = BuildSoundBlorb((1, "FORM", aiff));
        var reader = BlorbReader.Load(blorb);
        var backend = new MockAudioBackend();
        var mgr = CreateManager(reader, backend);

        mgr.PlaySound(1, 8, 1, 0);
        mgr.Dispose();

        Assert.Contains(1, backend.Stopped);
    }

    #endregion
}
