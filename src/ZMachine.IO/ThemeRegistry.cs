namespace ZMachine.IO;

/// <summary>
/// Central registry of all available vintage display themes. Provides
/// theme enumeration for the selection UI and lookup by name for
/// preferences loading.
/// </summary>
/// <remarks>
/// ZSpec S8 — Each theme maps the Z-Machine screen model to a specific
/// vintage platform's display characteristics.
/// </remarks>
public static class ThemeRegistry
{
    private static readonly ITheme[] _themes =
    [
        new C64Theme(),
        new ModernC64Theme(),
        new AppleIITheme(),
        new DosGreenTheme(),
        new DosAmberTheme(),
        new DosColorTheme(),
        new AmigaTheme(),
    ];

    /// <summary>Returns all registered themes in display order.</summary>
    public static IReadOnlyList<ITheme> GetAll() => _themes;

    /// <summary>
    /// Finds a theme by its display name (case-insensitive).
    /// Returns null if no theme matches.
    /// </summary>
    public static ITheme? GetByName(string name)
    {
        foreach (var theme in _themes)
        {
            if (string.Equals(theme.Name, name, StringComparison.OrdinalIgnoreCase))
                return theme;
        }
        return null;
    }

    /// <summary>
    /// Returns the default theme used when no preference is set.
    /// </summary>
    public static ITheme Default => _themes[0];
}
