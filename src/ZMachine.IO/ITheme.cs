namespace ZMachine.IO;

/// <summary>
/// Interface for vintage display themes. Each theme provides a
/// <see cref="ThemeConfig"/> defining colors, fonts, screen dimensions,
/// and visual effects for rendering the Z-Machine display.
/// </summary>
/// <remarks>
/// ZSpec S8 — The screen model's visual presentation is theme-dependent.
/// Themes map Z-Machine color numbers to platform-specific palettes and
/// provide bitmap fonts matching the original hardware character ROMs.
/// </remarks>
public interface ITheme
{
    /// <summary>Display name shown in the theme selection menu.</summary>
    string Name { get; }

    /// <summary>
    /// Creates the ThemeConfig for this theme. The config is consumed
    /// by <see cref="SkiaRenderer"/> to set up the back buffer, font,
    /// palette, and visual effects.
    /// </summary>
    ThemeConfig CreateConfig();
}
