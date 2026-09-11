using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ZMachine.Core;
using ZMachine.IO;

namespace ZMachine.App;

/// <summary>
/// Main application window hosting the Z-Machine display canvas, native
/// menu bar, and keyboard input handling. Runs the interpreter on a
/// background thread while feeding input from the GUI thread.
/// </summary>
public partial class MainWindow : Window
{
    private Interpreter? _interpreter;
    private SkiaRenderer? _renderer;
    private GuiScreen? _guiScreen;
    private GuiInputStream? _guiInputStream;
    private Thread? _interpreterThread;
    private UserPreferences _preferences = new();
    private ITheme _currentTheme = ThemeRegistry.Default;
    private string? _currentStoryPath;

    // Line-input mode state
    private bool _lineInputMode;
    private readonly List<char> _lineBuffer = new();

    public MainWindow()
    {
        InitializeComponent();
        Focusable = true;
        KeyDown += OnKeyDown;
        TextInput += OnTextInput;

        _preferences = UserPreferences.Load();
        _currentTheme = _preferences.ResolveTheme();
        BuildThemeMenu();
    }

    /// <summary>
    /// Opens a file picker for .z3/.z4/.z5/.z8/.blorb/.zblorb files,
    /// then loads and runs the selected story.
    /// </summary>
    private async void OnOpenStory(object? sender, EventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Story File",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Z-Machine Stories")
                {
                    Patterns = ["*.z1", "*.z2", "*.z3", "*.z4", "*.z5",
                                "*.z6", "*.z7", "*.z8",
                                "*.blorb", "*.zblorb", "*.blb", "*.zlb"]
                },
                FilePickerFileTypes.All
            ]
        });

        if (files.Count > 0)
        {
            string path = files[0].Path.LocalPath;
            LoadAndRunStory(path);
        }
    }

    /// <summary>
    /// Opens a file picker for Blorb resource files to attach to a running story.
    /// </summary>
    private async void OnOpenBlorb(object? sender, EventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Blorb Resource File",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Blorb Resource Files")
                {
                    Patterns = ["*.blorb", "*.zblorb", "*.blb", "*.zlb"]
                },
                FilePickerFileTypes.All
            ]
        });

        if (files.Count > 0)
        {
            // Blorb loading with existing story will be wired in later tasks
            string path = files[0].Path.LocalPath;
            Title = $"zAI-Machine — Blorb: {Path.GetFileName(path)}";
        }
    }

    private void OnQuit(object? sender, EventArgs e)
    {
        Close();
    }

    private async void OnAbout(object? sender, EventArgs e)
    {
        var dialog = new Window
        {
            Title = "About zAI-Machine",
            Width = 350,
            Height = 180,
            CanResize = false,
            Content = new TextBlock
            {
                Text = "zAI-Machine\n\n" +
                       "Z-Machine Standard 1.1 Interpreter\n" +
                       "Quetzal 1.4 | Blorb 2.0.4\n\n" +
                       "Versions 1–8 supported",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                TextAlignment = Avalonia.Media.TextAlignment.Center
            }
        };

        await dialog.ShowDialog(this);
    }

    private NativeMenu? _themeMenu;

    /// <summary>
    /// Builds the Options → Theme submenu dynamically from ThemeRegistry.
    /// Inserts theme items into the Options menu found via the NativeMenu
    /// attached property. Each theme gets a menu item; the current theme
    /// is marked with a bullet prefix.
    /// </summary>
    private void BuildThemeMenu()
    {
        var topMenu = NativeMenu.GetMenu(this);
        if (topMenu == null) return;

        NativeMenuItem? optionsItem = null;
        foreach (var item in topMenu.Items)
        {
            if (item is NativeMenuItem mi && mi.Header == "Options")
            {
                optionsItem = mi;
                break;
            }
        }
        if (optionsItem?.Menu == null) return;

        var themeSubmenu = new NativeMenuItem("Theme");
        _themeMenu = new NativeMenu();

        foreach (var theme in ThemeRegistry.GetAll())
        {
            string label = theme.Name == _currentTheme.Name
                ? $"● {theme.Name}"
                : $"  {theme.Name}";

            var menuItem = new NativeMenuItem(label);
            string themeName = theme.Name;
            menuItem.Click += (_, _) => SwitchTheme(themeName);
            _themeMenu.Items.Add(menuItem);
        }

        themeSubmenu.Menu = _themeMenu;
        optionsItem.Menu.Items.Insert(0, themeSubmenu);
        optionsItem.Menu.Items.Insert(1, new NativeMenuItemSeparator());
    }

    /// <summary>
    /// Switches to a new theme by name. Reinitializes the renderer with
    /// the new theme's config and redraws the current screen contents.
    /// Saves the preference to disk.
    /// </summary>
    internal void SwitchTheme(string themeName)
    {
        var theme = ThemeRegistry.GetByName(themeName);
        if (theme == null) return;

        _currentTheme = theme;
        _preferences.Theme = themeName;
        _preferences.Save();

        if (_renderer != null)
        {
            var config = theme.CreateConfig();
            _renderer.Initialize(config.PixelWidth, config.PixelHeight, config);
            _guiScreen?.ForceRefresh();
            GameCanvas.InvalidateVisual();
        }

        UpdateThemeMenuChecks();
    }

    private void UpdateThemeMenuChecks()
    {
        if (_themeMenu == null) return;

        var themes = ThemeRegistry.GetAll();
        for (int i = 0; i < _themeMenu.Items.Count && i < themes.Count; i++)
        {
            if (_themeMenu.Items[i] is NativeMenuItem mi)
            {
                mi.Header = themes[i].Name == _currentTheme.Name
                    ? $"● {themes[i].Name}"
                    : $"  {themes[i].Name}";
            }
        }
    }

    private void LoadAndRunStory(string path)
    {
        StopInterpreter();

        _currentStoryPath = path;
        _renderer = new SkiaRenderer();
        var config = _currentTheme.CreateConfig();

        _renderer.Initialize(config.PixelWidth, config.PixelHeight, config);
        _guiScreen = new GuiScreen(_renderer, config);
        _guiInputStream = new GuiInputStream();

        GameCanvas.Attach(_renderer);

        _interpreter = new Interpreter();

        Title = $"zAI-Machine — {Path.GetFileName(path)}";

        _interpreterThread = new Thread(() =>
        {
            try
            {
                _interpreter.Load(path, _guiInputStream, _guiScreen);
                _guiScreen.ForceRefresh();
                _interpreter.Run();
            }
            catch (Exception ex)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    Title = $"zAI-Machine — Error: {ex.Message}";
                });
            }
        })
        {
            IsBackground = true,
            Name = "Z-Machine"
        };

        _lineInputMode = true;
        _interpreterThread.Start();
    }

    private void StopInterpreter()
    {
        _interpreter = null;
        _interpreterThread = null;
        _lineBuffer.Clear();
    }

    /// <summary>
    /// Handles special keys (Enter, Backspace, arrows, function keys)
    /// and maps them to ZSCII codes for the input stream.
    /// ZSpec S10.5 — Cursor/function key mappings.
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_guiInputStream == null) return;

        switch (e.Key)
        {
            case Key.Return:
                if (_lineInputMode)
                {
                    string text = new string(_lineBuffer.ToArray());
                    _guiScreen?.Print(text);
                    _guiScreen?.NewLine();
                    _guiScreen?.ForceRefresh();
                    _guiInputStream.EnqueueLine(text);
                    _lineBuffer.Clear();
                }
                else
                {
                    _guiInputStream.EnqueueChar(13);
                }
                e.Handled = true;
                break;

            case Key.Back:
                if (_lineInputMode && _lineBuffer.Count > 0)
                {
                    _lineBuffer.RemoveAt(_lineBuffer.Count - 1);
                }
                else
                {
                    _guiInputStream.EnqueueChar(8);
                }
                e.Handled = true;
                break;

            case Key.Escape:
                _guiInputStream.EnqueueChar(27);
                e.Handled = true;
                break;

            case Key.Up:
                _guiInputStream.EnqueueChar(129);
                e.Handled = true;
                break;
            case Key.Down:
                _guiInputStream.EnqueueChar(130);
                e.Handled = true;
                break;
            case Key.Left:
                _guiInputStream.EnqueueChar(131);
                e.Handled = true;
                break;
            case Key.Right:
                _guiInputStream.EnqueueChar(132);
                e.Handled = true;
                break;

            case Key.F1: _guiInputStream.EnqueueChar(133); e.Handled = true; break;
            case Key.F2: _guiInputStream.EnqueueChar(134); e.Handled = true; break;
            case Key.F3: _guiInputStream.EnqueueChar(135); e.Handled = true; break;
            case Key.F4: _guiInputStream.EnqueueChar(136); e.Handled = true; break;
            case Key.F5: _guiInputStream.EnqueueChar(137); e.Handled = true; break;
            case Key.F6: _guiInputStream.EnqueueChar(138); e.Handled = true; break;
            case Key.F7: _guiInputStream.EnqueueChar(139); e.Handled = true; break;
            case Key.F8: _guiInputStream.EnqueueChar(140); e.Handled = true; break;
            case Key.F9: _guiInputStream.EnqueueChar(141); e.Handled = true; break;
            case Key.F10: _guiInputStream.EnqueueChar(142); e.Handled = true; break;
            case Key.F11: _guiInputStream.EnqueueChar(143); e.Handled = true; break;
            case Key.F12: _guiInputStream.EnqueueChar(144); e.Handled = true; break;
        }
    }

    /// <summary>
    /// Handles printable character input from Avalonia's TextInput event.
    /// In line-input mode, characters accumulate in the line buffer.
    /// </summary>
    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (_guiInputStream == null || string.IsNullOrEmpty(e.Text)) return;

        foreach (char c in e.Text)
        {
            if (c >= 32 && c <= 126)
            {
                if (_lineInputMode)
                {
                    _lineBuffer.Add(c);
                }
                else
                {
                    _guiInputStream.EnqueueChar(c);
                }
            }
        }
    }
}
