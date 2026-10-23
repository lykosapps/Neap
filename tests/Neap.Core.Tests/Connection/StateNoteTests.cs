using Neap.Core.Connection;

namespace Neap.Core.Tests.Connection;

public class StateNoteTests
{
    [Theory]
    [InlineData(Link.Connected, Route.ChargingDock, false, Note.None)]
    [InlineData(Link.Connected, Route.ChargingDock, true, Note.NoSound)]
    [InlineData(Link.Connected, Route.DirectUsb, false, Note.None)]
    [InlineData(Link.Silent, Route.UsbTransmitter, false, Note.Unreachable)]
    [InlineData(Link.Silent, Route.UsbTransmitter, true, Note.UnreachableNoSound)]
    [InlineData(Link.Quiet, Route.ChargingDock, false, Note.Off)]
    [InlineData(Link.Quiet, Route.UsbTransmitter, true, Note.Off)]
    [InlineData(Link.Quiet, Route.DirectUsb, false, Note.OffOnCable)]
    [InlineData(Link.Connecting, Route.Unknown, false, Note.None)]
    [InlineData(Link.Absent, Route.Unknown, false, Note.None)]
    public void EachStateHasOneNote(Link link, Route route, bool noSound, Note note) =>
        Assert.Equal(note, StateNote.For(new HeadsetStatus(link, route, "", "", NoSound: noSound)));

    [Theory]
    [InlineData(Route.UsbTransmitter)]
    [InlineData(Route.DirectUsb)]
    public void AStoppedTransmitterComesBeforeTheHeadsetBeingOff(Route route) =>
        Assert.Equal(Note.Stopped, StateNote.For(new HeadsetStatus(Link.Quiet, route, "", ""), ["229E", "225E"], stopped: true));

    [Fact]
    public void AStoppedTransmitterSaysNothingOnceTheHeadsetAnswers() =>
        Assert.Equal(Note.None, StateNote.For(new HeadsetStatus(Link.Connected, Route.UsbTransmitter, "", ""), stopped: true));

    [Fact]
    public void AHeadsetNeapOnlyReadsSaysSo() =>
        Assert.Equal(Note.ReadOnly, StateNote.For(new HeadsetStatus(Link.Connected, Route.ChargingDock, "", ""), readOnly: true));

    [Fact]
    public void NoSoundComesBeforeReadOnly() =>
        Assert.Equal(Note.NoSound,
            StateNote.For(new HeadsetStatus(Link.Connected, Route.ChargingDock, "", "", NoSound: true), readOnly: true));

    [Fact]
    public void WithOnlyItsCablePluggedInAQuietHeadsetIsOff() =>
        Assert.Equal(Note.OffOnCable, StateNote.For(new HeadsetStatus(Link.Quiet, Route.Unknown, "", ""), ["229E"]));

    [Fact]
    public void WithATransmitterBesideTheCableItMayBeOutOfRange() =>
        Assert.Equal(Note.Off, StateNote.For(new HeadsetStatus(Link.Quiet, Route.Unknown, "", ""), ["229E", "229B"]));

    [Fact]
    public void NotYetLookingAtWhatIsPluggedInChangesNothing() =>
        Assert.Equal(Note.Off, StateNote.For(new HeadsetStatus(Link.Quiet, Route.Unknown, "", ""), []));
}
