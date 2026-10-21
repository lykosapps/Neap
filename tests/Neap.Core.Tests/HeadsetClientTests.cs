using System.Diagnostics;
using System.Text;
using Neap.Core.Pretend;

namespace Neap.Core.Tests;

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
    public void ANotificationArrivingDuringAReadIsKeptForTheNext()
    {
        var headset = new PretendHeadset();
        headset.Push("{\"UP\":\"3DT\",\"KVP\":{\"510\":\"40\"}}");
        using var client = new HeadsetClient(transport: headset.Open());

        client.ReadCategory("SAF", Window);

        var kept = Assert.Single(client.ReadOnce());
        Assert.Equal("3DT", kept.Category);
        Assert.Equal("40", kept.Values["510"].GetString());
    }

    [Fact]
    public void ASilentDeviceLeavesNothingToReport()
    {
        using var client = new HeadsetClient(transport: new PretendHeadset { On = false }.Open());

        Assert.Empty(client.ReadCategory("GSI", Window));
        Assert.Null(client.Unanswered);
    }

    [Fact]
    public void ASilentDeviceIsAskedNoFasterThanWindowsAsks()
    {
        var told = new List<Exception>();

        HeadsetClient.Behind(_ => false, out _, devices: new PretendHeadset { On = false },
            failed: (_, ex) => told.Add(ex), window: TimeSpan.FromMilliseconds(300));

        int reads = int.Parse(System.Text.RegularExpressions.Regex.Match(Assert.Single(told).Message, @"(\d+) reads").Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(reads, 1, 300 / HeadsetClient.ReadInterval.Milliseconds + 1);
    }

    [Fact]
    public void ADeviceThatOpensAndSaysNothingIsToldWithWhatWasTried()
    {
        var told = new List<Exception>();

        var client = HeadsetClient.Behind(_ => false, out _, devices: new PretendHeadset { On = false },
            failed: (_, ex) => told.Add(ex), window: TimeSpan.FromMilliseconds(100));

        Assert.Null(client);
        var silent = Assert.IsType<TimeoutException>(Assert.Single(told));
        Assert.Matches(@"gave no answer: \d+ reads of \d+ bytes with nothing in them, to a request of \d+ bytes", silent.Message);
    }

    [Fact]
    public void SomethingOtherThanTheReplyIsReportedRatherThanTakenForSilence()
    {
        var headset = new PretendHeadset { On = false };
        headset.Push("{\"UP\":\"3DT\",\"KVP\":{\"510\":\"40\"}}");
        using var client = new HeadsetClient(transport: headset.Open());

        Assert.Empty(client.ReadCategory("GSI", Window));
        string heard = Encoding.Latin1.GetString(Convert.FromHexString(client.Unanswered!));
        Assert.Contains("\"UP\":\"3DT\"", heard, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAnsweredReadReportsNothing()
    {
        var headset = new PretendHeadset();
        headset.Push("{\"UP\":\"3DT\",\"KVP\":{\"510\":\"40\"}}");
        using var client = new HeadsetClient(transport: headset.Open());

        Assert.NotEmpty(client.ReadCategory("GSI", Window));
        Assert.Null(client.Unanswered);
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
    public void EachWriteIsToldWithWhetherItWasSent()
    {
        using var client = new HeadsetClient(allowWrites: true, transport: new PretendHeadset().Open());
        var told = new List<WriteAttempt>();
        client.Wrote += told.Add;

        client.Set(0x760, 55);
        Assert.Throws<ArgumentException>(() => client.Set(0x760, 101));

        Assert.Equal(new WriteAttempt(0x760, "55", null), told[0]);
        Assert.Equal((0x760, "101"), (told[1].Key, told[1].Value));
        Assert.False(string.IsNullOrEmpty(told[1].Refused));
    }

    [Fact]
    public void AWriteFromAReadOnlyClientIsToldAsRefused()
    {
        using var client = new HeadsetClient(allowWrites: false, transport: new PretendHeadset().Open());
        var told = new List<WriteAttempt>();
        client.Wrote += told.Add;

        Assert.Throws<WritesDisabledException>(() => client.Set(0x750, 1));

        Assert.Contains("read-only", Assert.Single(told).Refused, StringComparison.Ordinal);
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
    public void AReadRightAfterAWriteWaitsBeforeItIsSent()
    {
        using var client = new HeadsetClient(allowWrites: true, transport: new PretendHeadset().Open());

        client.Set(0x760, 55);
        var clock = Stopwatch.StartNew();
        client.ReadCategory("GSI", Window);

        Assert.True(clock.Elapsed >= HeadsetClient.AfterWrite - TimeSpan.FromMilliseconds(5),
            $"the read went out {clock.ElapsedMilliseconds} ms after the write");
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
