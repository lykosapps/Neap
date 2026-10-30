using Neap.Core.Audio;

namespace Neap.Core.Tests.Audio;

public class HeadsetDeviceTests
{
    private sealed record Device(string Name, bool Headset, bool Default);

    private static (Device? Device, bool Headset) Choose(params Device[] devices) =>
        HeadsetDevice.Choose(devices, d => d.Headset, d => d.Default);

    private static readonly Device Speakers = new("Speakers", false, false);
    private static readonly Device DefaultSpeakers = new("Speakers", false, true);

    [Fact]
    public void TheHeadsetInUseIsChosen()
    {
        var headset = new Device("Charging Dock", true, true);

        Assert.Equal((headset, true), Choose(Speakers, headset));
    }

    [Fact]
    public void AHeadsetDeviceComesBeforeADefaultThatIsNot()
    {
        var headset = new Device("Charging Dock", true, false);

        Assert.Equal((headset, true), Choose(DefaultSpeakers, headset));
    }

    [Fact]
    public void OfTwoHeadsetDevicesTheOneInUseIsChosen()
    {
        var dock = new Device("Charging Dock", true, false);
        var cable = new Device("Headset cable", true, true);

        Assert.Equal((cable, true), Choose(dock, cable));
    }

    [Fact]
    public void OfTwoHeadsetDevicesNeitherInUseTheFirstIsChosen()
    {
        var dock = new Device("Charging Dock", true, false);
        var transmitter = new Device("USB Transmitter", true, false);

        Assert.Equal((dock, true), Choose(DefaultSpeakers, dock, transmitter));
    }

    [Fact]
    public void WithNoHeadsetDeviceTheDefaultIsChosenAndSaidNotToBeTheHeadsets() =>
        Assert.Equal((DefaultSpeakers, false), Choose(Speakers, DefaultSpeakers));

    [Fact]
    public void WithNoHeadsetDeviceAndNoDefaultNothingIsChosen() =>
        Assert.Equal((null, false), Choose(Speakers));
}
