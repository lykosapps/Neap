using Neap.Core.Hid;

namespace Neap.Core.Tests.Hid;

public class ReportDescriptorTests
{
    /// <summary>The control collection as the headset is documented to declare it: report 6 out, report 7 in, 61 bytes each.</summary>
    private static readonly byte[] Control =
    [
        0x06, 0x13, 0xFF,       // usage page 0xFF13
        0x09, 0x01,             // usage 1
        0xA1, 0x01,             // collection (application)
        0x85, 0x06,             //   report id 6
        0x75, 0x08,             //   report size 8
        0x95, 0x3D,             //   report count 61
        0x09, 0x01,
        0x91, 0x02,             //   output
        0x85, 0x07,             //   report id 7
        0x95, 0x3D,             //   report count 61
        0x09, 0x01,
        0x81, 0x02,             //   input
        0xC0,                   // end collection
    ];

    /// <summary>Media keys: eight one-bit buttons on report 1.</summary>
    private static readonly byte[] Consumer =
    [
        0x05, 0x0C,             // usage page consumer
        0x09, 0x01,
        0xA1, 0x01,
        0x85, 0x01,
        0x75, 0x01,
        0x95, 0x08,
        0x81, 0x02,
        0xC0,
    ];

    [Fact]
    public void TheControlCollectionHasSixtyTwoByteReportsAndNoFeatureReport()
    {
        var collection = Assert.Single(ReportDescriptor.Collections(Control));

        Assert.Equal(new CollectionReports(0xFF13, InputLength: 62, OutputLength: 62, FeatureLength: 0), collection);
    }

    [Fact]
    public void EachTopLevelCollectionIsDescribedApart()
    {
        var collections = ReportDescriptor.Collections([.. Consumer, .. Control]);

        Assert.Equal([0x000C, 0xFF13], collections.Select(c => (int)c.UsagePage));
        Assert.Equal(2, collections[0].InputLength);
        Assert.Equal(0, collections[0].OutputLength);
    }

    [Fact]
    public void ReportsInsideANestedCollectionCountTowardsItsTopLevelOne()
    {
        byte[] nested =
        [
            0x06, 0x13, 0xFF, 0xA1, 0x01,
            0xA1, 0x02,                         // logical collection
            0x85, 0x07, 0x75, 0x08, 0x95, 0x10, 0x81, 0x02,
            0xC0,
            0xC0,
        ];

        Assert.Equal(17, Assert.Single(ReportDescriptor.Collections(nested)).InputLength);
    }

    [Fact]
    public void TheLongestReportOfAKindIsTheLength()
    {
        byte[] twoInputs =
        [
            0x06, 0x13, 0xFF, 0xA1, 0x01, 0x75, 0x08,
            0x85, 0x07, 0x95, 0x3D, 0x81, 0x02,
            0x85, 0x08, 0x95, 0x04, 0x81, 0x02,
            0xC0,
        ];

        Assert.Equal(62, Assert.Single(ReportDescriptor.Collections(twoInputs)).InputLength);
    }

    [Fact]
    public void ADeviceWithoutReportIdsStillCountsTheIdByte()
    {
        byte[] plain = [0x06, 0x00, 0xFF, 0xA1, 0x01, 0x75, 0x08, 0x95, 0x40, 0x81, 0x02, 0xC0];

        Assert.Equal(65, Assert.Single(ReportDescriptor.Collections(plain)).InputLength);
    }

    [Fact]
    public void APoppedUsagePageApplies()
    {
        byte[] pushed =
        [
            0x06, 0x13, 0xFF, 0xA4,             // push
            0x05, 0x0C, 0xB4,                   // usage page consumer, then pop
            0xA1, 0x01, 0xC0,
        ];

        Assert.Equal(0xFF13, Assert.Single(ReportDescriptor.Collections(pushed)).UsagePage);
    }

    [Theory]
    [InlineData(new byte[] { 0x06, 0x13 })]                  // an item cut short
    [InlineData(new byte[] { 0x06, 0x13, 0xFF, 0xA1, 0x01 })] // a collection left open
    [InlineData(new byte[] { 0xC0 })]                        // a collection ended that never began
    [InlineData(new byte[] { 0xB4 })]                        // a pop with nothing pushed
    public void ADescriptorThatDoesNotParseIsRefusedWhole(byte[] descriptor) =>
        Assert.Throws<FormatException>(() => ReportDescriptor.Collections(descriptor));
}
