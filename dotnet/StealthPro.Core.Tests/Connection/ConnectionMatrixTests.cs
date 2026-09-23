using StealthPro.Core.Connection;

namespace StealthPro.Core.Tests.Connection;

/// <summary>
/// Every arrangement of transmitters, settings, sound and power the app has
/// to put a name to, one test each, with what the header says.
/// </summary>
/// <remarks>
/// The same rows, as things to do with a headset, are the release walkthrough
/// in CONTRIBUTING.md. A new arrangement found on hardware gets a row in both.
/// </remarks>
public class ConnectionMatrixTests
{
    private const ushort Dock = 0x229B;
    private const ushort Transmitter = 0x229D;

    private static readonly LinkWords Words = new(
        "looking", "off", "unreachable", "unreachable, no sound", "off on cable",
        route => route.ToString());

    private TimeSpan _now;
    private bool _cabled;
    private LinkTracker _link;

    public ConnectionMatrixTests() => _link = New();

    private LinkTracker New() => new(Words, () => _now, () => _cabled);

    private void After(double seconds) => _now += TimeSpan.FromSeconds(seconds);

    private StatusLook Look => StatusLook.Of(_link.Status);

    // -- how the headset gets there ------------------------------------------

    private void OnTheDock()
    {
        _link.Answering(false, Route.ChargingDock, "Charging Dock", "Charging Dock", "d", "229B");
        _link.SoundLink(2);
        _link.Refresh();
    }

    private void SettingsOnTheDockSoundOnTheTransmitter()
    {
        _link.Answering(true, Route.ChargingDock, "Charging Dock", "USB Transmitter", "d", "229D");
        _link.SoundLink(2);
        _link.Refresh();
    }

    private void Unplugged(ushort gone, params ushort[] left)
    {
        _link.Forget();
        _link.Lost(gone, left, "lost");
        _link.Unreachable(left);
    }

    private void TurnedOff(params ushort[] plugged)
    {
        _link.Forget();
        _link.Unreachable(plugged);
    }

    // -- the rows --------------------------------------------------------------

    [Fact]
    public void SoundAndSettingsOnOneTransmitter()
    {
        OnTheDock();
        Assert.Equal(new StatusLook(Headline.Connected, Tone.Good), Look);
    }

    [Fact]
    public void SettingsOnOneTransmitterSoundOnTheOther()
    {
        SettingsOnTheDockSoundOnTheTransmitter();
        Assert.Equal(Headline.Connected, Look.Headline);
        Assert.Equal(Route.UsbTransmitter, _link.Status.Route);
        Assert.Equal("Charging Dock", _link.Status.ControlVia);
    }

    [Fact]
    public void TheTransmitterCarryingOnlyTheSoundUnplugged()
    {
        SettingsOnTheDockSoundOnTheTransmitter();
        _link.SoundLink(0);
        After(LinkTracker.SoundDropGrace.TotalSeconds);
        _link.Refresh();

        Assert.Equal(new StatusLook(Headline.NoSound, Tone.Caution), Look);
    }

    [Fact]
    public void TheTransmitterCarryingOnlyTheSettingsUnplugged()
    {
        SettingsOnTheDockSoundOnTheTransmitter();
        Unplugged(Dock, Transmitter);

        Assert.Equal(new StatusLook(Headline.SettingsUnavailable, Tone.Neutral), Look);
        Assert.Equal("unreachable", _link.Status.Detail);
    }

    [Fact]
    public void TheTransmitterCarryingBothUnplugged()
    {
        OnTheDock();
        Unplugged(Dock, Transmitter);

        Assert.Equal(new StatusLook(Headline.SettingsUnavailable, Tone.Caution), Look);
        Assert.Equal("unreachable, no sound", _link.Status.Detail);
    }

    [Fact]
    public void TheTransmitterCarryingBothUnpluggedWhileWindowsStillListsIt()
    {
        OnTheDock();
        Unplugged(Dock, Dock, Transmitter);
        _link.Unreachable([Transmitter]);

        Assert.Equal("unreachable, no sound", _link.Status.Detail);
    }

    [Fact]
    public void SwitchedOff()
    {
        OnTheDock();
        TurnedOff(Dock, Transmitter);

        Assert.Equal(new StatusLook(Headline.NotConnected, Tone.Caution), Look);
    }

    [Fact]
    public void SwitchedOffWhileTheOtherTransmitterAnswersFromMemory()
    {
        OnTheDock();
        TurnedOff(Dock, Transmitter);
        _link.SoundLink(0);
        _link.Answering(false, Route.UsbTransmitter, "USB Transmitter", "USB Transmitter", "t", "229D");

        Assert.Equal(Headline.NotConnected, Look.Headline);
    }

    [Fact]
    public void SwitchedOnThroughTheOtherTransmitter()
    {
        SwitchedOffWhileTheOtherTransmitterAnswersFromMemory();
        _link.SoundLink(2);
        _link.Refresh();

        Assert.Equal(Headline.Connected, Look.Headline);
        Assert.Equal(Route.UsbTransmitter, _link.Status.Route);
    }

    [Fact]
    public void EverythingUnplugged()
    {
        OnTheDock();
        _link.Lost(Dock, [], "nothing");
        After(LinkTracker.AbsentGrace.TotalSeconds);
        _link.Lost(null, [], "nothing");

        Assert.Equal(new StatusLook(Headline.NothingPluggedIn, Tone.Critical), Look);
    }

    [Fact]
    public void OnItsCable()
    {
        _cabled = true;
        _link.Answering(false, Route.DirectUsb, "Headset", "Headset", "h", "229E");

        Assert.Equal(Headline.Connected, Look.Headline);
        Assert.Equal(Route.DirectUsb, _link.Status.Route);
    }

    [Fact]
    public void SwitchedOffOnItsCable()
    {
        OnItsCable();
        _cabled = false;
        After(LinkTracker.OffGrace.TotalSeconds);
        _link.Cable(false, Route.DirectUsb, "Headset", "Headset", "h", "229E");
        After(LinkTracker.OffGrace.TotalSeconds);
        _link.Cable(false, Route.DirectUsb, "Headset", "Headset", "h", "229E");

        Assert.Equal(new StatusLook(Headline.HeadsetOff, Tone.Neutral), Look);
    }

    [Fact]
    public void RestartedWithTheSettingsOnAnUnpluggedTransmitter()
    {
        _link = New();
        _link.SettingsLeftEarlierWith(Dock);
        _link.Unreachable([Transmitter]);

        Assert.Equal(Headline.SettingsUnavailable, Look.Headline);
    }

    [Fact]
    public void RestartedWithThatTransmitterBack()
    {
        _link = New();
        _link.SettingsLeftEarlierWith(Dock);
        _link.Unreachable([Dock, Transmitter]);

        Assert.Equal(Headline.NotConnected, Look.Headline);
    }

    [Fact]
    public void StartingWithNothingAnswering()
    {
        _link.Unreachable([Dock]);

        Assert.Equal(Headline.NotConnected, Look.Headline);
    }
}
