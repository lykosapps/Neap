using Neap.Core.Presets;
using Neap.Core.Pretend;

namespace Neap.Core.Tests.Pretend;

public class PretendBandTests
{
    [Fact]
    public void ThePretendHeadsetStartsWithTheStockBands()
    {
        var headset = new PretendHeadset();
        Assert.Equal(StockBands.Game, headset.Bands);
    }

    [Fact]
    public void ABandIsSetOnAHeadsetThatTakesItAndSaidToTheRecording()
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(allowWrites: true, transport: headset.Open());
        var attempts = new List<WriteAttempt>();
        client.Wrote += attempts.Add;

        client.SetBand(new BandWrite(5, BandParameter.Frequency, 3000));
        client.SetBand(new BandWrite(5, BandParameter.Gain, 4));
        client.SetBand(new BandWrite(5, BandParameter.Type, (double)BandType.LowShelf));
        client.SetBand(new BandWrite(5, BandParameter.Q, 0.7f));

        var band = headset.Bands[5];
        Assert.Equal(new ParametricBand(BandType.LowShelf, 3000, 0.7f, 4, true), band);
        Assert.All(attempts, attempt => Assert.Null(attempt.Refused));
        Assert.Equal("parametric_band_5_frequency", attempts[0].Name);
        Assert.Equal("3000", attempts[0].Value);
    }

    [Fact]
    public void ABandIsReadBackWhereTheHeadsetAnswers()
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(allowWrites: true, transport: headset.Open());
        client.SetBand(new BandWrite(2, BandParameter.Gain, -3));
        client.SetBand(new BandWrite(2, BandParameter.Frequency, 200));

        Assert.Equal(-3, client.ReadBand(2, BandParameter.Gain, TimeSpan.FromSeconds(1)));
        Assert.Equal(200, client.ReadBand(2, BandParameter.Frequency, TimeSpan.FromSeconds(1)));
        Assert.Equal((double)BandType.Peaking, client.ReadBand(2, BandParameter.Type, TimeSpan.FromSeconds(1)));
        Assert.Equal(StockBands.Q, client.ReadBand(2, BandParameter.Q, TimeSpan.FromSeconds(1))!.Value, 4);
    }

    [Theory]
    [InlineData(BandParameter.Type, 3)]
    [InlineData(BandParameter.Gain, 30)]
    [InlineData(BandParameter.Frequency, 40000)]
    public void ABandValueOutsideItsLimitsIsNotSentAndSaidToTheRecording(BandParameter parameter, double value)
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(allowWrites: true, transport: headset.Open());
        var attempts = new List<WriteAttempt>();
        client.Wrote += attempts.Add;

        Assert.Throws<ArgumentException>(() => client.SetBand(new BandWrite(5, parameter, value)));

        Assert.Equal(StockBands.Game, headset.Bands);
        Assert.NotNull(Assert.Single(attempts).Refused);
    }

    [Fact]
    public void ABandIsSentToTheAtlasAirThroughItsTransmitter()
    {
        var headset = new PretendHeadset(atlas: true);
        using var client = new HeadsetClient(allowWrites: true, transport: headset.Open());

        client.SetBand(new BandWrite(5, BandParameter.Gain, 4));

        Assert.Equal(4, headset.Bands[5].Gain);
    }

    [Fact]
    public void ABandIsNotSentThroughAReadOnlyClient()
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(allowWrites: false, transport: headset.Open());

        Assert.Throws<WritesDisabledException>(() => client.SetBand(new BandWrite(5, BandParameter.Gain, 4)));
        Assert.Equal(StockBands.Game, headset.Bands);
    }

    [Fact]
    public void TheAtlasAirsStockBandsAreReadThroughItsTransmitter()
    {
        using var client = new HeadsetClient(transport: new PretendHeadset(atlas: true).Open());

        Assert.Equal(31.5, client.ReadBand(0, BandParameter.Frequency, TimeSpan.FromSeconds(1)));
        Assert.Equal((double)BandType.Peaking, client.ReadBand(0, BandParameter.Type, TimeSpan.FromSeconds(1)));
        Assert.Equal(1000, client.ReadBand(5, BandParameter.Frequency, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void OnlyTheAtlasAirLogsItsBandsAtConnect() =>
        Assert.Equal(["Atlas Air"], HeadsetModels.All.Where(model => model.LogsParametricBands).Select(model => model.Name));

    [Fact]
    public void BothHeadsetsHaveParametricBands() =>
        Assert.Equal(["Stealth Pro II", "Atlas Air"], HeadsetModels.All.Where(model => model.HasParametricBands).Select(model => model.Name));
}
