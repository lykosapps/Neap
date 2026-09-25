using Neap.Core.Protocol;

namespace Neap.Core.Tests.Protocol;

public class VerbsTests
{
    [Fact]
    public void EveryReaderButBluetoothIsSPlusItsCategory()
    {
        var readers = Verbs.Readers.Where(pair => pair.Key != "BT");

        Assert.All(readers, pair => Assert.Equal("S" + pair.Key, pair.Value));
    }

    [Fact]
    public void BluetoothIsReadWithRbt()
    {
        Assert.Equal("RBT", Verbs.Readers["BT"]);
    }

    [Theory]
    [InlineData("CG1", 0x1700)]
    [InlineData("CG5", 0x1780)]
    [InlineData("CM1", 0x1800)]
    [InlineData("CM5", 0x1880)]
    public void PresetSlotsSitTwentyApart(string category, int key)
    {
        Assert.Equal(key, Verbs.PresetSlotKey(category));
    }

    [Theory]
    [InlineData("TX1", 0x400)]
    [InlineData("TX4", 0x460)]
    public void TransmitterSlotsSitTwentyApart(string category, int key)
    {
        Assert.Equal(key, Verbs.TransmitterKey(category));
    }
}
