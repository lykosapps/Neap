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
    public void AWriteCheckedAsAnotherSettingIsSentWithinThatSettingsRange()
    {
        var headset = new PretendHeadset(atlas: true);
        using var client = new HeadsetClient(allowWrites: true, transport: headset.Open());

        client.Set(0x630, 65, checkedAs: 0x620);

        Assert.Equal(new PretendWrite(0x630, "65"), Assert.Single(headset.Writes));
    }

    [Theory]
    [InlineData(0x400, 1, 0x620)]   // a slot's base address, whatever it is checked as
    [InlineData(0x630, 101, 0x620)] // past the range it is checked against
    public void AWriteCheckedAsAnotherSettingIsStillRefusedWhereNotConfirmed(int key, int value, int checkedAs)
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(allowWrites: true, transport: headset.Open());

        Assert.Throws<ArgumentException>(() => client.Set(key, value, checkedAs));
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

    [Fact]
    public void AReadOfEveryCategoryNamesTheOnesThatGaveNoAnswer()
    {
        using var client = new HeadsetClient(transport: new PretendHeadset().Open());
        var unanswered = new List<(string Category, string? Heard)>();

        var values = client.ReadAll(Window, ["GSI", "NG"], unanswered);

        Assert.Contains("240", values.Keys);
        Assert.Equal([("NG", (string?)null)], unanswered);
    }

    [Fact]
    public void AReportHandedBackAgainWhileARequestWaitsIsTakenOnce()
    {
        using var client = new HeadsetClient(transport: new Repeating("{\"UP\":\"BT\",\"KVP\":{\"310\":\"-56\"}}"));

        Assert.Null(client.ReadInputs(TimeSpan.FromMilliseconds(500)));

        var kept = Assert.Single(client.ReadOnce());
        Assert.Equal("-56", kept.Values["310"].GetString());
    }

    [Fact]
    public void AReplyWhoseFirstPartIsHandedBackAgainIsStillRead()
    {
        var transmitter = new Scripted();
        transmitter.Answer(Fragments(LongReply, repeatFirst: 5));
        transmitter.Answer(Fragments("{\"OR\":\"GSI\",\"KVP\":{\"240\":\"72\"}}"));
        using var client = new HeadsetClient(transport: transmitter);

        Assert.Equal("50", client.ReadCategory("Prm", Slow)["c00"].GetString());
        Assert.Equal("72", client.ReadCategory("GSI", Slow)["240"].GetString());
    }

    [Fact]
    public void AReplyCutShortDoesNotStopTheNextOneBeingRead()
    {
        var transmitter = new Scripted();
        transmitter.Answer([Fragments(LongReply)[0]]);
        transmitter.Answer(Fragments("{\"OR\":\"GSI\",\"KVP\":{\"240\":\"72\"}}"));
        using var client = new HeadsetClient(transport: transmitter);

        Assert.Empty(client.ReadCategory("Prm", Window));
        Assert.Equal("72", client.ReadCategory("GSI", Slow)["240"].GetString());
    }

    /// <summary>Long enough for the reads of a few repeated reports however coarse the machine's sleeps.</summary>
    private static readonly TimeSpan Slow = TimeSpan.FromMilliseconds(1000);

    /// <summary>A reply that takes three reports, as the Atlas Air's Prm reply does.</summary>
    private const string LongReply =
        "{\"OR\":\"Prm\",\"KVP\":{\"c00\":\"50\",\"c10\":\"1\",\"c20\":\"2\",\"c30\":\"3\",\"c40\":\"4\",\"c50\":\"5\",\"c60\":\"6\",\"c70\":\"7\",\"c80\":\"8\"}}";

    /// <summary>A reply split into reports of at most 59 bytes, the first one given <paramref name="repeatFirst"/> more times, as an Atlas Air's transmitter hands back its last report while the next is on its way.</summary>
    private static List<byte[]> Fragments(string json, int repeatFirst = 0)
    {
        var bytes = Encoding.ASCII.GetBytes(json);
        var reports = new List<byte[]>();
        for (int at = 0; at < bytes.Length; at += 59)
        {
            var payload = bytes.AsSpan(at, Math.Min(59, bytes.Length - at));
            var report = new byte[62];
            report[0] = Core.Hid.HidControl.InReportId;
            report[1] = (byte)payload.Length;
            payload.CopyTo(report.AsSpan(3));
            reports.Add(report);
        }
        reports.InsertRange(1, Enumerable.Repeat(reports[0], repeatFirst));
        return reports;
    }

    /// <summary>A transmitter that answers each request in turn with the reports given for it, then empty reports.</summary>
    private sealed class Scripted : Core.Hid.IHidTransport
    {
        private readonly Queue<List<byte[]>> _answers = new();
        private readonly Queue<byte[]> _waiting = new();

        public ushort ProductId => 0x225E;

        public string Name => "";

        public void Answer(List<byte[]> reports) => _answers.Enqueue(reports);

        public string Describe() => "scripted";

        public void SendOutput(ReadOnlySpan<byte> report)
        {
            _waiting.Clear();
            if (_answers.TryDequeue(out var reports))
                foreach (var each in reports) _waiting.Enqueue((byte[])each.Clone());
        }

        public byte[] GetInput(byte reportId = Core.Hid.HidControl.InReportId) =>
            _waiting.TryDequeue(out var report) ? report : new byte[62];

        public void Dispose() { }
    }

    /// <summary>A transmitter that answers the reads after a request with the same notification, five times, as an Atlas Air's did while a request waited.</summary>
    /// <remarks>Few enough to be read within the wait however coarse the machine's sleeps: about 15 ms each on some Windows machines.</remarks>
    private sealed class Repeating(string json) : Core.Hid.IHidTransport
    {
        private readonly byte[] _report = Report(Encoding.ASCII.GetBytes(json));
        private int _left;

        public ushort ProductId => 0x225E;

        public string Name => "";

        public string Describe() => "repeating";

        public void SendOutput(ReadOnlySpan<byte> report) => _left = 5;

        public byte[] GetInput(byte reportId = Core.Hid.HidControl.InReportId) =>
            _left-- > 0 ? (byte[])_report.Clone() : new byte[_report.Length];

        public void Dispose() { }

        private static byte[] Report(byte[] payload)
        {
            var report = new byte[64];
            report[0] = Core.Hid.HidControl.InReportId;
            report[1] = (byte)payload.Length;
            payload.CopyTo(report, 3);
            return report;
        }
    }
}
