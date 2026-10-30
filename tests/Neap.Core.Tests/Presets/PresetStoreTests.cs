using Neap.Core.Presets;
using Neap.Core.Pretend;

namespace Neap.Core.Tests.Presets;

public class PresetStoreTests
{
    private static string Copy(string name, params string[] taken) =>
        PresetStore.CopyName(name, "{0} copy", "{0} copy {1}", taken, Bank.Game);

    [Fact]
    public void ACopyIsNamedForItsPreset() => Assert.Equal("Test copy", Copy("Test", "Test"));

    [Fact]
    public void ALaterCopyIsNumbered() => Assert.Equal("Test copy 2", Copy("Test", "Test", "Test copy"));

    [Fact]
    public void ALongNameIsShortenedToFit()
    {
        string name = Copy("Bass & Treble Boost", "Bass & Treble Boost");
        Assert.Equal("Bass & Treble copy", name);
        Assert.True(name.Length <= PresetStore.MaxNameLength);
    }

    [Theory]
    [InlineData(27, 25)]
    [InlineData(23, 25)]
    [InlineData(22, 20)]
    [InlineData(-82, -80)]
    [InlineData(-83, -85)]
    [InlineData(0, 0)]
    [InlineData(90, 90)]
    public void BandValuesSnapToHalfDecibels(int tenths, int snapped) =>
        Assert.Equal(snapped, PresetStore.Snap(tenths));

    /// <summary>Long enough for a slot's reply of two reports however coarse the machine's sleeps, as a missed reply leaves the bank not fully read.</summary>
    private static readonly TimeSpan Window = TimeSpan.FromMilliseconds(200);
    private static readonly int[] Flat = new int[10];

    [Fact]
    public void ReadsTheCustomSlotsThatHoldAPreset()
    {
        using var client = new HeadsetClient(transport: new PretendHeadset().Open());

        var read = PresetStore.ReadCustoms(client, Bank.Game, Window);

        Assert.Equal([16, 17], read.Presets.Select(p => p.Id));
        Assert.Equal("Night Raid", read.Presets[0].Name);
        Assert.Equal(-10, read.Presets[0].Bands[3]);
        Assert.All(read.Presets, p => Assert.True(p.Custom));
        Assert.True(read.Complete);
    }

    [Fact]
    public void ASlotAnsweringWithAnEmptyNameIsLeftOut()
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(allowWrites: true, transport: headset.Open());
        PresetStore.Delete(client, "Night Raid", Bank.Game);
        client.Drain();
        headset.Push("{\"OR\":\"CG1\",\"KVP\":{\"1700\":{\"name\":\"\",\"bands\":[]}}}");

        var read = PresetStore.ReadCustoms(client, Bank.Game, Window);

