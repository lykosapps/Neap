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
}
