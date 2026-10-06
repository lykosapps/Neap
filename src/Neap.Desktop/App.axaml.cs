using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Neap.Desktop.Localization;

namespace Neap.Desktop;

public class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // Before anything draws or starts: the words and the system underneath.
        Strings.Source = ResourceStrings.Get;
        Platform.Use(new DesktopPlatform());

        // Someone who has asked their system for high contrast gets its
        // colours, and gets them as they change while the app is open. A
        // pretend run can be shown in either theme whatever the system is set to.
        HighContrast.Install(this);
        ChooseTheme();
        if (PlatformSettings is { } settings) settings.ColorValuesChanged += (_, _) => ChooseTheme();

        AppLog.Write(Pretend.Active ? "started with the pretend headset" : Startup.LaunchedAtLogin ? "started at login" : "started");

        // If the app has been moved since starting at sign-in was switched on,
        // point the entry at where it is now. A pretend run is not the copy
        // the system should start.
        if (!Pretend.Active) Startup.Refresh();
        AppServices.Start();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            desktop.Exit += (_, _) => AppServices.Stop();
        }
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Picks high contrast, or the theme a pretend run asked for, or the system's own.</summary>
    private void ChooseTheme() => RequestedThemeVariant =
        HighContrast.Variant(this)
        ?? (Pretend.Theme is { } theme
            ? theme == PretendTheme.Dark ? ThemeVariant.Dark : ThemeVariant.Light
            : ThemeVariant.Default);
}
