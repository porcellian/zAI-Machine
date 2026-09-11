namespace ZMachine.Core;

/// <summary>
/// Represents the current state of the mouse, including position and
/// button status. Used by <c>@read_mouse</c> and mouse click input.
/// </summary>
/// <remarks>
/// ZSpec11 "@read_mouse" — realtime position reading.
/// ZSpec11 "Mouse co-ordinates" — coordinates relative to (1,1) at top-left.
/// ZSpec11 "Mouse clicks" — ZSCII 254 for single/first click, 253 for
/// second of double-click (V6 only).
/// </remarks>
public class MouseState
{
    /// <summary>Mouse Y position, 1-based from top of display.</summary>
    public int Y { get; set; } = 1;

    /// <summary>Mouse X position, 1-based from left of display.</summary>
    public int X { get; set; } = 1;

    /// <summary>
    /// Button bitfield. Bit 0 = primary button, bit 1 = secondary, etc.
    /// ZSpec11 "@read_mouse" — button assignments vary by platform.
    /// </summary>
    public int Buttons { get; set; }

    /// <summary>
    /// Menu word (V6). Encodes which menu item was selected, or 0 if none.
    /// High byte = menu number, low byte = item number.
    /// </summary>
    public int Menu { get; set; }

    /// <summary>
    /// Updates the mouse position. Coordinates are 1-based relative to
    /// the top-left of the display.
    /// </summary>
    public void SetPosition(int y, int x)
    {
        Y = y;
        X = x;
    }

    /// <summary>
    /// Updates the button state.
    /// </summary>
    public void SetButtons(int buttons)
    {
        Buttons = buttons;
    }

    /// <summary>
    /// Writes the current mouse state to the array at the given address.
    /// ZSpec11 "@read_mouse" — array format: word 0=y, 1=x, 2=buttons, 3=menu.
    /// </summary>
    public void WriteToArray(Memory memory, int arrayAddress)
    {
        memory.WriteWord(arrayAddress, (ushort)Y);
        memory.WriteWord(arrayAddress + 2, (ushort)X);
        memory.WriteWord(arrayAddress + 4, (ushort)Buttons);
        memory.WriteWord(arrayAddress + 6, (ushort)Menu);
    }

    /// <summary>
    /// Writes the mouse click coordinates to the header bytes used during
    /// <c>@read</c> / <c>@read_char</c> input.
    /// ZSpec11 "Mouse co-ordinates" — header words at $24 (y) and $26 (x)
    /// for V5, or click coordinates for V6.
    /// </summary>
    /// <remarks>
    /// ZSpec S8.4 — In V5+, when a mouse click terminates input, the
    /// interpreter writes y to header word $24 and x to header word $26.
    /// Despite TASKS.md referencing $26/$27 as bytes, the spec uses words.
    /// </remarks>
    public void WriteClickToHeader(Memory memory)
    {
        memory.WriteWord(0x24, (ushort)Y);
        memory.WriteWord(0x26, (ushort)X);
    }

    /// <summary>
    /// Returns the ZSCII code for a mouse click event.
    /// ZSpec11 "Mouse clicks" — V5: always 254. V6: 254 for single/first
    /// click, 253 for second of double-click.
    /// </summary>
    public static int GetClickZscii(int version, bool isDoubleClickSecond)
    {
        if (version == 6 && isDoubleClickSecond)
            return 253;
        return 254;
    }
}
