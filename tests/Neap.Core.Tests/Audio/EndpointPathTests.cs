using Neap.Core.Audio;

namespace Neap.Core.Tests.Audio;

public class EndpointPathTests
{
    // Laid out as Windows writes a USB audio device's filter path.
    private static string Path(string vid, string pid) =>
        $@"\\?\usb#vid_{vid}&pid_{pid}&mi_00#7&1a2b3c4d&0&0000#{{6994ad04-93ef-11d0-a3cc-00a0c9223196}}\global";

    [Theory]
    [InlineData("2287", "2287")] // the Charging Dock
    [InlineData("2288", "2288")] // the USB Transmitter
    [InlineData("229e", "229E")] // the headset on its cable
    public void ATurtleBeachDeviceGivesItsProductId(string pid, string product) =>
        Assert.Equal(product, EndpointPath.ProductOf(Path("10f5", pid)));

    [Fact]
    public void TheIdsAreReadWhateverTheirCase() =>
        Assert.Equal("229B", EndpointPath.ProductOf(Path("10F5", "229B").ToUpperInvariant()));

    [Fact]
    public void AnotherMakersDeviceIsNotCounted() =>
        Assert.Equal("", EndpointPath.ProductOf(Path("046d", "085b"))); // a webcam's microphone

    [Theory]
    [InlineData("")]
    [InlineData(@"\\?\usb#vid_10f5")]
    [InlineData(@"\\?\usb#vid_10f5&pid_22")]
    [InlineData(@"{0.0.0.00000000}.{8f3e1a52-0000-0000-0000-000000000000}")]
    public void APathWithoutAWholeProductIdGivesNone(string path) =>
        Assert.Equal("", EndpointPath.ProductOf(path));
}
