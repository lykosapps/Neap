using StealthPro.Core.Audio;
using StealthPro.Core.Connection;

namespace StealthPro.Core.Tests.Connection;

public class RoutingCheckTests
{
    private const string Dock = "229B";
    private const string Transmitter = "229D";
    private const string Headset = "229E";

    private static readonly Routed OnDock = new("dock", Dock);
    private static readonly Routed OnTransmitter = new("transmitter", Transmitter);
    private static readonly Routed OnCable = new("cable", Headset);
    private static readonly Routed OnSpeakers = new("speakers", "");

    private static IReadOnlyList<string> Devices(string product, bool output) =>
        [$"{(output ? "Speakers" : "Microphone")} ({product})"];

    private static HeadsetStatus On(string product, Link link = Link.Connected, bool noSound = false) =>
        new(link, Route.ChargingHub, "Charging Dock", "", product, NoSound: noSound);

    private static RoutingVerdict? Judge(HeadsetStatus status, Routed? output, Routed? calls = null,
        Routed? input = null, Routed? callsInput = null, string cable = "",
        bool sound = true, bool microphone = true) =>
        RoutingCheck.Judge(status, output, calls, input, callsInput, cable, Devices, sound, microphone);

    [Fact]
    public void NothingToJudgeWithoutSoundOrATransmitter()
    {
        Assert.Null(Judge(On(Dock, noSound: true), OnTransmitter));
        Assert.Null(Judge(On(""), OnTransmitter));
        Assert.Null(Judge(On(Dock, Link.Quiet), OnTransmitter));
    }

    [Fact]
    public void EverythingOnTheRightTransmitterIsFine()
    {
        var verdict = Judge(On(Dock), OnDock, OnDock, OnDock, OnDock);

        Assert.NotNull(verdict);
        Assert.Empty(verdict.Wrong);
    }

    [Fact]
    public void SoundOnTheOtherTransmitterIsWrongAndSaysWhatToPick()
    {
        var wrong = Assert.Single(Judge(On(Dock), OnTransmitter)!.Wrong);

        Assert.Equal(AudioRole.Sound, wrong.Role);
        Assert.Equal("USB Transmitter", wrong.OnName);
        Assert.Equal($"Speakers ({Dock})", wrong.Pick);
    }

    [Fact]
    public void DevicesThatAreNotTheHeadsetsAreLeftAlone()
    {
        Assert.Empty(Judge(On(Dock), OnSpeakers, input: OnSpeakers)!.Wrong);
    }

    [Fact]
    public void WithTheCableInTheCableIsTheOnlyRightPlace()
    {
        var verdict = Judge(On(Dock), OnDock, input: OnCable, cable: Headset);

        Assert.True(verdict!.Cabled);
        Assert.Equal(AudioRole.Sound, Assert.Single(verdict.Wrong).Role);
    }

    [Fact]
    public void CrossPlayFixesItOnlyWhenEverythingIsOnTheOtherTransmitter()
    {
        Assert.True(Judge(On(Dock), OnTransmitter, OnTransmitter, OnTransmitter, OnTransmitter)!.CrossPlayFixes);
        Assert.False(Judge(On(Dock), OnTransmitter, OnDock, OnDock, OnDock)!.CrossPlayFixes);
    }

    [Fact]
    public void APlacementShowsOnlyItsOwnHalvesButCrossPlayCountsThemAll()
    {
        var verdict = Judge(On(Dock), OnTransmitter, input: OnDock, sound: false);

        Assert.Empty(verdict!.Wrong);

        var mic = Judge(On(Dock), OnDock, input: OnTransmitter, sound: false);
        Assert.Equal(AudioRole.Microphone, Assert.Single(mic!.Wrong).Role);
        Assert.False(mic.CrossPlayFixes);
    }
}
