namespace ZMachine.IO;

using SkiaSharp;
using ZMachine.Core;

/// <summary>
/// GUI-based IScreen implementation that maps Z-Machine screen operations
/// to an IRenderer via a character grid backing store with word wrap.
/// </summary>
/// <remarks>
/// ZSpec S8 — The screen model: V1-3 status line + scrolling window,
/// V4-5 split upper/lower windows, V6 eight independent windows.
/// This class handles the character grid logic; the IRenderer handles
/// pixel-level drawing.
/// </remarks>
public class GuiScreen : IScreen
{
    private readonly IRenderer _renderer;
    private readonly ThemeConfig _theme;

    private int _columns;
    private int _rows;

    // Character grid: [row, col] stores the character at each cell
    private char[,] _charGrid = null!;
    private SKColor[,] _fgGrid = null!;
    private SKColor[,] _bgGrid = null!;
    private int[,] _styleGrid = null!;

    // Current state
    private int _currentWindow;
    private int _upperWindowLines;
    private bool _bufferMode = true;
    private int _currentStyle;
    private SKColor _currentFg;
    private SKColor _currentBg;

    // Lower window cursor (0-based)
    private int _lowerCursorRow;
    private int _lowerCursorCol;

    // Upper window cursor (0-based internally, 1-based in Z-Machine API)
    private int _upperCursorRow;
    private int _upperCursorCol;

    // Word wrap buffer for the lower window
    private readonly List<char> _lineBuffer = new();

    public GuiScreen(IRenderer renderer, ThemeConfig theme)
    {
        _renderer = renderer;
        _theme = theme;
        _columns = theme.Columns;
        _rows = theme.Rows;

        _currentFg = theme.GetColor(theme.DefaultForeground);
        _currentBg = theme.GetColor(theme.DefaultBackground);

        _renderer.Initialize(theme.PixelWidth, theme.PixelHeight, theme);
        InitGrids();

        _lowerCursorRow = _upperWindowLines;
        _lowerCursorCol = 0;
    }

    private void InitGrids()
    {
        _charGrid = new char[_rows, _columns];
        _fgGrid = new SKColor[_rows, _columns];
        _bgGrid = new SKColor[_rows, _columns];
        _styleGrid = new int[_rows, _columns];

        var bg = _theme.GetColor(_theme.DefaultBackground);

        for (int r = 0; r < _rows; r++)
        for (int c = 0; c < _columns; c++)
        {
            _charGrid[r, c] = ' ';
            _fgGrid[r, c] = _currentFg;
            _bgGrid[r, c] = bg;
            _styleGrid[r, c] = 0;
        }
    }

    public void Print(string text)
    {
        foreach (char c in text)
            PrintChar(c);
    }

    public void PrintChar(char c)
    {
        if (_currentWindow == 1)
        {
            PrintToUpperWindow(c);
            return;
        }

        if (c == '\n')
        {
            NewLine();
            return;
        }

        if (_bufferMode)
        {
            _lineBuffer.Add(c);
            if (_lowerCursorCol + _lineBuffer.Count > _columns)
                FlushWithWordWrap();
        }
        else
        {
            PutCharAtLower(c);
            _lowerCursorCol++;
            if (_lowerCursorCol >= _columns)
            {
                _lowerCursorCol = 0;
                AdvanceLowerRow();
            }
        }
    }

    public void NewLine()
    {
        if (_currentWindow == 1)
        {
            _upperCursorRow++;
            _upperCursorCol = 0;
            return;
        }

        FlushLineBuffer();
        _lowerCursorCol = 0;
        AdvanceLowerRow();
    }

    /// <remarks>
    /// ZSpec S8.2 — Status line: location left-aligned, score/time right-aligned.
    /// Drawn as reverse video in the top row.
    /// </remarks>
    public void ShowStatusLine(string location, string scoreOrTime)
    {
        string line = ConsoleScreen.FormatStatusLine(location, scoreOrTime, _columns);
        var fg = _theme.GetColor(_theme.DefaultBackground);
        var bg = _theme.GetColor(_theme.DefaultForeground);

        for (int c = 0; c < _columns && c < line.Length; c++)
        {
            _charGrid[0, c] = line[c];
            _fgGrid[0, c] = fg;
            _bgGrid[0, c] = bg;
            _styleGrid[0, c] = 0;
            _renderer.DrawCharacter(c, 0, line[c], fg, bg, 0);
        }

        _renderer.Refresh();
    }

