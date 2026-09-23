using StealthPro.Core.Connection;

namespace StealthPro.Core.Tests.Connection;

public class TransmitterListTests
{
    private const string Dock = "229B";
    private const string Transmitter = "229D";

    private static Transmitter Slot(int slot, string product, string kind) =>
        new(slot, true, false, kind, product, "10F5", "4.107.703.0", "AA:BB:CC:DD:EE:FF", [], []);

    private static readonly Transmitter[] Known =
    [
        Slot(1, Transmitter, "USB Transmitter"),
        Slot(2, Dock, "Charging Dock"),
    ];

    private static HeadsetStatus Connected(string product, bool noSound = false) =>
        new(Link.Connected, Route.ChargingDock, "", "", product, NoSound: noSound);

    private static TransmitterState StateOf(IReadOnlyList<TransmitterRow> rows, string name) =>
        rows.Single(r => r.Name == name).State;

    [Fact]
    public void TheOneCarryingTheSoundIsInUseAndListedFirst()
    {
        var rows = TransmitterList.Rows(Connected(Dock), Known, [Dock, Transmitter], cable: false);

        Assert.Equal("Charging Dock", rows[0].Name);
        Assert.Equal(TransmitterState.InUse, rows[0].State);
        Assert.Equal(TransmitterState.CanSwitchTo, StateOf(rows, "USB Transmitter"));
    }

    [Fact]
    public void SelectedButUnpluggedIsSaidPlainly()
    {
        var rows = TransmitterList.Rows(Connected(Transmitter), Known, [Dock], cable: false);

        Assert.Equal(TransmitterState.SelectedButUnplugged, StateOf(rows, "USB Transmitter"));
    }

    [Fact]
    public void OverTheCableNoneIsInUseOrOfferedToSwitchTo()
    {
        var rows = TransmitterList.Rows(Connected(Dock), Known, [Dock, Transmitter], cable: true);

        Assert.All(rows, r => Assert.Equal(TransmitterState.PluggedIn, r.State));
    }

    [Fact]
    public void WithNoSoundNoneIsInUse()
    {
        var rows = TransmitterList.Rows(Connected(Dock, noSound: true), Known, [Dock], cable: false);

        Assert.DoesNotContain(rows, r => r.State == TransmitterState.InUse);
    }

    [Fact]
    public void APluggedInTransmitterTheHeadsetHasNotReportedIsStillListed()
    {
        var rows = TransmitterList.Rows(Connected(Dock), [Slot(1, Dock, "Charging Dock")],
            [Dock, Transmitter], cable: false);

        Assert.Equal(TransmitterState.CanSwitchTo, StateOf(rows, "USB Transmitter"));
        Assert.Equal("", rows.Single(r => r.Name == "USB Transmitter").Firmware);
    }

    [Fact]
    public void KnownButNotPluggedInIsSaidSo()
    {
        var quiet = new HeadsetStatus(Link.Quiet, Route.ChargingDock, "", "");

        var rows = TransmitterList.Rows(quiet, Known, [Dock], cable: false);

        Assert.Equal(TransmitterState.NotPluggedIn, StateOf(rows, "USB Transmitter"));
        Assert.Equal(TransmitterState.PluggedIn, StateOf(rows, "Charging Dock"));
    }
}
