using Neap.Core.Audio;

namespace Neap.Core.Tests.Audio;

public class DeviceFormatTests
{
    [Fact]
    public void BuildsAnOrdinaryStereoFormat()
    {
        var format = DeviceFormat.Build(16, 48000, 2);

        Assert.Equal(0xFFFE, format.FormatTag);
        Assert.Equal(2, format.Channels);
        Assert.Equal(48000u, format.SamplesPerSec);
        Assert.Equal(4, format.BlockAlign); // 2 channels * 16 bits / 8.
        Assert.Equal(192000u, format.AvgBytesPerSec); // rate * block align.
        Assert.Equal(16, format.BitsPerSample);
        Assert.Equal(16, format.ValidBitsPerSample);
        Assert.Equal(0x3u, format.ChannelMask); // stereo: front left and right.
    }

    [Fact]
    public void TwentyFourBitIsPackedNotPaddedToThirtyTwo()
    {
        var format = DeviceFormat.Build(24, 96000, 2);

        Assert.Equal(24, format.BitsPerSample);
        Assert.Equal(6, format.BlockAlign); // 2 channels * 24 bits / 8, not 32.
    }

    [Fact]
    public void MonoUsesFrontCentreNotTheFirstSpeaker()
    {
        var format = DeviceFormat.Build(16, 44100, 1);

        Assert.Equal(0x4u, format.ChannelMask); // SPEAKER_FRONT_CENTER, not 0x1.
    }

    [Fact]
    public void MoreThanStereoFillsOneMaskBitPerChannel()
    {
        var format = DeviceFormat.Build(16, 48000, 6);

        Assert.Equal(0x3Fu, format.ChannelMask); // (1 << 6) - 1.
    }

    [Fact]
    public void ThirtyTwoBitUsesADifferentSubtypeThanEverythingElse()
    {
        var pcm = DeviceFormat.Build(24, 48000, 2);
        var floatingPoint = DeviceFormat.Build(32, 48000, 2);

        Assert.NotEqual(pcm.SubFormat, floatingPoint.SubFormat);
    }
}
