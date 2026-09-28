using Neap.Core.Mix;

namespace Neap.Core.Tests.Mix;

public class ProcessNamesTests
{
    private readonly List<uint> _asked = new();
    private readonly List<string> _logged = new();
    private TimeSpan _now;

    private ProcessNames Make(Func<uint, string>? lookUp = null) => new(pid =>
    {
        _asked.Add(pid);
        return (lookUp ?? (p => $"app{p}"))(pid);
    }, () => _now, _logged.Add);

    [Fact]
    public void ANameIsLookedUpOnceWhileItKeepsBeingAskedFor()
    {
        var names = Make();
        for (int pass = 0; pass < 30; pass++)
        {
            Assert.Equal("app7", names.Of(7));
            _now += TimeSpan.FromSeconds(2);
        }
        Assert.Equal([7u], _asked);
        Assert.Equal(["app7"], _logged);
    }

    [Fact]
    public void AProgramNotAskedForAWhileIsLookedUpAgain()
    {
        var names = Make();
        names.Of(7);
        _now += ProcessNames.Forget + TimeSpan.FromSeconds(1);
        names.Of(7);
        Assert.Equal([7u, 7u], _asked);
    }

    [Fact]
    public void EachProgramIsLookedUpSeparately()
    {
        var names = Make();
        names.Of(7);
        names.Of(9);
        names.Of(7);
        Assert.Equal([7u, 9u], _asked);
    }

    [Fact]
    public void ANameWindowsCannotGiveIsNotRememberedOrLogged()
    {
        var names = Make(_ => "");
        Assert.Equal("", names.Of(7));
        Assert.Equal("", names.Of(7));
        Assert.Equal([7u, 7u], _asked);
        Assert.Empty(_logged);
    }

    [Fact]
    public void IdZeroIsNoProgram()
    {
        Assert.Equal("", Make().Of(0));
        Assert.Empty(_asked);
    }
}
