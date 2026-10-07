using Neap.Core.Connection;

namespace Neap.Core.Tests.Connection;

public class SectionFoldTests
{
    [Theory]
    [InlineData(Link.Connecting, false, Fold.Unchanged)]
    [InlineData(Link.Connecting, true, Fold.Unchanged)]
    [InlineData(Link.Absent, false, Fold.Hidden)]
    [InlineData(Link.Absent, true, Fold.Hidden)]
    [InlineData(Link.Quiet, false, Fold.Off)]
    [InlineData(Link.Quiet, true, Fold.Off)]
    [InlineData(Link.Silent, false, Fold.Unreachable)]
    [InlineData(Link.Silent, true, Fold.Open)]
    [InlineData(Link.Connected, false, Fold.Open)]
    [InlineData(Link.Connected, true, Fold.Open)]
    public void FoldsForEachState(Link link, bool whenOff, Fold fold) =>
        Assert.Equal(fold, SectionFold.For(new HeadsetStatus(link, Route.ChargingDock, "", ""), whenOff));

    [Theory]
    [InlineData(Link.Connecting)]
    [InlineData(Link.Absent)]
    [InlineData(Link.Quiet)]
    [InlineData(Link.Silent)]
    [InlineData(Link.Connected)]
    public void WithoutAccessEverySectionFoldsWithNothingInItsPlace(Link link) =>
        Assert.Equal(Fold.Hidden,
            SectionFold.For(new HeadsetStatus(link, Route.ChargingDock, "", ""), whenOff: true, accessDenied: true));

    [Fact]
    public void SwitchedOffOnItsCableFoldsAsOff() =>
        Assert.Equal(Fold.Off,
            SectionFold.For(new HeadsetStatus(Link.Quiet, Route.DirectUsb, "", ""), whenOff: true));
}