    /// <remarks>ZSpec S8.7 — @split_window</remarks>
    public void SplitWindow(int lines)
    {
        _upperWindowLines = lines;
        if (lines == 0)
            _currentWindow = 0;

        // Ensure lower cursor stays below the split
        if (_lowerCursorRow < _upperWindowLines)
            _lowerCursorRow = _upperWindowLines;
    }

    public void SetWindow(int window)
    {
        if (_currentWindow == 0 && window == 1)
            FlushLineBuffer();

        _currentWindow = window;

        if (window == 1)
        {
            _upperCursorRow = 0;
            _upperCursorCol = 0;
        }
    }

    public void EraseLine()
    {
        int row, col;
        if (_currentWindow == 1)
        {
            row = _upperCursorRow;
            col = _upperCursorCol;
        }
        else
        {
            FlushLineBuffer();
            row = _lowerCursorRow;
            col = _lowerCursorCol;
        }

        if (row < _rows)
        {
            for (int c = col; c < _columns; c++)
            {
                _charGrid[row, c] = ' ';
                _fgGrid[row, c] = _currentFg;
                _bgGrid[row, c] = _currentBg;
                _styleGrid[row, c] = 0;
                _renderer.DrawCharacter(c, row, ' ', _currentFg, _currentBg, 0);
            }
        }

        _renderer.Refresh();
    }

    /// <remarks>ZSpec S8.7.3 — @erase_window</remarks>
    public void EraseWindow(int window)
    {
        if (window == -1 || window == -2)
        {
            ClearRows(0, _rows);
            if (window == -1)
                _upperWindowLines = 0;
            _lowerCursorRow = _upperWindowLines;
            _lowerCursorCol = 0;
            _lineBuffer.Clear();
        }
        else if (window == 0)
        {
            ClearRows(_upperWindowLines, _rows);
            _lowerCursorRow = _upperWindowLines;
            _lowerCursorCol = 0;
            _lineBuffer.Clear();
        }
        else if (window == 1)
        {
            ClearRows(0, _upperWindowLines);
        }

        _renderer.Refresh();
    }

    /// <remarks>ZSpec S8.7.2 — Cursor is 1-based in the Z-Machine API.</remarks>
    public void SetCursor(int line, int column)
    {
        if (_currentWindow == 1)
        {
            _upperCursorRow = line - 1;
            _upperCursorCol = column - 1;
        }
    }

    /// <remarks>
    /// ZSpec11 "@set_text_style" — 0=Roman, 1=Reverse, 2=Bold,
    /// 4=Italic, 8=Fixed-pitch. Style 0 resets.
    /// </remarks>
    public void SetTextStyle(int style)
    {
        _currentStyle = style;
    }

    public void BufferMode(bool enabled)
    {
        if (_bufferMode && !enabled)
            FlushLineBuffer();
        _bufferMode = enabled;
    }

    public (int Columns, int Rows) GetScreenSize()
    {
        return (_columns, _rows);
    }

    /// <summary>
    /// Forces a refresh of the back buffer to the display.
    /// Called by the host after input completes.
    /// </summary>
    public void ForceRefresh()
    {
        FlushLineBuffer();
        _renderer.Refresh();
    }

    #region Internal Drawing

    private void PutCharAtLower(char c)
    {
        if (_lowerCursorRow >= _rows) return;
        _charGrid[_lowerCursorRow, _lowerCursorCol] = c;
        _fgGrid[_lowerCursorRow, _lowerCursorCol] = _currentFg;
        _bgGrid[_lowerCursorRow, _lowerCursorCol] = _currentBg;
        _styleGrid[_lowerCursorRow, _lowerCursorCol] = _currentStyle;

        _renderer.DrawCharacter(_lowerCursorCol, _lowerCursorRow,
            c, _currentFg, _currentBg, _currentStyle);
    }

