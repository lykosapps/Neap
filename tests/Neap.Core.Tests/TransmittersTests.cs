using System.Text.Json;

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

    [Theory]
    [InlineData((ushort)0x229B, Transmitters.Piece.Dock)]
    [InlineData((ushort)0x229D, Transmitters.Piece.Transmitter)]
    [InlineData((ushort)0x229E, Transmitters.Piece.Headset)]
    [InlineData((ushort)0x1234, Transmitters.Piece.Unknown)]
    public void KnowsEachPieceOfTheFamily(ushort product, Transmitters.Piece piece)
    {
        Assert.Equal(piece, Transmitters.PieceOf(product));
    }
}
