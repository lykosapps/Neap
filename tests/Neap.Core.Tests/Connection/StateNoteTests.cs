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
