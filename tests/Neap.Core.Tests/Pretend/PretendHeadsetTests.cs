using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Neap.Core.Connection;
using Neap.Core.Hid;
using Neap.Core.Presets;
using Neap.Core.Pretend;
using Neap.Core.Protocol;
using Neap.Core.Settings;

namespace Neap.Core.Tests.Pretend;

public class PretendHeadsetTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMilliseconds(200);

    /// <summary>Everything the headset sends back for one frame, as text.</summary>
    private static string Exchange(IHidTransport link, byte[] frame)
    {
        link.SendOutput(frame);
        return Waiting(link);
    }

    /// <summary>Everything the headset has queued, as text.</summary>
    private static string Waiting(IHidTransport link)
    {
        var text = new StringBuilder();
        while (true)
        {
            var payload = Frames.PayloadOf(link.GetInput());
            if (payload.IsEmpty) return text.ToString();
            text.Append(Encoding.Latin1.GetString(payload));
        }
    }

    private static string Json(string text) => text[text.IndexOf('{', StringComparison.Ordinal)..];

    // -- the shape of what it says -------------------------------------------

    [Fact]
    public void AGeneralStateReplyHasTheDocumentedShape()
    {
        var link = new PretendHeadset().Open();

        string reply = Json(Exchange(link, Frames.Build("SGSI")));

        Assert.StartsWith(
            $"{{\"OR\":\"GSI\",\"KVP\":{{\"200\":\"1\",\"210\":\"0\",\"220\":\"{PretendHeadset.HeadsetName}\",",
            reply, StringComparison.Ordinal);
    }

    [Fact]
    public void ATransmitterSlotAnswersWithItsInfoAndControlArrays()
    {
        var link = new PretendHeadset().Open();

        string reply = Json(Exchange(link, Frames.Build("STX1")));

        Assert.Matches(
            new Regex(@"^\{""OR"":""TX1"",""KVP"":\{""400"":\{""info"":\[(""[^""]*"",){8}""[^""]*""\],"
                + @"""control"":\[(""\d+"",){2}""\d+""\]\}\}\}$"),
            reply);
    }

    [Fact]
    public void AnEmptyTransmitterSlotAnswersWithZeros()
    {
        using var client = new HeadsetClient(transport: new PretendHeadset().Open());

        var slot = Transmitters.Describe(3, client.ReadCategory("TX3", Window)["440"]);

        Assert.False(slot.Paired);
        Assert.All(slot.Control, value => Assert.Equal("0", value));
    }

    [Fact]
    public void ACustomSlotAnswersWithItsNameAndTenBands()
    {
        var link = new PretendHeadset().Open();

        string reply = Json(Exchange(link, Frames.Build("SCG1")));

        Assert.Matches(
            new Regex(@"^\{""OR"":""CG1"",""KVP"":\{""1700"":\{""name"":""[^""]+"",""bands"":\[(""-?\d+"",){9}""-?\d+""\]\}\}\}$"),
            reply);
    }

    [Fact]
    public void AnEmptyCustomSlotDoesNotAnswer()
    {
        var link = new PretendHeadset().Open();

        Assert.Empty(Exchange(link, Frames.Build("SCG5")));
    }

    [Fact]
    public void ANotificationHasTheDocumentedShape()
    {
        var headset = new PretendHeadset();
        var link = headset.Open();

        headset.Report(0x750, "0");

        Assert.Equal("{\"UP\":\"SAF\",\"KVP\":{\"750\":\"0\"}}", Json(Waiting(link)));
    }

    [Fact]
    public void RepliesAreFramedAsRaceResponsesAndNotificationsAsRaceNotifications()
    {
        var headset = new PretendHeadset();
        var link = headset.Open();

        link.SendOutput(Frames.Build("S3DT"));
        var reply = Frames.PayloadOf(link.GetInput()).ToArray();
        headset.Report(0x510, "55");
        var update = Frames.PayloadOf(link.GetInput()).ToArray();

        Assert.Equal([0x05, 0x5B], reply[..2]);
        Assert.Equal([0x05, 0x5D], update[..2]);
    }

    [Fact]
    public void ALongReplyContinuesAcrossReports()
    {
        var link = new PretendHeadset().Open();

        link.SendOutput(Frames.Build("SAQG"));
        var first = link.GetInput();
        var second = link.GetInput();

        Assert.Equal(Frames.ReportLength - 3, first[1] | (first[2] << 8));
        Assert.NotEqual(0, second[1]);
    }

    [Fact]
    public void EverySettingsValueIsAString()
    {
        using var client = new HeadsetClient(transport: new PretendHeadset().Open());

        var values = client.ReadAll(Window);

        Assert.All(values, pair => Assert.Equal(JsonValueKind.String, pair.Value.ValueKind));
    }

    [Fact]
    public void EveryConfirmedSettingIsAnswered()
    {
        using var client = new HeadsetClient(transport: new PretendHeadset().Open());

        var values = client.ReadAll(Window);

        var plain = Registry.All.Where(k => Verbs.SettingCategories.Contains(k.Category));
        Assert.All(plain, key => Assert.True(values.ContainsKey($"{key.Key:x}"), key.Name));
    }

    [Fact]
    public void NothingItReportsCouldBeSomebodysHeadset()
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(transport: headset.Open());

        var all = Transmitters.ReadAll(client, Window);

        Assert.StartsWith("PRETEND", headset.Value(0x120), StringComparison.Ordinal);
        Assert.StartsWith("Pretend", headset.Value(0x220), StringComparison.Ordinal);
        // Locally administered: no manufacturer's address has this bit set.
        Assert.All(all.Where(t => t.Paired), t => Assert.StartsWith("02:", t.Address, StringComparison.Ordinal));
    }

    [Fact]
    public void TheDockHoldsASpareAndTheUsbTransmitterHasNoSlotForOne()
    {
        using var client = new HeadsetClient(transport: new PretendHeadset().Open());

        var all = Transmitters.ReadAll(client, Window);

        Assert.Equal(SpareState.InSlot, all.Single(t => t.ProductId == "229B").Spare?.State);
        Assert.Null(all.Single(t => t.ProductId == "229D").Spare);
    }

    [Fact]
    public void TakingTheSpareOutSendsTheDocksSlot()
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(transport: headset.Open());

        headset.Spare(null);
        // A slot's record is longer than one report.
        var update = Assert.Single(Enumerable.Range(0, 4).SelectMany(_ => client.ReadOnce()).ToList());

        Assert.Equal(SpareState.Empty, Transmitters.FromEvent(update)?.Spare?.State);
    }

    [Fact]
    public void ASparesChargeIsAPercentage()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PretendHeadset().Spare(101));
    }

    // -- finding it ----------------------------------------------------------

    [Fact]
    public void TheClientFindsItBehindTheDock()
    {
        using var client = HeadsetClient.Behind(allowWrites: _ => false, out int present, devices: new PretendHeadset());

        Assert.Equal(1, present);
        Assert.Equal(PretendHeadset.DockProduct, client!.ProductId);
    }

    [Fact]
    public void AnAtlasAirIsFoundBehindItsOwnTransmitterAndSaysWhatItIs()
    {
        using var client = HeadsetClient.Behind(allowWrites: _ => false, out _, devices: new PretendHeadset(atlas: true));

        Assert.Equal(PretendHeadset.AtlasTransmitterProduct, client!.ProductId);
        var everything = client.ReadAll();
        Assert.Equal("2260", everything["110"].GetString());
        Assert.DoesNotContain(everything.Keys, key => key.Length == 3 && key[0] is '5' or '7');
        Assert.Contains("1220", everything.Keys);
    }

    [Fact]
    public void SwitchedOffTheDockIsPresentAndNothingAnswers()
    {
        var headset = new PretendHeadset { On = false };

        var client = HeadsetClient.Behind(allowWrites: _ => false, out int present, devices: headset);

        Assert.Null(client);
        Assert.Equal(1, present);
    }

    [Fact]
    public void AFrameLongerThanTheReportIsRefusedAsTheRealDeviceRefusesIt()
    {
        var link = new PretendHeadset().Open();

        Assert.Throws<TransportException>(() => link.SendOutput(new byte[Frames.ReportLength + 1]));
    }

    // -- what it records and applies -----------------------------------------

    [Fact]
    public void AConfirmedWriteIsRecordedAppliedAndNotified()
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(allowWrites: true, transport: headset.Open());

        client.Set(0x760, 55);

        Assert.Equal(new PretendWrite(0x760, "55"), Assert.Single(headset.Writes));
        Assert.Equal("55", headset.Value(0x760));
        var update = Assert.Single(client.ReadOnce());
        Assert.Equal(("UP", "SAF", "55"), (update.Kind, update.Category, update.Values["760"].GetString()));
    }

    [Fact]
    public void EveryFrameIsRecordedReadsIncluded()
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(transport: headset.Open());

        client.ReadCategory("SAF", Window);
        client.ReadCategory("Mic", Window);

        Assert.Equal(2, headset.Sent.Count);
    }

    [Theory]
    [InlineData("0x400", "2")]    // a transmitter slot's base address
    [InlineData("0x420", "0")]
    [InlineData("0x999", "1")]    // not in the registry
    [InlineData("0x240", "50")]   // battery: reported, never set
    [InlineData("0x760", "101")]  // past its range
    [InlineData("0xa20", "0")]    // not one of the dial's options
    public void AWriteTheRegistryDoesNotConfirmIsRefusedAndNotApplied(string key, string value)
    {
        var headset = new PretendHeadset();
        string? before = headset.Value(Convert.ToInt32(key, 16));
        var refused = new List<PretendRefusal>();
        headset.Refused += refused.Add;

        // Built by hand: the client would refuse to send it.
        headset.Open().SendOutput(Frames.Build("set_kvp", new Dictionary<string, string> { [key] = value }));

        Assert.Empty(headset.Writes);
        Assert.Single(headset.Refusals);
        Assert.Equal(headset.Refusals, refused);
        Assert.Equal(before, headset.Value(Convert.ToInt32(key, 16)));
    }

    [Fact]
    public void ASlotBaseWriteLeavesTheSlotAsItWas()
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(transport: headset.Open());

        headset.Open().SendOutput(Frames.Build("set_kvp", new Dictionary<string, string> { ["0x420"] = "2" }));
        var slot = Transmitters.ReadAll(client, Window)[1];

        Assert.False(slot.Active);
        Assert.Equal("1", slot.Control[0]);
    }

    [Fact]
    public void AnUnknownVerbIsRefusedAndNotAnswered()
    {
        var headset = new PretendHeadset();
        var link = headset.Open();
        var frame = Frames.Build("SGSI");
        frame[^1] = (byte)'X';     // SGSX

        Assert.Empty(Exchange(link, frame));
        Assert.Contains("unknown verb", Assert.Single(headset.Refusals).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AFirmwareCommandIsRefused()
    {
        var headset = new PretendHeadset();

        // Airoha's own storage read, as FINDINGS.md gives it.
        headset.Open().SendOutput([0x06, 0x0A, 0x00, 0x05, 0x5A, 0x06, 0x00, 0x00, 0x0A, 0x83, 0xF2, 0x10, 0x00]);

        Assert.Contains("firmware", Assert.Single(headset.Refusals).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void SwitchedOffAWriteIsCheckedButNotApplied()
    {
        var headset = new PretendHeadset { On = false };
        using var client = new HeadsetClient(allowWrites: true, transport: headset.Open());

        client.Set(0x750, 0);

        Assert.Equal("1", headset.Value(0x750));
        Assert.Empty(headset.Refusals);
    }

    [Fact]
    public void ReportedChangesAreCheckedAgainstTheRegistry()
    {
        var headset = new PretendHeadset();

        Assert.Throws<ArgumentException>(() => headset.Report(0x750, "2"));
        Assert.Throws<ArgumentException>(() => headset.Report(0x400, "2"));
        Assert.Throws<ArgumentException>(() => headset.Report(0x999, "1"));
    }

    // -- the effects the hardware showed -------------------------------------

    [Fact]
    public void WritingABandClearsTheSelectedPreset()
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(allowWrites: true, transport: headset.Open());

        client.Set(0x1250, 20);

        Assert.Equal("0", headset.Value(0x1210));
        Assert.Equal("", headset.Value(0x12C0));
    }

    [Fact]
    public void SelectingAPresetLoadsItsBands()
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(allowWrites: true, transport: headset.Open());

        PresetStore.Apply(client, 4, Bank.Game);
        var current = PresetStore.ReadCurrent(client, Bank.Game, Window);

        Assert.Equal(PresetStore.Factory(Bank.Game)[3].Bands, current!.Bands);
        Assert.Equal("Vocal Boost", current.Name);
    }

    [Fact]
    public void SavingPutsThePresetInTheLowestFreeSlotWhateverIdWasAskedFor()
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(allowWrites: true, transport: headset.Open());

        PresetStore.Save(client, "Bass & Treble 2", Enumerable.Range(1, 10).ToArray(), Bank.Game, hint: 19);
        var saved = PresetStore.ReadCustoms(client, Bank.Game, Window).Single(p => p.Name == "Bass & Treble 2");

        Assert.Equal(18, saved.Id);
        Assert.Equal(Enumerable.Range(1, 10), saved.Bands);
        Assert.Equal(3, PresetStore.Count(client, Bank.Game, Window));
    }

    [Fact]
    public void DeletingByNameFreesTheSlot()
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(allowWrites: true, transport: headset.Open());

        PresetStore.Delete(client, "Night Raid", Bank.Game);

        Assert.DoesNotContain(PresetStore.ReadCustoms(client, Bank.Game, Window), p => p.Name == "Night Raid");
        Assert.Equal(1, PresetStore.Count(client, Bank.Game, Window));
    }

    [Fact]
    public void ALightingWriteLandsInTheSlotItAddresses()
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(allowWrites: true, transport: headset.Open());

        client.Set(0x422, 35);
        var slots = Transmitters.ReadAll(client, Window);

        Assert.Equal("35", slots[1].Control[2]);
        Assert.Equal("60", slots[0].Control[2]);
        Assert.Equal("35", headset.Value(0x422));
    }
}
