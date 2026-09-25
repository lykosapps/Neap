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
}
