using Avalonia;

namespace ZMachine.App;

/// <summary>
/// Application entry point. Builds the Avalonia application host
/// and launches the main window.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
