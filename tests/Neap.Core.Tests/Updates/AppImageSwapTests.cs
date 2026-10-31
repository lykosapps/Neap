using System.Runtime.Versioning;
using Neap.Core.Updates;

namespace Neap.Core.Tests.Updates;

public sealed class AppImageSwapTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("neap-appimage-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string AppImage => Path.Combine(_folder, "Neap-0.4.0-x86_64.AppImage");

    [Fact]
    public void TheDownloadArrivesHiddenBesideTheAppImage() =>
        Assert.Equal(Path.Combine(_folder, ".Neap-0.4.0-x86_64.AppImage.update"), AppImageSwap.IncomingFor(AppImage));

    [Fact]
    public void TheNewAppImageTakesTheOldOnesPlaceAndNothingIsLeft()
    {
        File.WriteAllText(AppImage, "old");
        string incoming = AppImageSwap.IncomingFor(AppImage);
        File.WriteAllText(incoming, "new");

        AppImageSwap.Swap(AppImage, incoming);

        Assert.Equal("new", File.ReadAllText(AppImage));
        Assert.False(File.Exists(incoming));
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void TheNewAppImageCanBeRunAsTheOldOneCould()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Windows keeps no Unix permissions");
        File.WriteAllText(AppImage, "old");
        File.SetUnixFileMode(AppImage, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        string incoming = AppImageSwap.IncomingFor(AppImage);
        File.WriteAllText(incoming, "new");

        AppImageSwap.Swap(AppImage, incoming);

        Assert.True(File.GetUnixFileMode(AppImage).HasFlag(UnixFileMode.UserExecute));
    }
}
