using System.Text;

namespace StealthPro.Core.Tests;

public class HeadsetClientTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMilliseconds(200);

    [Fact]
    public void ReadsACategoryAndStopsAtItsReply()
    {
        var device = new FakeTransport();
        device.Answers["SGSI"] = ["{\"OR\":\"GSI\",\"KVP\":{\"240\":\"89\",\"220\":\"My Headset\"}}"];
        using var client = new HeadsetClient(transport: device);

        var values = client.ReadCategory("GSI", Window);

        Assert.Equal("89", values["240"].GetString());
        Assert.Equal("My Headset", values["220"].GetString());
        Assert.Equal("SGSI", Encoding.ASCII.GetString(Assert.Single(device.Sent)[23..]));
    }

    [Fact]
    public void OtherCategoriesArrivingFirstAreNotMistakenForTheReply()
    {
        var device = new FakeTransport();
        device.Answers["SSAF"] =
        [
            "{\"UP\":\"3DT\",\"KVP\":{\"510\":\"40\"}}",
            "{\"OR\":\"SAF\",\"KVP\":{\"750\":\"1\"}}",
        ];
        using var client = new HeadsetClient(transport: device);

        var values = client.ReadCategory("SAF", Window);

        Assert.Equal(["750"], values.Keys);
    }

    [Fact]
    public void AReplySpanningSeveralReportsArrivesWhole()
    {
        string bands = string.Join(",", Enumerable.Repeat("\"-30\"", 10));
        var device = new FakeTransport();
        device.Answers["SCG1"] =
            [$"{{\"OR\":\"CG1\",\"KVP\":{{\"1700\":{{\"name\":\"Mud cut\",\"bands\":[{bands}]}}}}}}"];
        using var client = new HeadsetClient(transport: device);

        var values = client.ReadCategory("CG1", Window);

        Assert.Equal(10, values["1700"].GetProperty("bands").GetArrayLength());
    }

    [Fact]
    public void AReadOnlyClientSendsNothing()
    {
        var device = new FakeTransport();
        using var client = new HeadsetClient(allowWrites: false, transport: device);

        Assert.Throws<WritesDisabledException>(() => client.Set(0x750, 1));
        Assert.Empty(device.Sent);
    }

    [Fact]
    public void AWriteSendsTheSetting()
    {
        var device = new FakeTransport();
        using var client = new HeadsetClient(allowWrites: true, transport: device);

        client.Set(0x760, 55);

        Assert.Equal("{\"0x760\":\"55\"}", Assert.Single(device.Writes));
    }

    [Theory]
    [InlineData(0x999)]  // not in the registry
    [InlineData(0x240)]  // battery: reported, not set
    [InlineData(0x400)]  // a transmitter slot's base address
    [InlineData(0x420)]
    [InlineData(0x423)]  // inside a slot, but not a brightness
    public void AWriteThatIsNotConfirmedIsRefused(int key)
    {
        var device = new FakeTransport();
        using var client = new HeadsetClient(allowWrites: true, transport: device);

        Assert.Throws<ArgumentException>(() => client.Set(key, 1));
        Assert.Empty(device.Sent);
    }

    [Theory]
    [InlineData(0x760, 101)]  // past its range
    [InlineData(0x750, 2)]    // a toggle
    [InlineData(0xA20, 0)]    // not one of the dial's options
    public void AValueOutsideTheSettingIsRefused(int key, int value)
    {
        var device = new FakeTransport();
        using var client = new HeadsetClient(allowWrites: true, transport: device);

        Assert.Throws<ArgumentException>(() => client.Set(key, value));
        Assert.Empty(device.Sent);
    }

    [Fact]
    public void EachTransmitterSlotsLightingCanBeSet()
    {
        var device = new FakeTransport();
        using var client = new HeadsetClient(allowWrites: true, transport: device);

        client.Set(0x422, 60);
        client.Set(0x461, 20);

        Assert.Equal(["{\"0x422\":\"60\"}", "{\"0x461\":\"20\"}"], device.Writes);
    }

    [Fact]
    public void DrainDiscardsWhatIsWaiting()
    {
        var device = new FakeTransport();
        device.Push("{\"UP\":\"GSI\",\"KVP\":{\"240\":\"88\"}}");
        using var client = new HeadsetClient(transport: device);

        Assert.Equal(1, client.Drain());
        Assert.Empty(client.ReadOnce());
    }
}
