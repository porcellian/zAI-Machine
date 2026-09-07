namespace ZMachine.Core;

/// <summary>
/// Constructs the V1-3 status line text from Z-Machine state: reads the
/// current room object from global 0, score/turns from globals 1/2, and
/// the time-mode flag from header Flags 1 bit 1.
/// </summary>
/// <remarks>
/// ZSpec S8.2 — The status line shows the short name of the object whose
/// number is in global variable 0, plus either "score/turns" or
/// "hours:minutes" depending on Flags 1 bit 1.
///
/// The status line is displayed before each @read in V1-3.
/// </remarks>
public class StatusLineHandler
{
    private readonly Memory _memory;
    private readonly ObjectTable _objectTable;
    private readonly TextDecoder _textDecoder;
    private readonly int _globalsAddress;
    private readonly bool _isTimeGame;

    public StatusLineHandler(Memory memory, ObjectTable objectTable, TextDecoder textDecoder)
    {
        _memory = memory;
        _objectTable = objectTable;
        _textDecoder = textDecoder;

        _globalsAddress = memory.ReadWord(0x0C);

        // ZSpec S11 — Flags 1 bit 1: 0 = score game, 1 = time game.
        byte flags1 = memory.ReadByte(0x01);
        _isTimeGame = (flags1 & 0x02) != 0;
    }

    /// <summary>Whether this is a time game (Flags 1 bit 1 set).</summary>
    public bool IsTimeGame => _isTimeGame;

    /// <summary>
    /// Reads global variable N (0-based). Globals are stored as consecutive
    /// 16-bit words starting at the globals table address (header $0C).
    /// </summary>
    /// <remarks>ZSpec S6.2 — Global variables 0-239 stored as words.</remarks>
    public ushort ReadGlobal(int index)
    {
        return _memory.ReadWord(_globalsAddress + index * 2);
    }

    /// <summary>
    /// Gets the location name: the short name of the object whose number
    /// is in global variable 0.
    /// </summary>
    public string GetLocationName()
    {
        int objNum = ReadGlobal(0);
        if (objNum == 0)
            return "";

        int nameAddr = _objectTable.GetShortNameAddress(objNum);
        var (text, _) = _textDecoder.DecodeZString(nameAddr);
        return text;
    }

    /// <summary>
    /// Formats the right-hand portion of the status line: either
    /// "score/turns" or "HH:MM" depending on the game type.
    /// </summary>
    public string GetScoreOrTime()
    {
        ushort val1 = ReadGlobal(1);
        ushort val2 = ReadGlobal(2);

        if (_isTimeGame)
        {
            // ZSpec S8.2 — Time display: "hours:minutes" from globals 1 and 2.
            int hours = (short)val1;
            int minutes = (short)val2;
            return $"{hours:D2}:{minutes:D2}";
        }

        // Score game: "score/turns" from globals 1 and 2.
        int score = (short)val1;
        int turns = (short)val2;
        return $"{score}/{turns}";
    }

    /// <summary>
    /// Builds the complete status line content: location + score/time.
    /// The caller is responsible for rendering it (e.g., via IScreen.ShowStatusLine).
    /// </summary>
    public (string Location, string ScoreOrTime) BuildStatusLine()
    {
        return (GetLocationName(), GetScoreOrTime());
    }
}
