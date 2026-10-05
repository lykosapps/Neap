using Neap.Core.Updates;

namespace Neap.Core.Tests.Updates;

public class UpdateLookTests
{
    [Theory]
    [InlineData(UpdateStage.NotChecked)]
    [InlineData(UpdateStage.UpToDate)]
    [InlineData(UpdateStage.CheckFailed)]
    public void WithNothingToUpdateToTheButtonChecks(UpdateStage stage)
    {
        Assert.Equal(new UpdateLook(UpdateAction.Check, Busy: false, Leads: false, Notes: false, Download: false),
            UpdateLook.Of(stage));
    }

    [Theory]
    [InlineData(UpdateStage.Available)]
    [InlineData(UpdateStage.UpdateFailed)]
    public void WithANewerVersionUpdatingLeadsWithWhatsNewBesideIt(UpdateStage stage)
    {
        Assert.Equal(new UpdateLook(UpdateAction.Update, Busy: false, Leads: true, Notes: true, Download: false),
            UpdateLook.Of(stage));
    }

    [Theory]
    [InlineData(UpdateStage.Checking, UpdateAction.Check, false)]
    [InlineData(UpdateStage.Downloading, UpdateAction.Update, true)]
    [InlineData(UpdateStage.Installing, UpdateAction.Update, true)]
    public void WhileItRunsTheButtonStaysAndDoesNothing(UpdateStage stage, UpdateAction action, bool notes)
    {
        var look = UpdateLook.Of(stage);
        Assert.Equal(action, look.Action);
        Assert.True(look.Busy);
        Assert.Equal(notes, look.Notes);
    }

    [Fact]
    public void WhereNeapCannotUpdateItselfTheDownloadIsOfferedBesideWhatsNew()
    {
        var look = UpdateLook.Of(UpdateStage.CannotUpdateHere);
        Assert.True(look.Download);
        Assert.True(look.Notes);
        Assert.False(look.Leads);
    }

    [Fact]
    public void TheDownloadIsOfferedNowhereElse()
    {
        foreach (var stage in Enum.GetValues<UpdateStage>().Where(s => s != UpdateStage.CannotUpdateHere))
            Assert.False(UpdateLook.Of(stage).Download, stage.ToString());
    }
}
