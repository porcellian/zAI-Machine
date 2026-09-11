namespace ZMachine.Tests;

using System.Text.Json;
using ZMachine.IO;

/// <summary>
/// Tests for Task 11.9 — Theme Selection UI and Preferences:
/// ThemeRegistry enumeration/lookup, UserPreferences JSON round-trip,
/// and theme switching through preferences.
/// </summary>
public class ThemeSelectionTests
{
    #region ThemeRegistry — Enumeration

    [Fact]
    public void GetAll_Returns7Themes()
    {
        var themes = ThemeRegistry.GetAll();
        Assert.Equal(7, themes.Count);
    }

    [Fact]
    public void GetAll_ContainsAllExpectedThemes()
    {
        var names = ThemeRegistry.GetAll().Select(t => t.Name).ToList();
        Assert.Contains("C64 Classic", names);
        Assert.Contains("Modern C64", names);
        Assert.Contains("Apple II", names);
        Assert.Contains("DOS Green", names);
        Assert.Contains("DOS Amber", names);
        Assert.Contains("DOS Color", names);
        Assert.Contains("Amiga Workbench", names);
    }

    [Fact]
    public void GetAll_AllImplementITheme()
    {
        foreach (var theme in ThemeRegistry.GetAll())
        {
            Assert.IsAssignableFrom<ITheme>(theme);
            Assert.NotNull(theme.Name);
            Assert.NotNull(theme.CreateConfig());
        }
    }

    [Fact]
    public void GetAll_AllProduceValidConfigs()
    {
        foreach (var theme in ThemeRegistry.GetAll())
        {
            var config = theme.CreateConfig();
            Assert.True(config.Columns > 0);
            Assert.True(config.Rows > 0);
            Assert.True(config.CharWidth > 0);
            Assert.True(config.CharHeight > 0);
            Assert.NotNull(config.ColorPalette);
            Assert.Equal(14, config.ColorPalette.Length);
        }
    }

    [Fact]
    public void Default_IsC64Classic()
    {
        Assert.Equal("C64 Classic", ThemeRegistry.Default.Name);
    }

    #endregion

    #region ThemeRegistry — Lookup

    [Theory]
    [InlineData("C64 Classic")]
    [InlineData("Modern C64")]
    [InlineData("Apple II")]
    [InlineData("DOS Green")]
    [InlineData("DOS Amber")]
    [InlineData("DOS Color")]
    [InlineData("Amiga Workbench")]
    public void GetByName_FindsTheme(string name)
    {
        var theme = ThemeRegistry.GetByName(name);
        Assert.NotNull(theme);
        Assert.Equal(name, theme!.Name);
    }

    [Fact]
    public void GetByName_CaseInsensitive()
    {
        Assert.NotNull(ThemeRegistry.GetByName("c64 classic"));
        Assert.NotNull(ThemeRegistry.GetByName("DOS GREEN"));
        Assert.NotNull(ThemeRegistry.GetByName("amiga workbench"));
    }

    [Fact]
    public void GetByName_ReturnsNull_ForUnknown()
    {
        Assert.Null(ThemeRegistry.GetByName("Nonexistent Theme"));
        Assert.Null(ThemeRegistry.GetByName(""));
    }

    #endregion

    #region UserPreferences — Defaults

    [Fact]
    public void NewPreferences_HasDefaults()
    {
        var prefs = new UserPreferences();
        Assert.Equal("C64 Classic", prefs.Theme);
        Assert.False(prefs.Scanlines);
        Assert.False(prefs.CrtCurvature);
        Assert.False(prefs.PhosphorBloom);
        Assert.Equal(80, prefs.SoundVolume);
        Assert.Equal(0, prefs.WindowWidth);
        Assert.Equal(0, prefs.WindowHeight);
    }

    #endregion

