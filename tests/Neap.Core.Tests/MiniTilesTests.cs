namespace Neap.Core.Tests;

public class MiniTilesTests
{
    [Fact]
    public void BeforeAChoiceThePanelShowsWhatItAlwaysDid()
    {
        var shown = MiniTiles.Chosen(null);

        Assert.Equal(4, shown.Count);
        Assert.DoesNotContain(MiniTile.SuperhumanHearing, shown);
    }

    [Fact]
    public void BeforeAChoiceTheProfileShowsOnceOneIsSaved()
    {
        Assert.DoesNotContain(MiniTile.Profile, MiniTiles.Chosen(null, hasProfiles: false));
        Assert.Contains(MiniTile.Profile, MiniTiles.Chosen(null, hasProfiles: true));
    }

    [Fact]
    public void AChoiceMadeLeavesTheProfileAsChosen()
    {
        Assert.DoesNotContain(MiniTile.Profile, MiniTiles.Chosen(["microphone"], hasProfiles: true));
        Assert.Contains(MiniTile.Profile, MiniTiles.Chosen(["profile"], hasProfiles: false));
    }

    [Fact]
    public void AChoiceOfNothingShowsNothing() =>
        Assert.Empty(MiniTiles.Chosen([]));

    [Fact]
    public void ANameNoLongerKnownIsLeftOut() =>
        Assert.Equal(MiniTile.Microphone, Assert.Single(MiniTiles.Chosen(["microphone", "tomorrow"])));

    [Fact]
    public void AChoiceSurvivesBeingSavedAndRead()
    {
        var chosen = new[] { MiniTile.SuperhumanHearing, MiniTile.NoiseCancellation };

        var read = MiniTiles.Chosen(MiniTiles.ToSave(chosen));

        Assert.Equal(2, read.Count);
        Assert.All(chosen, tile => Assert.Contains(tile, read));
    }

    [Fact]
    public void ASavedChoiceIsInTheOrderTheTilesAreShown() =>
        Assert.Equal(new[] { "noise", "hearing" },
            MiniTiles.ToSave([MiniTile.SuperhumanHearing, MiniTile.NoiseCancellation]).ToArray());

    [Fact]
    public void EveryTileHasItsOwnName() =>
        Assert.Equal(Enum.GetValues<MiniTile>().Length,
            Enum.GetValues<MiniTile>().Select(MiniTiles.NameOf).Distinct().Count());

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 2)]
    public void TilesAreSetOutThreeToARowOrTwoRowsOfTwo(int shown, int columns) =>
        Assert.Equal(columns, MiniTiles.Columns(shown));
}
