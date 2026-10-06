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

        // A pretend run can be shown in either theme whatever the system is set to.
        if (Pretend.Theme is { } theme)
            RequestedThemeVariant = theme == PretendTheme.Dark ? ThemeVariant.Dark : ThemeVariant.Light;

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
}
