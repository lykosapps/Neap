using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class BandSenderTests
{
    private static ParametricBand[] Flat() => [.. StockBands.Game];

    [Fact]
    public void EverythingIsSentTheFirstTime()
    {
        var sent = new List<BandWrite>();
        Assert.Equal(40, new BandSender().Send(Flat(), sent.Add, () => { }));
        Assert.Equal(40, sent.Count);
    }

    [Fact]
    public void NothingIsSentAgainWhenNothingChanged()
    {
        var sender = new BandSender();
        sender.Send(Flat(), _ => { }, () => { });
        Assert.Equal(0, sender.Send(Flat(), _ => throw new InvalidOperationException("sent"), () => { }));
    }

    [Fact]
    public void ForgettingDuringASendIsNotUndoneWhenItEnds()
    {
        var sender = new BandSender();
        sender.Send(Flat(), _ => sender.Forget(), () => { });
        Assert.Equal(40, sender.Send(Flat(), _ => { }, () => { }));
    }

    [Fact]
    public void OnlyTheChangeIsSentTheSecondTime()
    {
        var sender = new BandSender();
        sender.Send(Flat(), _ => { }, () => { });
        var wanted = Flat();
        wanted[5] = wanted[5] with { Gain = 4 };
        var sent = new List<BandWrite>();

        sender.Send(wanted, sent.Add, () => { });

        Assert.Equal([new BandWrite(5, BandParameter.Gain, 4)], sent);
    }

    [Fact]
    public void ABandSwitchedOffAndOnAgainIsSentBothTimes()
    {
        var sender = new BandSender();
        var on = Flat();
        on[2] = on[2] with { Gain = 3 };
        sender.Send(on, _ => { }, () => { });
        var off = Flat();
        off[2] = on[2] with { InUse = false };
        var sent = new List<BandWrite>();

        sender.Send(off, sent.Add, () => { });
        Assert.Equal(
            [new BandWrite(2, BandParameter.Type, (double)BandType.Bypass), new BandWrite(2, BandParameter.Gain, 0)],
            sent);

        sent.Clear();
        sender.Send(on, sent.Add, () => { });
        Assert.Equal(
            [new BandWrite(2, BandParameter.Type, (double)BandType.Peaking), new BandWrite(2, BandParameter.Gain, 3)],
            sent);
    }

    [Fact]
    public void ForgettingMakesTheNextSendEverything()
    {
        var sender = new BandSender();
        sender.Send(Flat(), _ => { }, () => { });
        sender.Forget();
        Assert.Equal(40, sender.Send(Flat(), _ => { }, () => { }));
    }

    [Fact]
    public void AFailedSendLeavesNothingAssumedSoTheNextSendIsEverything()
    {
        var sender = new BandSender();
        sender.Send(Flat(), _ => { }, () => { });
        var wanted = Flat();
        wanted[0] = wanted[0] with { Gain = 2 };
        wanted[9] = wanted[9] with { Gain = 2 };
        int calls = 0;

        Assert.Throws<InvalidOperationException>(() =>
            sender.Send(wanted, _ => { if (++calls == 2) throw new InvalidOperationException("lost"); }, () => { }));

        Assert.Equal(40, sender.Send(wanted, _ => { }, () => { }));
    }

    [Fact]
    public void ItWaitsBetweenWritesButNotBeforeTheFirst()
    {
        int waits = 0;
        new BandSender().Send(Flat(), _ => { }, () => waits++);
        Assert.Equal(39, waits);
    }
}
