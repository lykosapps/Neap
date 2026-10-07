using Neap.Core.Updates;

namespace Neap.Core.Tests.Updates;

public class UpdateOfferTests
{
    [Fact]
    public void AWritableFolderOnASystemThatInstallsIsOfferedToInstall() =>
        Assert.Equal(UpdateStage.Available, UpdateOffer.For(canInstall: true, () => true));

    [Fact]
    public void AFolderThatCannotBeWrittenToSendsThePersonToTheReleasePage() =>
        Assert.Equal(UpdateStage.CannotUpdateHere, UpdateOffer.For(canInstall: true, () => false));

    [Fact]
    public void ASystemThatNeverInstallsSendsThePersonToTheReleasePageWithoutLookingAtTheFolder()
    {
        bool asked = false;

        var stage = UpdateOffer.For(canInstall: false, () => asked = true);

        Assert.Equal(UpdateStage.CannotUpdateHere, stage);
        Assert.False(asked);
    }
}
