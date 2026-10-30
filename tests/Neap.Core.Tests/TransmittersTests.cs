using System.Text.Json;
using Neap.Core.Connection;
using Neap.Core.Protocol;

namespace Neap.Core.Tests;

public class TransmittersTests
{
    private static JsonElement Slot(string info, string control = "\"0\",\"100\",\"100\"") =>
        JsonDocument.Parse($"{{\"info\":[{info}],\"control\":[{control}]}}").RootElement;

    private static readonly JsonElement Dock = Slot(
        "\"2\",\"17\",\"1\",\"100\",\"1\",\"10F5\",\"229B\",\"4.107.703.0\",\"AA:BB:CC:DD:EE:FF\"",
        "\"0\",\"80\",\"30\"");

    private static readonly JsonElement Empty = Slot(
        "\"0\",\"0\",\"0\",\"0\",\"0\",\"0\",\"0\",\"\",\"00:00:00:00:00:00\"");

    [Fact]
    public void DescribesTheSlotInUse()
    {
        var dock = Transmitters.Describe(2, Dock);

        Assert.True(dock.Paired);
        Assert.True(dock.Active);
        Assert.Equal("Charging Dock", dock.Kind);
        Assert.Equal("229B", dock.ProductId);
        Assert.Equal("4.107.703.0", dock.Firmware);
    }

    [Fact]
    public void AnEmptySlotNamesNothing()
    {
        var empty = Transmitters.Describe(3, Empty);

        Assert.False(empty.Paired);
        Assert.False(empty.Active);
        Assert.Equal("", empty.Kind);
        Assert.Equal("", empty.Address);
        Assert.True(empty.Answered);
    }

    [Fact]
    public void ASlotWithNoAnswerIsNotKnownToBeEmpty() =>
        Assert.False(Transmitters.Describe(3, default).Answered);

    [Fact]
    public void ASlotWithNoAnswerIsNotUnreadable() =>
        Assert.False(Transmitters.Describe(3, default).Unreadable);

    [Fact]
    public void AnEmptySlotIsReadable() =>
        Assert.False(Transmitters.Describe(3, Empty).Unreadable);

    [Theory]
    [InlineData("\"garbled\"")]
    [InlineData("{\"control\":[\"0\",\"80\",\"30\"]}")]
    [InlineData("{\"info\":[\"2\",\"17\"]}")]
    public void ASlotThatDoesNotParseIsUnreadableNotEmpty(string reply)
    {
        var slot = Transmitters.Describe(2, JsonDocument.Parse(reply).RootElement);

        Assert.True(slot.Unreadable);
        Assert.True(slot.Answered);
        Assert.False(slot.Active);
    }

    [Fact]
    public void AnUnreadableSlotKeepsWhatWasKnownOfIt()
    {
        var known = new[] { Transmitters.Describe(2, Dock) };
        var read = new[]
        {
            Transmitters.Describe(1, Empty),
            Transmitters.Describe(2, JsonDocument.Parse("\"garbled\"").RootElement),
        };

        Assert.Equal(known, Transmitters.AfterRead(known, read));
    }

    [Fact]
    public void AReadableSlotReplacesWhatWasKnownOfIt()
    {
        var usb = Transmitters.Describe(1, Slot(
            "\"1\",\"33\",\"0\",\"0\",\"1\",\"10F5\",\"229D\",\"4.107.703.0\",\"11:22:33:44:55:66\""));
        var known = new[] { Transmitters.Describe(2, Dock) };
        var read = new[] { usb, Transmitters.Describe(2, Empty) };

        Assert.Equal([usb], Transmitters.AfterRead(known, read));
    }

    [Fact]
    public void AnUnknownProductSaysSo()
    {
        var odd = Transmitters.Describe(1, Slot(
            "\"1\",\"0\",\"0\",\"0\",\"0\",\"10F5\",\"1234\",\"1.0\",\"11:22:33:44:55:66\""));

        Assert.Equal("Unrecognised (1234)", odd.Kind);
    }

    [Fact]
    public void LightingComesFromTheSlotInUse()
    {
        var slots = new[] { Transmitters.Describe(1, Empty), Transmitters.Describe(2, Dock) };

        var lighting = Transmitters.Lighting(slots);

        Assert.Equal("80", lighting["401"]);
        Assert.Equal("30", lighting["402"]);
    }

    [Fact]
    public void TheDockCarriesItsSpareBattery()
    {
        var dock = Transmitters.Describe(1, Slot(
            "\"2\",\"17\",\"1\",\"57\",\"1\",\"10F5\",\"229B\",\"4.107.703.0\",\"AA:BB:CC:DD:EE:FF\""));

        Assert.Equal(new SpareReading(SpareState.InSlot, 57), dock.Spare);
    }

