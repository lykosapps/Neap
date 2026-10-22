namespace Neap.Core.Tests;

public class ActiveWindowsTests
{
    private readonly object _main = new();
    private readonly object _panel = new();

    [Fact]
    public void BeforeAnyWindowSaysItIsInFront() => Assert.True(new ActiveWindows().InFront);

    [Fact]
    public void ThePanelGoingBehindAfterTheWindowCameForwardLeavesItInFront()
    {
        var windows = new ActiveWindows();
        windows.Set(_panel, active: true);
        windows.Set(_main, active: true);
        windows.Set(_panel, active: false);
        Assert.True(windows.InFront);
    }

    [Fact]
    public void ThePanelGoingBehindBeforeTheWindowCameForwardLeavesItInFront()
    {
        var windows = new ActiveWindows();
        windows.Set(_panel, active: true);
        windows.Set(_panel, active: false);
        windows.Set(_main, active: true);
        Assert.True(windows.InFront);
    }

    [Fact]
    public void WithEveryWindowBehindItIsBehind()
    {
        var windows = new ActiveWindows();
        windows.Set(_main, active: true);
        windows.Set(_panel, active: true);
        windows.Set(_main, active: false);
        windows.Set(_panel, active: false);
        Assert.False(windows.InFront);
    }

    [Fact]
    public void OnlyAChangeInFrontOrBehindCounts()
    {
        var windows = new ActiveWindows();
        Assert.False(windows.Set(_main, active: true));
        Assert.False(windows.Set(_panel, active: true));
        Assert.False(windows.Set(_main, active: false));
        Assert.True(windows.Set(_panel, active: false));
        Assert.True(windows.Set(_main, active: true));
    }
}
