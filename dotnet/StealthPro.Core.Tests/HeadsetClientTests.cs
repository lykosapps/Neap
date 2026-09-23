using System.Text;
using StealthPro.Core.Pretend;

namespace StealthPro.Core.Tests;

public class HeadsetClientTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMilliseconds(200);

    [Fact]
    public void ReadsACategoryAndStopsAtItsReply()
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(transport: headset.Open());

        var values = client.ReadCategory("GSI", Window);

        Assert.Equal("76", values["240"].GetString());
        Assert.Equal(PretendHeadset.HeadsetName, values["220"].GetString());
        Assert.Equal("SGSI", Encoding.ASCII.GetString(Assert.Single(headset.Sent)[23..]));
    }

    [Fact]
    public void OtherCategoriesArrivingFirstAreNotMistakenForTheReply()
    {
        var headset = new PretendHeadset();
        headset.Push("{\"UP\":\"3DT\",\"KVP\":{\"510\":\"40\"}}");
        using var client = new HeadsetClient(transport: headset.Open());

        var values = client.ReadCategory("SAF", Window);

        Assert.Contains("750", values.Keys);
        Assert.DoesNotContain("510", values.Keys);
    }

    [Fact]
    public void AReplySpanningSeveralReportsArrivesWhole()
    {
        using var client = new HeadsetClient(transport: new PretendHeadset().Open());

        var values = client.ReadCategory("CG1", Window);

        Assert.Equal(10, values["1700"].GetProperty("bands").GetArrayLength());
    }

    [Fact]
    public void AReadOnlyClientSendsNothing()
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(allowWrites: false, transport: headset.Open());

        Assert.Throws<WritesDisabledException>(() => client.Set(0x750, 1));
        Assert.Empty(headset.Sent);
    }

    [Fact]
    public void AWriteSendsTheSetting()
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(allowWrites: true, transport: headset.Open());

        client.Set(0x760, 55);

        Assert.Equal(new PretendWrite(0x760, "55"), Assert.Single(headset.Writes));
    }

    [Theory]
    [InlineData(0x999)]  // not in the registry
    [InlineData(0x240)]  // battery: reported, not set
    [InlineData(0x400)]  // a transmitter slot's base address
    [InlineData(0x420)]
    [InlineData(0x423)]  // inside a slot, but not a brightness
    public void AWriteThatIsNotConfirmedIsRefused(int key)
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(allowWrites: true, transport: headset.Open());

        Assert.Throws<ArgumentException>(() => client.Set(key, 1));
        Assert.Empty(headset.Sent);
    }

    [Theory]
    [InlineData(0x760, 101)]  // past its range
    [InlineData(0x750, 2)]    // a toggle
    [InlineData(0xA20, 0)]    // not one of the dial's options
    public void AValueOutsideTheSettingIsRefused(int key, int value)
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(allowWrites: true, transport: headset.Open());

        Assert.Throws<ArgumentException>(() => client.Set(key, value));
        Assert.Empty(headset.Sent);
    }

    [Fact]
    public void EachTransmitterSlotsLightingCanBeSet()
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(allowWrites: true, transport: headset.Open());

        client.Set(0x422, 60);
        client.Set(0x461, 20);

        Assert.Equal([new PretendWrite(0x422, "60"), new PretendWrite(0x461, "20")], headset.Writes);
    }

    [Fact]
    public void DrainDiscardsWhatIsWaiting()
    {
        var headset = new PretendHeadset();
        headset.Push("{\"UP\":\"GSI\",\"KVP\":{\"240\":\"88\"}}");
        using var client = new HeadsetClient(transport: headset.Open());

        Assert.Equal(1, client.Drain());
        Assert.Empty(client.ReadOnce());
    }
}