    [Fact]
    public void TheUsbTransmitterHasNoSpareBattery()
    {
        var transmitter = Transmitters.Describe(1, Slot(
            "\"2\",\"33\",\"0\",\"0\",\"1\",\"10F5\",\"229D\",\"4.107.703.0\",\"AA:BB:CC:DD:EE:FF\""));

        Assert.Null(transmitter.Spare);
    }

    [Fact]
    public void AnEmptySlotHasNoSpareBattery()
    {
        Assert.Null(Transmitters.Describe(1, Empty).Spare);
    }

    [Fact]
    public void APushedSlotIsReadAsThatSlot()
    {
        var evt = Event("TX2", "420", Dock.GetRawText());

        var slot = Transmitters.FromEvent(evt);

        Assert.NotNull(slot);
        Assert.Equal(2, slot.Slot);
        Assert.Equal("229B", slot.ProductId);
    }

    [Fact]
    public void APushedSlotThatDoesNotParseIsNotTakenForAnEmptyOne()
    {
        Assert.Null(Transmitters.FromEvent(Event("TX1", "400", "\"garbled\"")));
        Assert.Null(Transmitters.FromEvent(Event("TX1", "400", "{\"info\":[\"2\",\"17\"]}")));
    }

    [Fact]
    public void AnythingButASlotIsNotOne()
    {
        Assert.Null(Transmitters.FromEvent(Event("GSI", "240", "\"80\"")));
    }

    [Fact]
    public void ANewerRecordReplacesItsSlotAndKeepsTheOthers()
    {
        var usb = Transmitters.Describe(1, Slot(
            "\"1\",\"33\",\"0\",\"0\",\"1\",\"10F5\",\"229D\",\"4.107.703.0\",\"11:22:33:44:55:66\""));
        var known = new[] { usb, Transmitters.Describe(2, Dock) };
        var newer = Transmitters.Describe(2, Slot(
            "\"2\",\"17\",\"1\",\"255\",\"1\",\"10F5\",\"229B\",\"4.107.703.0\",\"AA:BB:CC:DD:EE:FF\""));

        var list = Transmitters.With(known, newer);

        Assert.Equal([1, 2], list.Select(t => t.Slot));
        Assert.Equal(SpareState.Empty, list[1].Spare?.State);
    }

    [Fact]
    public void ASlotThatEmptiedLeavesTheList()
    {
        var known = new[] { Transmitters.Describe(1, Dock) };

        Assert.Empty(Transmitters.With(known, Transmitters.Describe(1, Empty)));
    }

    private static DeviceEvent Event(string category, string key, string value) =>
        new("UP", category, new Dictionary<string, JsonElement> { [key] = JsonDocument.Parse(value).RootElement });

    [Theory]
    [InlineData((ushort)0x229B, Transmitters.Piece.Dock)]
    [InlineData((ushort)0x229D, Transmitters.Piece.Transmitter)]
    [InlineData((ushort)0x229E, Transmitters.Piece.Headset)]
    [InlineData((ushort)0x1234, Transmitters.Piece.Unknown)]
    public void KnowsEachPieceOfTheFamily(ushort product, Transmitters.Piece piece)
    {
        Assert.Equal(piece, Transmitters.PieceOf(product));
    }

    [Theory]
    [InlineData(1, 0x400)]
    [InlineData(2, 0x420)]
    [InlineData(3, 0x440)]
    [InlineData(4, 0x460)]
    public void EachSlotsRecordSitsInItsOwnBlock(int slot, int key)
    {
        Assert.Equal(key, Transmitters.BlockOf(slot));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void ASlotOutsideOneToFourHasNoBlock(int slot)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Transmitters.BlockOf(slot));
    }

    [Theory]
    [InlineData(0x401, 1, 0x401)]
    [InlineData(0x402, 1, 0x402)]
    [InlineData(0x401, 2, 0x421)]
    [InlineData(0x402, 2, 0x422)]
    [InlineData(0x401, 4, 0x461)]
    public void ALightWriteGoesToTheActiveSlotsBlock(int role, int activeSlot, int wire)
    {
        Assert.Equal(wire, Transmitters.LightingKeyFor(role, activeSlot));
    }

    [Theory]
    [InlineData(0x400, 1)]
    [InlineData(0x403, 1)]
    [InlineData(0x401, 0)]
    [InlineData(0x401, 5)]
    public void OnlyALightRoleInARealSlotHasAWireKey(int role, int activeSlot)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Transmitters.LightingKeyFor(role, activeSlot));
    }

    [Fact]
    public void ASlotsCategoryReadsAtItsBlock()
    {
        Assert.Equal(Transmitters.BlockOf(3), Verbs.TransmitterKey("TX3"));
    }
}
