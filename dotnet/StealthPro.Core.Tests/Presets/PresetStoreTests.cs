using StealthPro.Core.Presets;

namespace StealthPro.Core.Tests.Presets;

public class PresetStoreTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMilliseconds(50);
    private static readonly int[] Flat = new int[10];

    [Fact]
    public void ReadsTheCustomSlotsThatHoldAPreset()
    {
        var device = new FakeTransport();
        device.Answers["SCG1"] =
            ["{\"OR\":\"CG1\",\"KVP\":{\"1700\":{\"name\":\"Mud cut\",\"bands\":[\"30\",\"20\",\"-30\",\"15\",\"0\",\"10\",\"20\",\"30\",\"0\",\"20\"]}}}"];
        device.Answers["SCG2"] = ["{\"OR\":\"CG2\",\"KVP\":{\"1720\":{\"name\":\"\",\"bands\":[]}}}"];
        using var client = new HeadsetClient(transport: device);

        var preset = Assert.Single(PresetStore.ReadCustoms(client, Bank.Game, Window));

        Assert.Equal(16, preset.Id);
        Assert.Equal("Mud cut", preset.Name);
        Assert.Equal(-30, preset.Bands[2]);
        Assert.True(preset.Custom);
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
        var device = new FakeTransport();
        using var client = new HeadsetClient(allowWrites: true, transport: device);

        PresetStore.Save(client, "Mud cut", Enumerable.Range(1, 10).ToArray(), Bank.Game);

        var writes = device.Writes.ToList();
        Assert.Equal(12, writes.Count);
        Assert.Equal("{\"0x1210\":\"16\"}", writes[0]);
        Assert.Equal("{\"0x1220\":\"1\"}", writes[1]);
        Assert.Equal("{\"0x12b0\":\"10\"}", writes[10]);
        Assert.Equal("{\"0x12c0\":\"Mud cut\"}", writes[11]);
    }

    [Fact]
    public void ANameTooLongIsRefusedBeforeAnythingIsWritten()
    {
        var device = new FakeTransport();
        using var client = new HeadsetClient(allowWrites: true, transport: device);

        Assert.Throws<PresetException>(() =>
            PresetStore.Save(client, "A name far too long to store", Flat, Bank.Game));
        Assert.Empty(device.Sent);
    }

    [Fact]
    public void FactoryPresetsCannotBeDeleted()
    {
        var device = new FakeTransport();
        using var client = new HeadsetClient(allowWrites: true, transport: device);

        Assert.Throws<PresetException>(() => PresetStore.Delete(client, "Bass Boost", Bank.Game));
        Assert.Empty(device.Sent);
    }

    [Fact]
    public void DeletingThePresetInUseMovesOffItFirst()
    {
        var device = new FakeTransport();
        device.Answers["SAQG"] = ["{\"OR\":\"AQG\",\"KVP\":{\"1210\":\"16\",\"12c0\":\"Mud cut\"}}"];
        using var client = new HeadsetClient(allowWrites: true, transport: device);

        PresetStore.Delete(client, "Mud cut", Bank.Game);

        Assert.Equal(["{\"0x1210\":\"1\"}", "{\"0x1610\":\"Mud cut\"}"], device.Writes);
    }
}