        Assert.Equal(["Footstep Focus"], read.Presets.Select(p => p.Name));
        Assert.Equal(SlotFound.Empty, read.Slots[0].Found);
        Assert.True(read.Complete);
    }

    /// <summary>Reads the pretend headset's game slots, with the slots given losing their reply the number of times given.</summary>
    private static CustomsRead ReadLosing(HeadsetClient client, Dictionary<int, int> losses, Func<int?>? count = null)
    {
        var reads = new Dictionary<int, int>();
        return PresetStore.ReadCustoms(Bank.Game,
            index =>
            {
                reads[index] = reads.GetValueOrDefault(index) + 1;
                return Task.FromResult(reads[index] <= losses.GetValueOrDefault(index)
                    ? new SlotReading(PresetStore.Game.Slots[index], SlotFound.NoAnswer)
                    : PresetStore.ReadSlot(client, Bank.Game, index, Window));
            },
            () => Task.FromResult(count is null ? PresetStore.Count(client, Bank.Game, Window) : count())).GetAwaiter().GetResult();
    }

    [Fact]
    public void ASlotWhoseReplyIsLostIsReadAgain()
    {
        using var client = new HeadsetClient(transport: new PretendHeadset().Open());

        var read = ReadLosing(client, new() { [1] = 1 });

        Assert.Equal(["Night Raid", "Footstep Focus"], read.Presets.Select(p => p.Name));
        Assert.True(read.Complete);
    }

    [Fact]
    public void ASlotLostTwiceLeavesTheBankNotFullyRead()
    {
        using var client = new HeadsetClient(transport: new PretendHeadset().Open());

        var read = ReadLosing(client, new() { [1] = 2 });

        Assert.Equal(["Night Raid"], read.Presets.Select(p => p.Name));
        Assert.False(read.Complete);
        Assert.Empty(PresetStore.FreeSlots(read.Presets, read.Complete));
        Assert.Contains("CG2 did not answer", read.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void ABankThatDisagreesWithItsCountIsReadAgainWhole()
    {
        using var client = new HeadsetClient(transport: new PretendHeadset().Open());
        var counts = new Queue<int?>([3, 2]);

        var read = ReadLosing(client, [], counts.Dequeue);

        Assert.Equal(["Night Raid", "Footstep Focus"], read.Presets.Select(p => p.Name));
        Assert.True(read.Complete);
    }

    [Fact]
    public void ABankWithNoCountIsNotFullyRead()
    {
        using var client = new HeadsetClient(transport: new PretendHeadset().Open());
        int asked = 0;

        var read = ReadLosing(client, [], () => { asked++; return null; });

        Assert.Equal(2, read.Presets.Count);
        Assert.False(read.Complete);
        Assert.Equal(2, asked);
    }

    [Fact]
    public void ABandSentAsANumberIsRead()
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(transport: headset.Open());
        headset.Push("{\"OR\":\"CG1\",\"KVP\":{\"1700\":{\"name\":\"Plain\",\"bands\":[1,2,3,4,5,6,7,8,9,10]}}}");

        var slot = PresetStore.ReadSlot(client, Bank.Game, 0, Window);

        Assert.Equal(SlotFound.Preset, slot.Found);
        Assert.Equal(Enumerable.Range(1, 10), slot.Preset!.Bands);
    }

    [Theory]
    [InlineData("[\"10\",\"x\",\"0\",\"0\",\"0\",\"0\",\"0\",\"0\",\"0\",\"0\"]")]
    [InlineData("[\"10\",\"20\"]")]
    [InlineData("\"10\"")]
    public void ASlotWithABandThatCannotBeReadIsNotUnderstood(string bands)
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(transport: headset.Open());
        headset.Push($"{{\"OR\":\"CG1\",\"KVP\":{{\"1700\":{{\"name\":\"Odd\",\"bands\":{bands}}}}}}}");

        var slot = PresetStore.ReadSlot(client, Bank.Game, 0, Window);

        Assert.Equal(SlotFound.Unparsed, slot.Found);
        Assert.Null(slot.Preset);
    }

    [Fact]
    public void FreeSlotsAreTheCustomIdsNotInUse()
    {
        Preset[] used =
        [
            new(16, "A", Flat, Bank.Game, true),
            new(18, "B", Flat, Bank.Game, true),
        ];

        Assert.Equal([17, 19, 20], PresetStore.FreeSlots(used, fullyRead: true));
        Assert.Empty(PresetStore.FreeSlots(used, fullyRead: false));
    }

    [Fact]
    public void SavingWritesTheSlotThenTheBandsThenTheName()
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(allowWrites: true, transport: headset.Open());

        PresetStore.Save(client, "Mud cut", Enumerable.Range(1, 10).ToArray(), Bank.Game);

        var writes = headset.Writes;
        Assert.Equal(12, writes.Count);
        Assert.Equal(new PretendWrite(0x1210, "16"), writes[0]);
        Assert.Equal(new PretendWrite(0x1220, "1"), writes[1]);
        Assert.Equal(new PretendWrite(0x12B0, "10"), writes[10]);
        Assert.Equal(new PretendWrite(0x12C0, "Mud cut"), writes[11]);
    }

    [Fact]
    public void ANameTooLongIsRefusedBeforeAnythingIsWritten()
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(allowWrites: true, transport: headset.Open());

        Assert.Throws<PresetException>(() =>
            PresetStore.Save(client, "A name far too long to store", Flat, Bank.Game));
        Assert.Empty(headset.Sent);
    }

    [Fact]
    public void FactoryPresetsCannotBeDeleted()
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(allowWrites: true, transport: headset.Open());

        Assert.Throws<PresetException>(() => PresetStore.Delete(client, "Bass Boost", Bank.Game));
        Assert.Empty(headset.Sent);
    }

    [Fact]
    public void DeletingThePresetInUseMovesOffItFirst()
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(allowWrites: true, transport: headset.Open());
        PresetStore.Apply(client, 16, Bank.Game);
        client.Drain();
        headset.ClearRecord();

        PresetStore.Delete(client, "Night Raid", Bank.Game);

        Assert.Equal([new PretendWrite(0x1210, "1"), new PretendWrite(0x1610, "Night Raid")], headset.Writes);
    }

    [Fact]
    public void DeletingThePresetInUseMovesOffItEvenWhenItsBandsAreNotRead()
    {
        var headset = new PretendHeadset();
        var transmitter = new FirstReplyLosesBands(headset.Open(),
            "{\"OR\":\"AQG\",\"KVP\":{\"1210\":\"16\",\"12c0\":\"Night Raid\"}}");
        using var client = new HeadsetClient(allowWrites: true, transport: transmitter);

        PresetStore.Delete(client, "Night Raid", Bank.Game);

        Assert.Equal([new PretendWrite(0x1210, "1"), new PretendWrite(0x1610, "Night Raid")], headset.Writes);
    }

    private static Preset Custom(int id, string name) => new(id, name, Flat, Bank.Game, true);

    private static readonly Preset[] OneSaved = [Custom(16, "Footsteps")];

    [Theory]
    [InlineData("", PresetNameProblem.Empty)]
    [InlineData("   ", PresetNameProblem.Empty)]
    [InlineData("bass boost", PresetNameProblem.Factory)]
    [InlineData(" FOOTSTEPS", PresetNameProblem.Taken)]
    [InlineData("Bass & Treble copy", PresetNameProblem.None)]
    public void ANewPresetsNameMustBeGivenAndFree(string name, PresetNameProblem expected) =>
        Assert.Equal(expected, PresetStore.Check(name, OneSaved, Bank.Game, true));

    [Fact]
    public void AFactoryNameIsRefusedEvenWhenTheListLeavesTheFactoryPresetsOut() =>
        Assert.Equal(PresetNameProblem.Factory, PresetStore.Check("Clarity", [], Bank.Mic, true));

    [Fact]
    public void ANameAtTheLimitFitsAsLongAsItsCharactersArePlain()
    {
        Assert.Equal(PresetNameProblem.None, PresetStore.Check(new string('a', PresetStore.MaxNameLength), OneSaved, Bank.Game, true));
        // A quotation mark is escaped, so it costs two of the report's bytes.
        Assert.Equal(PresetNameProblem.TooLong, PresetStore.Check("\"" + new string('a', PresetStore.MaxNameLength - 1), OneSaved, Bank.Game, true));
    }

    [Fact]
    public void ANewPresetNeedsAFreeSlot()
    {
        Preset[] full = [.. Enumerable.Range(0, PresetStore.CustomSlots).Select(i => Custom(PresetStore.FirstCustomId + i, $"P{i}"))];

        Assert.Equal(PresetNameProblem.Full, PresetStore.Check("Music", full, Bank.Game, true));
        Assert.Equal(PresetNameProblem.Taken, PresetStore.Check("p1", full, Bank.Game, true));
    }

    [Fact]
    public void ANewPresetWaitsForEverySlotToBeRead()
    {
        Assert.Equal(PresetNameProblem.Unread, PresetStore.Check("Music", OneSaved, Bank.Game, false));
        Assert.Equal(PresetNameProblem.Taken, PresetStore.Check("footsteps", OneSaved, Bank.Game, false));
    }

    /// <summary>A pretend headset whose answer to the first request is replaced by the reply given, as when a category's band values are lost.</summary>
    private sealed class FirstReplyLosesBands(Core.Hid.IHidTransport headset, string reply) : Core.Hid.IHidTransport
    {
        private byte[]? _waiting;
        private bool _replied;

        public ushort ProductId => headset.ProductId;

        public string Name => headset.Name;

        public string Describe() => headset.Describe();

        public void SendOutput(ReadOnlySpan<byte> report)
        {
            if (_replied)
            {
                headset.SendOutput(report);
                return;
            }
            _replied = true;
            _waiting = new byte[64];
            _waiting[0] = Core.Hid.HidControl.InReportId;
            _waiting[1] = (byte)reply.Length;
            System.Text.Encoding.ASCII.GetBytes(reply).CopyTo(_waiting, 3);
        }

        public byte[] GetInput(byte reportId = Core.Hid.HidControl.InReportId)
        {
            if (_waiting is not { } report) return headset.GetInput(reportId);
            _waiting = null;
            return report;
        }

        public void Dispose() => headset.Dispose();
    }
}
