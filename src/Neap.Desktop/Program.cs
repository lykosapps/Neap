using Avalonia;

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
            AppLog.Write(Pretend.Active
                ? "started while a pretend copy was already running: left it alone"
                : "started while already running: left the running copy alone");
            return 0;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) => AppLog.Write($"crashed: {e.ExceptionObject}");
        return AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
    }
}
