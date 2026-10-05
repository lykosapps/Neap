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
        Assert.Equal(new UpdateLook(UpdateAction.Check, Busy: false, Leads: false, UpdateLink.None), UpdateLook.Of(stage));
    }

    [Theory]
    [InlineData(UpdateStage.Available)]
    [InlineData(UpdateStage.UpdateFailed)]
    public void WithANewerVersionUpdatingLeadsWithWhatsNewBesideIt(UpdateStage stage)
    {
        Assert.Equal(new UpdateLook(UpdateAction.Update, Busy: false, Leads: true, UpdateLink.Notes), UpdateLook.Of(stage));
    }

    [Theory]
    [InlineData(UpdateStage.Checking, UpdateAction.Check)]
    [InlineData(UpdateStage.Downloading, UpdateAction.Update)]
    [InlineData(UpdateStage.Installing, UpdateAction.Update)]
    public void WhileItRunsTheButtonStaysAndDoesNothing(UpdateStage stage, UpdateAction action)
    {
        var look = UpdateLook.Of(stage);
        Assert.Equal(action, look.Action);
        Assert.True(look.Busy);
    }

    [Fact]
    public void WhereNeapCannotUpdateItselfTheDownloadIsOffered()
    {
        var look = UpdateLook.Of(UpdateStage.CannotUpdateHere);
        Assert.Equal(UpdateLink.Download, look.Link);
        Assert.False(look.Leads);
    }
}
