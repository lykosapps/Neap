using Neap.Core.Settings;

namespace Neap.Core.Tests.Settings;

public class RegistryTests
{
    [Fact]
    public void KeysAndNamesAreUnique()
    {
        Assert.Equal(Registry.All.Count, Registry.All.Select(k => k.Key).Distinct().Count());
        Assert.Equal(Registry.All.Count, Registry.All.Select(k => k.Name).Distinct().Count());
    }

    [Fact]
    public void EveryWritableNumberHasLimits()
    {
        var ranges = Registry.All.Where(k => k.Writable && k.Kind == SettingKind.Range);

        Assert.All(ranges, k => Assert.True(k.Minimum is not null && k.Maximum is not null, k.Name));
    }

    [Fact]
    public void EveryChoiceHasOptions()
    {
        var choices = Registry.All.Where(k => k.Kind == SettingKind.Enum);

        Assert.All(choices, k => Assert.NotEmpty(k.Options!));
    }

    [Theory]
    [InlineData("anc")]
    [InlineData(0x750)]
    [InlineData("0x750")]
    [InlineData("750")]
    public void ResolvesByNameOrNumber(object key)
    {
        Assert.Equal(0x750, Registry.Resolve(key).Key);
    }

    [Fact]
    public void AnUnknownSettingIsRefused()
    {
        Assert.Throws<KeyNotFoundException>(() => Registry.Resolve("0x9999"));
    }

    [Fact]
    public void AConfirmedWritableKeyIsAccepted()
    {
        Assert.Equal("1", Registry.WireValue(0x750, 1)); // "anc", a toggle.
    }

    [Fact]
    public void AValueOutsideTheKeysOwnLimitsIsRefused()
    {
        Assert.Throws<ArgumentException>(() => Registry.WireValue(0x750, 5)); // a toggle, not 0 or 1.
    }

    [Fact]
    public void AKeyNotInTheRegistryIsRefused()
    {
        Assert.Throws<ArgumentException>(() => Registry.WireValue(0x9999, 1));
    }

    [Fact]
    public void ANonWritableKeyIsRefusedEvenThoughItIsConfirmed()
    {
        Assert.Throws<ArgumentException>(() => Registry.WireValue(0x240, 50)); // "battery", read-only.
    }

    [Fact]
    public void ATransmitterSlotsBaseAddressIsNeverWritable()
    {
        // 0x400, 0x420, 0x440, 0x460: a slot's base, never a setting to write.
        foreach (int slotBase in new[] { 0x400, 0x420, 0x440, 0x460 })
            Assert.Throws<ArgumentException>(() => Registry.WireValue(slotBase, 1));
    }

    [Fact]
    public void EveryTransmitterSlotsLightingValidatesLikeTheFirstSlots()
    {
        // Only slot one's lighting is listed; a write to another slot's is
        // checked against slot one's limits. See Registry.WireValue.
        foreach (int slotBase in new[] { 0x400, 0x420, 0x440, 0x460 })
        {
            Assert.Equal(Registry.WireValue(0x401, 50), Registry.WireValue(slotBase + 1, 50));
            Assert.Equal(Registry.WireValue(0x402, 50), Registry.WireValue(slotBase + 2, 50));
        }
    }

    [Theory]
    [InlineData(0x430, 50)]  // the Atlas Air's transmitter light, inside the second slot's block
    [InlineData(0x800, 1)]   // its Superhuman Hearing
    [InlineData(0x820, 50)]  // its Superhuman Hearing level
    [InlineData(0xC00, 10)]  // its voice prompt volume
    public void AnotherHeadsetsOwnKeyIsRefusedForTheStealthProII(int key, int value)
    {
        Assert.Throws<ArgumentException>(() => Registry.WireValue(key, value, model: HeadsetModels.StealthProII));
        Assert.Throws<ArgumentException>(() => Registry.WireValue(key, value));
        Assert.Equal(value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Registry.WireValue(key, value, model: HeadsetModels.AtlasAir));
    }

    [Fact]
    public void NoWritableKeyFromAGroupOnlyTheAtlasAirHasReachesTheStealthProII()
    {
        var atlasOnly = Registry.All.Where(k => k.Writable
            && HeadsetModels.AtlasAir.ExtraCategories.Contains(k.Category)).ToList();

        Assert.NotEmpty(atlasOnly);
        Assert.All(atlasOnly, k => Assert.Throws<ArgumentException>(
            () => Registry.WireValue(k.Key, k.Minimum ?? 0, model: HeadsetModels.StealthProII)));
    }

    [Fact]
    public void AStealthProIIKeyTheAtlasAirAlsoUsesIsStillAcceptedForTheStealthProII()
    {
        // 0x630 is where the Atlas Air keeps its mic monitoring level, and a
        // setting of the Stealth Pro II's own.
        Assert.Equal("1", Registry.WireValue(0x630, 1, model: HeadsetModels.StealthProII));
    }
}
