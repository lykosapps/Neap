using System.Reflection;

namespace StealthPro.App.Services;

/// <summary>The app's own name and version, from the build, so they are set in one place.</summary>
public static class AppInfo
{
    public static string Name { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "";

    public static Version? Version { get; } = typeof(AppInfo).Assembly.GetName().Version;
}
