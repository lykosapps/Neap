using Neap.Core.Hid;
using Neap.Core.Tests.Resources;

namespace Neap.Core.Tests.Hid;

public class UdevRuleTests
{
    private static readonly string Packaged = Path.Combine(AppSource.RepoRoot(), "packaging", "linux", UdevRule.FileName);

    [Fact]
    public void ThePackagedRuleIsTheOneTheAppInstalls()
    {
        string expected = string.Join('\n', UdevRule.Lines) + "\n";
        Assert.Equal(expected, File.ReadAllText(Packaged).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void TheRuleNamesTurtleBeachsVendorIdAndNothingWider()
    {
        string rule = UdevRule.Lines.Single(line => !line.StartsWith('#'));

        Assert.Contains($"ATTRS{{idVendor}}==\"{HidControl.VendorId:x4}\"", rule, StringComparison.Ordinal);
        Assert.Contains("SUBSYSTEM==\"hidraw\"", rule, StringComparison.Ordinal);
        Assert.Contains("TAG+=\"uaccess\"", rule, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCommandsWriteTheRuleToTheFolderTheSystemReads()
    {
        Assert.Contains($"{UdevRule.Folder}/{UdevRule.FileName}", UdevRule.RootCommand, StringComparison.Ordinal);
        Assert.Contains("udevadm control --reload-rules", UdevRule.RootCommand, StringComparison.Ordinal);
        Assert.Contains("udevadm trigger --subsystem-match=hidraw", UdevRule.RootCommand, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCommandWritesTheRuleExactlyWhenRunByAShell()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "only a Linux shell runs it");
        string folder = Directory.CreateTempSubdirectory("neap-udev").FullName;
        try
        {
            var shell = System.Diagnostics.Process.Start("sh", ["-c", UdevRule.RootCommandFor(folder)])!;
            shell.WaitForExit();

            // What follows the writing is the system's own tool, which a test cannot run; the file is what matters here.
            Assert.Equal(string.Join('\n', UdevRule.Lines) + "\n", File.ReadAllText(Path.Combine(folder, UdevRule.FileName)));
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public void ADeviceTheSystemWillNotOpenIsToldApartFromOneThatIsAbsent()
    {
        var refused = new List<Exception>();
        var source = new Refusing();

        var client = HeadsetClient.Behind(allowWrites: false, out int present, null, source, (_, ex) => refused.Add(ex));

        Assert.Null(client);
        Assert.Equal(1, present);
        Assert.IsType<AccessDeniedException>(Assert.Single(refused));
    }

    /// <summary>One headset that is there, and that the system will not open.</summary>
    private sealed class Refusing : IDeviceSource
    {
        public IReadOnlyList<HidDeviceInfo> Candidates() =>
            [new HidDeviceInfo("/dev/hidraw3", HidControl.VendorId, 0x229B, HidControl.UsagePage, 64, 0, 64)];

        public IHidTransport Open(HidDeviceInfo device) =>
            throw new AccessDeniedException($"no permission to open {device.Path}");
    }
}
