using Avalonia;
using Avalonia.Media;

namespace Neap.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Before anything reads the app's folder: a pretend run keeps its own.
        Pretend.Separate();

        // Two copies would both open the headset's channel and take each
        // other's replies, which reads as a headset that is "Connecting" for
        // ever.
        using var one = OneCopy.Take();
        if (one is null)
        {
            // A launch at sign-in, or a pretend one kept behind, has nothing to show anyone.
            bool quiet = Startup.LaunchedAtLogin || Pretend.Behind;
            if (!quiet) OneCopy.AskToShow();
            AppLog.Write(quiet
                ? "started while already running: left the running copy alone"
                : "started again while running: showed the running copy instead");
            return 0;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) => AppLog.Write($"crashed: {e.ExceptionObject}");
        // Inter, carried by the app and the default for every window and
        // pop-up, so text looks the same on Windows and Linux.
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .With(new FontManagerOptions { DefaultFamilyName = "fonts:Inter#Inter" })
            .StartWithClassicDesktopLifetime(args);
    }
}
