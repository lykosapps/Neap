using StealthPro.Core.Audio;
using StealthPro.Core.Connection;
using StealthPro.Core.Pretend;

namespace StealthPro.Core.Tests.Pretend;

public class PretendWindowsTests
{
    private static readonly HeadsetStatus Connected = new(
        Link.Connected, Route.ChargingDock, "Charging Dock", "", Product: "229B");

    [Fact]
    public void EveryRolePointsAtTheDockTheHeadsetIsOn()
    {
        var verdict = RoutingCheck.Judge(Connected,
            PretendWindows.Default(output: true), PretendWindows.Default(output: true),
            PretendWindows.Default(output: false), PretendWindows.Default(output: false),
            PretendWindows.Cable(), PretendWindows.Belonging);

        Assert.Empty(verdict!.Wrong);
        Assert.False(verdict.Cabled);
    }

    [Fact]
    public void OnlyTheDockHasDevices()
    {
        Assert.Equal([PretendWindows.OutputName], PretendWindows.Belonging("229B", output: true));
        Assert.Empty(PretendWindows.Belonging("229D", output: true));
    }

    [Fact]
    public void VolumeAndMuteAreKeptPerDevice()
    {
        var windows = new PretendWindows();

        windows.SetPercent(12, Flow.Output);
        windows.SetMuted(true, Flow.Input);

        Assert.Equal((12, false), (windows.Describe(Flow.Output).Percent, windows.Describe(Flow.Output).Muted));
        Assert.True(windows.Describe(Flow.Input).Muted);
        Assert.NotEqual(12, windows.Describe(Flow.Input).Percent);
    }

    [Fact]
    public void AnOfferedFormatIsAppliedAndAnotherIsRefused()
    {
        var windows = new PretendWindows();

        windows.ApplyFormat(24, 96000, Flow.Output);

        Assert.Equal(new AudioFormat(24, 96000, 2), windows.Formats(Flow.Output).Current);
        Assert.Throws<Core.Audio.FormatException>(() => windows.ApplyFormat(24, 96000, Flow.Input));
    }

    [Fact]
    public void TheMixRecordsWhatItAppliesOnlyWhileRunning()
    {
        var windows = new PretendWindows();
        var mix = windows.CreateMix(["PretendChat"]);

        mix.SetMix(70);
        Assert.Null(windows.Mix);

        mix.Start();
        mix.SetMix(130);
        Assert.Equal(100, windows.Mix);
        Assert.Equal((1, 2), (mix.Status.ChatSessions, mix.Status.GameSessions));

        mix.Dispose();
        Assert.Null(windows.Mix);
    }
}
