namespace ZMachine.Tests.Harness;

using ZMachine.Core;

/// <summary>
/// In-memory save file provider for testing @save/@restore without disk I/O.
/// Stores save data in a byte array and replays it on restore.
/// </summary>
public class MemorySaveFileProvider : ISaveFileProvider
{
    private byte[]? _savedData;

    /// <summary>Whether a save file is currently stored.</summary>
    public bool HasSave => _savedData != null;

    /// <summary>The raw save data, or null if no save has been made.</summary>
    public byte[]? SavedData => _savedData;

    public Stream? OpenForSave()
    {
        var stream = new SaveCaptureStream(this);
        return stream;
    }

    public Stream? OpenForRestore()
    {
        if (_savedData == null)
            return null;
        return new MemoryStream(_savedData, writable: false);
    }

    /// <summary>
    /// Writable stream that captures all written data into the provider
    /// when disposed.
    /// </summary>
    private class SaveCaptureStream : MemoryStream
    {
        private readonly MemorySaveFileProvider _provider;

        public SaveCaptureStream(MemorySaveFileProvider provider) => _provider = provider;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _provider._savedData = ToArray();
            base.Dispose(disposing);
        }
    }
}
