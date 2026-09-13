namespace ZMachine.Core;

/// <summary>
/// Provides file streams for @save and @restore opcodes. The GUI host
/// implements this to show a file dialog; the test harness uses an
/// in-memory implementation.
/// </summary>
/// <remarks>
/// ZSpec S15 — @save / @restore: "The interpreter should ask the player
/// for a filename." Quetzal S5.8 — the save stream carries an IFF FORM
/// 'IFZS' container.
/// </remarks>
public interface ISaveFileProvider
{
    /// <summary>
    /// Opens a stream for writing a save file. Returns null if the user
    /// cancels the save dialog. The caller will dispose the stream.
    /// </summary>
    Stream? OpenForSave();

    /// <summary>
    /// Opens a stream for reading a save file. Returns null if the user
    /// cancels the restore dialog. The caller will dispose the stream.
    /// </summary>
    Stream? OpenForRestore();
}
