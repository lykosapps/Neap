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

    /// <remarks>
    /// Laid out as sysfs is: the class folder is a link into the device tree,
    /// and the device link inside it climbs out of the folder it is in, so
    /// reading the path as spelled lands in the wrong place.
    /// </remarks>
    [Fact]
    public void TheProductNameIsReadFromTheUsbDevice()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "sysfs and its links are Linux's");
        string root = Directory.CreateTempSubdirectory("neap-sysfs").FullName;
        try
        {
            string usb = Path.Combine(root, "devices", "usb1", "1-2");
            string hid = Path.Combine(usb, "1-2:1.3", "0003:10F5:229B.0001");
            Directory.CreateDirectory(Path.Combine(hid, "hidraw", "hidraw0"));
            File.WriteAllText(Path.Combine(usb, "product"), "Stealth Pro II Xbox\n");
            File.CreateSymbolicLink(Path.Combine(hid, "hidraw", "hidraw0", "device"), "../../../0003:10F5:229B.0001");
            Directory.CreateDirectory(Path.Combine(root, "class", "hidraw"));
            Directory.CreateSymbolicLink(Path.Combine(root, "class", "hidraw", "hidraw0"),
                "../../devices/usb1/1-2/1-2:1.3/0003:10F5:229B.0001/hidraw/hidraw0");

            Assert.Equal("Stealth Pro II Xbox", Hidraw.ProductName(Path.Combine(root, "class", "hidraw", "hidraw0")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ADeviceWithNoFolderHasNoName() =>
        Assert.Equal("", Hidraw.ProductName(Path.Combine(Path.GetTempPath(), "neap-no-such-hidraw")));

    [Theory]
    [InlineData("HID_ID=0003:10F5\n")]
    [InlineData("HID_ID=0003:0000ZZZZ:0000229B\n")]
    [InlineData("HID_ID=0003:000110F5:0000229B\n")]
    public void IdsThatDoNotReadAreRefusedRatherThanSkipped(string uevent) =>
        Assert.Throws<FormatException>(() => Hidraw.Ids(uevent));
}