    #region UserPreferences — JSON Round-Trip

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        string path = Path.Combine(Path.GetTempPath(), $"zai-test-{Guid.NewGuid()}.json");
        try
        {
            var prefs = new UserPreferences
            {
                Theme = "DOS Color",
                Scanlines = true,
                CrtCurvature = true,
                PhosphorBloom = false,
                SoundVolume = 50,
                WindowWidth = 800,
                WindowHeight = 600,
                WindowX = 100,
                WindowY = 200,
            };

            prefs.Save(path);
            var loaded = UserPreferences.Load(path);

            Assert.Equal("DOS Color", loaded.Theme);
            Assert.True(loaded.Scanlines);
            Assert.True(loaded.CrtCurvature);
            Assert.False(loaded.PhosphorBloom);
            Assert.Equal(50, loaded.SoundVolume);
            Assert.Equal(800, loaded.WindowWidth);
            Assert.Equal(600, loaded.WindowHeight);
            Assert.Equal(100, loaded.WindowX);
            Assert.Equal(200, loaded.WindowY);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Save_CreatesDirectory()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"zai-test-dir-{Guid.NewGuid()}");
        string path = Path.Combine(dir, "preferences.json");
        try
        {
            new UserPreferences().Save(path);
            Assert.True(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(dir)) Directory.Delete(dir);
        }
    }

    [Fact]
    public void Save_WritesValidJson()
    {
        string path = Path.Combine(Path.GetTempPath(), $"zai-test-{Guid.NewGuid()}.json");
        try
        {
            new UserPreferences { Theme = "Apple II" }.Save(path);
            string json = File.ReadAllText(path);

            var doc = JsonDocument.Parse(json);
            Assert.Equal("Apple II", doc.RootElement.GetProperty("theme").GetString());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        string path = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}.json");
        var prefs = UserPreferences.Load(path);
        Assert.Equal("C64 Classic", prefs.Theme);
        Assert.Equal(80, prefs.SoundVolume);
    }

    [Fact]
    public void Load_CorruptJson_ReturnsDefaults()
    {
        string path = Path.Combine(Path.GetTempPath(), $"zai-test-{Guid.NewGuid()}.json");
        try
        {
            File.WriteAllText(path, "not valid json {{{}");
            var prefs = UserPreferences.Load(path);
            Assert.Equal("C64 Classic", prefs.Theme);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Load_PartialJson_MergesWithDefaults()
    {
        string path = Path.Combine(Path.GetTempPath(), $"zai-test-{Guid.NewGuid()}.json");
        try
        {
            File.WriteAllText(path, """{"theme": "DOS Amber"}""");
            var prefs = UserPreferences.Load(path);
            Assert.Equal("DOS Amber", prefs.Theme);
            Assert.Equal(80, prefs.SoundVolume);
            Assert.False(prefs.Scanlines);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    #endregion

    #region UserPreferences — Theme Resolution

    [Fact]
    public void ResolveTheme_FindsSavedTheme()
    {
        var prefs = new UserPreferences { Theme = "DOS Green" };
        var theme = prefs.ResolveTheme();
        Assert.Equal("DOS Green", theme.Name);
    }

    [Fact]
    public void ResolveTheme_FallsBackToDefault_ForUnknown()
    {
        var prefs = new UserPreferences { Theme = "Nonexistent" };
        var theme = prefs.ResolveTheme();
        Assert.Equal(ThemeRegistry.Default.Name, theme.Name);
    }

    [Theory]
    [InlineData("C64 Classic")]
    [InlineData("Modern C64")]
    [InlineData("Apple II")]
    [InlineData("DOS Green")]
    [InlineData("DOS Amber")]
    [InlineData("DOS Color")]
    [InlineData("Amiga Workbench")]
    public void ResolveTheme_AllThemes_Resolvable(string name)
    {
        var prefs = new UserPreferences { Theme = name };
        var theme = prefs.ResolveTheme();
        Assert.Equal(name, theme.Name);
    }

    #endregion

    #region Theme Switching — Renderer Reinit

    /// <summary>
    /// Switching themes reinitializes the renderer with different pixel
    /// dimensions matching the new theme's config.
    /// </summary>
    [Fact]
    public void ThemeSwitch_ChangesRendererDimensions()
    {
        using var renderer = new SkiaRenderer();

        var c64Config = new C64Theme().CreateConfig();
        renderer.Initialize(c64Config.PixelWidth, c64Config.PixelHeight, c64Config);
        Assert.Equal(c64Config.PixelWidth, renderer.BackBuffer!.Width);
        Assert.Equal(c64Config.PixelHeight, renderer.BackBuffer.Height);

        var dosConfig = new DosColorTheme().CreateConfig();
        renderer.Initialize(dosConfig.PixelWidth, dosConfig.PixelHeight, dosConfig);
        Assert.Equal(dosConfig.PixelWidth, renderer.BackBuffer!.Width);
        Assert.Equal(dosConfig.PixelHeight, renderer.BackBuffer.Height);
    }

    /// <summary>
    /// After switching from C64 to DOS Color theme, drawing uses the
    /// new theme's CGA colours, not the old theme's VIC-II palette.
    /// </summary>
    [Fact]
    public void ThemeSwitch_UsesNewPalette()
    {
        using var renderer = new SkiaRenderer();

        var c64Config = new C64Theme().CreateConfig();
        renderer.Initialize(c64Config.PixelWidth, c64Config.PixelHeight, c64Config);

        var dosConfig = new DosColorTheme().CreateConfig();
        renderer.Initialize(dosConfig.PixelWidth, dosConfig.PixelHeight, dosConfig);

        var fg = dosConfig.GetColor(dosConfig.DefaultForeground);
        var bg = dosConfig.GetColor(dosConfig.DefaultBackground);
        renderer.DrawCharacter(0, 0, 'X', fg, bg, 0);

        int x = dosConfig.BorderWidth;
        int y = dosConfig.BorderWidth;
        var pixel = renderer.BackBuffer!.GetPixel(x, y);
        Assert.True(pixel == fg || pixel == bg,
            "After theme switch, renderer should use new theme's colors");
    }

    /// <summary>
    /// GuiScreen reports correct dimensions after a theme switch.
    /// </summary>
    [Fact]
    public void ThemeSwitch_GuiScreen_ReportsDimensions()
    {
        using var renderer = new SkiaRenderer();

        var c64Config = new C64Theme().CreateConfig();
        renderer.Initialize(c64Config.PixelWidth, c64Config.PixelHeight, c64Config);
        var screen1 = new GuiScreen(renderer, c64Config);
        var (cols1, rows1) = screen1.GetScreenSize();
        Assert.Equal(40, cols1);
        Assert.Equal(25, rows1);

        var dosConfig = new DosColorTheme().CreateConfig();
        renderer.Initialize(dosConfig.PixelWidth, dosConfig.PixelHeight, dosConfig);
        var screen2 = new GuiScreen(renderer, dosConfig);
        var (cols2, rows2) = screen2.GetScreenSize();
        Assert.Equal(80, cols2);
        Assert.Equal(25, rows2);
    }

    #endregion

    #region Preferences — Full Workflow

    /// <summary>
    /// End-to-end: save preferences with a theme, load them, resolve the
    /// theme, create a config, and verify it matches the expected theme.
    /// </summary>
    [Fact]
    public void FullWorkflow_SaveLoadResolveCreateConfig()
    {
        string path = Path.Combine(Path.GetTempPath(), $"zai-test-{Guid.NewGuid()}.json");
        try
        {
            var prefs = new UserPreferences { Theme = "Amiga Workbench" };
            prefs.Save(path);

            var loaded = UserPreferences.Load(path);
            var theme = loaded.ResolveTheme();
            var config = theme.CreateConfig();

            Assert.Equal("Amiga Workbench", config.Name);
            Assert.Equal(80, config.Columns);
            Assert.Equal(25, config.Rows);
            Assert.NotNull(config.Font);
            Assert.Equal("Amiga 8×8", config.Font!.Name);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    #endregion
}
