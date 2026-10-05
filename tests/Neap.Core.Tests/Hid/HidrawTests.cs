using Neap.Core.Hid;

namespace Neap.Core.Tests.Hid;

public class HidrawTests
{
    [Fact]
    public void TheIdsAreReadFromTheUevent()
    {
        const string uevent = "DRIVER=hid-generic\nHID_ID=0003:000010F5:0000229B\n"
            + "HID_NAME=Turtle Beach Stealth Pro II\nHID_PHYS=usb-0000:04:00.3-1/input3\n";

        Assert.Equal(((ushort)0x10F5, (ushort)0x229B), Hidraw.Ids(uevent));
    }

    [Fact]
    public void AUeventWithNoIdsHasNone() =>
        Assert.Null(Hidraw.Ids("DRIVER=hid-generic\n"));

    [Theory]
    [InlineData("HID_ID=0003:10F5\n")]
    [InlineData("HID_ID=0003:0000ZZZZ:0000229B\n")]
    [InlineData("HID_ID=0003:000110F5:0000229B\n")]
    public void IdsThatDoNotReadAreRefusedRatherThanSkipped(string uevent) =>
        Assert.Throws<FormatException>(() => Hidraw.Ids(uevent));
}
