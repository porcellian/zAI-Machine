namespace ZMachine.IO;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// User preferences persisted as JSON. Stores the selected theme,
/// CRT effect toggles, sound volume, and window geometry. Loaded
/// on startup and saved whenever a setting changes.
/// </summary>
/// <remarks>
/// Preferences file: ~/.zai-machine/preferences.json (or platform equivalent).
/// Missing or corrupt files produce defaults; partial JSON merges with defaults.
/// </remarks>
public class UserPreferences
{
    /// <summary>Display name of the selected theme.</summary>
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "C64 Classic";

    /// <summary>Whether CRT scanline effect is enabled.</summary>
    [JsonPropertyName("scanlines")]
    public bool Scanlines { get; set; }

    /// <summary>Whether CRT barrel distortion is enabled.</summary>
    [JsonPropertyName("crtCurvature")]
    public bool CrtCurvature { get; set; }

    /// <summary>Whether phosphor bloom/glow is enabled.</summary>
    [JsonPropertyName("phosphorBloom")]
    public bool PhosphorBloom { get; set; }

    /// <summary>Sound volume from 0 (mute) to 100 (max).</summary>
    [JsonPropertyName("soundVolume")]
    public int SoundVolume { get; set; } = 80;

    /// <summary>Window width in device-independent pixels.</summary>
    [JsonPropertyName("windowWidth")]
    public double WindowWidth { get; set; }

    /// <summary>Window height in device-independent pixels.</summary>
    [JsonPropertyName("windowHeight")]
    public double WindowHeight { get; set; }

    /// <summary>Window X position (0 = not set).</summary>
    [JsonPropertyName("windowX")]
    public double WindowX { get; set; }

    /// <summary>Window Y position (0 = not set).</summary>
    [JsonPropertyName("windowY")]
    public double WindowY { get; set; }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>
    /// Default preferences directory: ~/.zai-machine/
    /// </summary>
    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".zai-machine");

    /// <summary>
    /// Default preferences file path: ~/.zai-machine/preferences.json
    /// </summary>
    public static string DefaultPath =>
        Path.Combine(DefaultDirectory, "preferences.json");

    /// <summary>
    /// Loads preferences from a JSON file. Returns defaults if the file
    /// does not exist or cannot be parsed.
    /// </summary>
    public static UserPreferences Load(string? path = null)
    {
        path ??= DefaultPath;

        try
        {
            if (!File.Exists(path))
                return new UserPreferences();

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<UserPreferences>(json, _jsonOptions)
                   ?? new UserPreferences();
        }
        catch (JsonException)
        {
            return new UserPreferences();
        }
        catch (IOException)
        {
            return new UserPreferences();
        }
    }

    /// <summary>
    /// Saves preferences to a JSON file. Creates the directory if needed.
    /// </summary>
    public void Save(string? path = null)
    {
        path ??= DefaultPath;

        string? dir = Path.GetDirectoryName(path);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string json = JsonSerializer.Serialize(this, _jsonOptions);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Resolves the selected theme from the registry.
    /// Falls back to the default theme if the saved name is not found.
    /// </summary>
    public ITheme ResolveTheme()
    {
        return ThemeRegistry.GetByName(Theme) ?? ThemeRegistry.Default;
    }
}
