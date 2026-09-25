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

    private static readonly TimeSpan Window = TimeSpan.FromMilliseconds(50);
    private static readonly int[] Flat = new int[10];

    [Fact]
    public void ReadsTheCustomSlotsThatHoldAPreset()
    {
        using var client = new HeadsetClient(transport: new PretendHeadset().Open());

        var presets = PresetStore.ReadCustoms(client, Bank.Game, Window);

        Assert.Equal([16, 17], presets.Select(p => p.Id));
        Assert.Equal("Night Raid", presets[0].Name);
        Assert.Equal(-10, presets[0].Bands[3]);
        Assert.All(presets, p => Assert.True(p.Custom));
    }

    [Fact]
    public void ASlotAnsweringWithAnEmptyNameIsLeftOut()
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(allowWrites: true, transport: headset.Open());
        PresetStore.Delete(client, "Night Raid", Bank.Game);
        client.Drain();
        headset.Push("{\"OR\":\"CG1\",\"KVP\":{\"1700\":{\"name\":\"\",\"bands\":[]}}}");

        var presets = PresetStore.ReadCustoms(client, Bank.Game, Window);

        Assert.Equal(["Footstep Focus"], presets.Select(p => p.Name));
    }

    [Fact]
    public void FreeSlotsAreTheCustomIdsNotInUse()
    {
        Preset[] used =
        [
            new(16, "A", Flat, Bank.Game, true),
            new(18, "B", Flat, Bank.Game, true),
        ];

        Assert.Equal([17, 19, 20], PresetStore.FreeSlots(used));
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
}