    private void PrintToUpperWindow(char c)
    {
        if (_upperCursorRow >= _upperWindowLines || _upperCursorRow >= _rows)
            return;

        _charGrid[_upperCursorRow, _upperCursorCol] = c;
        _fgGrid[_upperCursorRow, _upperCursorCol] = _currentFg;
        _bgGrid[_upperCursorRow, _upperCursorCol] = _currentBg;
        _styleGrid[_upperCursorRow, _upperCursorCol] = _currentStyle;

        _renderer.DrawCharacter(_upperCursorCol, _upperCursorRow,
            c, _currentFg, _currentBg, _currentStyle);

        _upperCursorCol++;
        if (_upperCursorCol >= _columns)
        {
            _upperCursorCol = 0;
            _upperCursorRow++;
        }
    }

    private void ClearRows(int startRow, int endRow)
    {
        var bg = _currentBg;
        for (int r = startRow; r < endRow && r < _rows; r++)
        for (int c = 0; c < _columns; c++)
        {
            _charGrid[r, c] = ' ';
            _fgGrid[r, c] = _currentFg;
            _bgGrid[r, c] = bg;
            _styleGrid[r, c] = 0;
            _renderer.DrawCharacter(c, r, ' ', _currentFg, bg, 0);
        }
    }

    private void AdvanceLowerRow()
    {
        _lowerCursorRow++;
        if (_lowerCursorRow >= _rows)
        {
            ScrollLowerWindow();
            _lowerCursorRow = _rows - 1;
        }
    }

    /// <summary>
    /// Scrolls the lower window up by one line: shifts character grid
    /// rows and redraws from the backing store.
    /// </summary>
    private void ScrollLowerWindow()
    {
        int startRow = _upperWindowLines;

        for (int r = startRow; r < _rows - 1; r++)
        for (int c = 0; c < _columns; c++)
        {
            _charGrid[r, c] = _charGrid[r + 1, c];
            _fgGrid[r, c] = _fgGrid[r + 1, c];
            _bgGrid[r, c] = _bgGrid[r + 1, c];
            _styleGrid[r, c] = _styleGrid[r + 1, c];
        }

        // Clear the bottom row
        var bg = _theme.GetColor(_theme.DefaultBackground);
        for (int c = 0; c < _columns; c++)
        {
            _charGrid[_rows - 1, c] = ' ';
            _fgGrid[_rows - 1, c] = _currentFg;
            _bgGrid[_rows - 1, c] = bg;
            _styleGrid[_rows - 1, c] = 0;
        }

        // Redraw all lower window rows
        for (int r = startRow; r < _rows; r++)
        for (int c = 0; c < _columns; c++)
        {
            _renderer.DrawCharacter(c, r, _charGrid[r, c],
                _fgGrid[r, c], _bgGrid[r, c], _styleGrid[r, c]);
        }
    }

    #endregion

    #region Word Wrapping

    private void FlushWithWordWrap()
    {
        int available = _columns - _lowerCursorCol;

        int breakAt = -1;
        int searchEnd = Math.Min(_lineBuffer.Count, available);
        for (int i = searchEnd - 1; i >= 0; i--)
        {
            if (_lineBuffer[i] == ' ')
            {
                breakAt = i;
                break;
            }
        }

        if (breakAt < 0)
        {
            if (_lowerCursorCol == 0)
            {
                breakAt = available;
                for (int i = 0; i < breakAt; i++)
                    PutCharAtLower(_lineBuffer[i]);
                _lineBuffer.RemoveRange(0, breakAt);
                _lowerCursorCol = 0;
                AdvanceLowerRow();
            }
            else
            {
                _lowerCursorCol = 0;
                AdvanceLowerRow();
                if (_lineBuffer.Count > _columns)
                    FlushWithWordWrap();
            }
            return;
        }

        for (int i = 0; i < breakAt; i++)
        {
            PutCharAtLower(_lineBuffer[i]);
            _lowerCursorCol++;
        }
        _lowerCursorCol = 0;
        AdvanceLowerRow();
        _lineBuffer.RemoveRange(0, breakAt + 1);

        if (_lineBuffer.Count > _columns)
            FlushWithWordWrap();
    }

    private void FlushLineBuffer()
    {
        for (int i = 0; i < _lineBuffer.Count; i++)
        {
            PutCharAtLower(_lineBuffer[i]);
            _lowerCursorCol++;
            if (_lowerCursorCol >= _columns)
            {
                _lowerCursorCol = 0;
                AdvanceLowerRow();
            }
        }
        _lineBuffer.Clear();
    }

    #endregion
}
