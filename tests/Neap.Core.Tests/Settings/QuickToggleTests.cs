using Neap.Core.Audio;
using Neap.Core.Settings;

namespace Neap.Core.Tests.Settings;

public class QuickToggleTests
{
    [Fact]
    public void APressTurnsItOff() =>
        Assert.Equal(NoiseMode.Off, QuickToggle.Press(NoiseMode.Transparency, NoiseMode.Off, NoiseMode.Cancelling));

    [Fact]
    public void APressTurnsItBackOnTheWayItWasLastOn() =>
        Assert.Equal(NoiseMode.Transparency, QuickToggle.Press(NoiseMode.Off, NoiseMode.Off, NoiseMode.Transparency));

    [Fact]
    public void AWayNoLongerOfferedGivesWayToTheFirstThatIs() =>
        Assert.Equal(SpatialFormat.WindowsSonic, QuickToggle.Press(
            SpatialFormat.Off, SpatialFormat.Off, SpatialFormat.DolbyAtmos, [SpatialFormat.Off, SpatialFormat.WindowsSonic]));

    [Fact]
    public void WithNoWayToBeOnOffStaysOff() =>
        Assert.Equal(SpatialFormat.Off, QuickToggle.Press(
            SpatialFormat.Off, SpatialFormat.Off, SpatialFormat.WindowsSonic, [SpatialFormat.Off]));

    [Theory]
    [InlineData(NoiseMode.Transparency, NoiseMode.Cancelling, NoiseMode.Transparency)]
    [InlineData(NoiseMode.Off, NoiseMode.Transparency, NoiseMode.Transparency)]
    public void OnlyAWayOfBeingOnIsRemembered(NoiseMode seen, NoiseMode before, NoiseMode after) =>
        Assert.Equal(after, QuickToggle.LastOn(seen, NoiseMode.Off, before));
}
