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

        AppLog.Write(Pretend.Active ? "started with the pretend headset" : "started");
        AppServices.Start();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            desktop.Exit += (_, _) => AppServices.Stop();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
