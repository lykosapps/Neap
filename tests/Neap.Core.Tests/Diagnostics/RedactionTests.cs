using Neap.Core.Diagnostics;

namespace Neap.Core.Tests.Diagnostics;

public class RedactionTests
{
    [Fact]
    public void ASecretIsBlankedWhereverItAppears()
    {
        var redaction = new Redaction(["TB1234567"]);

        Assert.Equal("serial (removed), again (removed)",
            redaction.Apply("serial TB1234567, again tb1234567"));
    }

    [Fact]
    public void AnyRadioAddressIsBlankedNotOnlyTheOnesNamed()
    {
        var redaction = new Redaction([]);

        Assert.Equal("info [2, 10F5, (removed)] and (removed)",
            redaction.Apply("info [2, 10F5, AA:BB:CC:DD:EE:0F] and 12-34-56-78-9a-bc"));
    }

    [Fact]
    public void AnEmptySlotsAddressStays()
    {
        Assert.Equal("00:00:00:00:00:00", new Redaction([]).Apply("00:00:00:00:00:00"));
    }

    [Fact]
    public void TimesAndVersionsAreNotTakenForAddresses()
    {
        string text = "14:32:05.1  firmware 4.107.703.0  10:20:30";
        Assert.Equal(text, new Redaction([]).Apply(text));
    }

    [Fact]
    public void TheProfileFolderReadsAsItsVariable()
    {
        var redaction = new Redaction([], profileFolder: @"C:\Users\Sam Smith", account: "Sam Smith");

        Assert.Equal(@"could not read %USERPROFILE%\AppData\Local\Neap\settings.json",
            redaction.Apply(@"could not read C:\Users\Sam Smith\AppData\Local\Neap\settings.json"));
    }

    [Fact]
    public void TheAccountNameIsBlankedOnlyAsAWordOfItsOwn()
    {
        var redaction = new Redaction([], account: "kai");

        Assert.Equal("signed in as (removed); skaitė", redaction.Apply("signed in as Kai; skaitė"));
    }

    [Fact]
    public void AShortAccountNameIsLeftAlone()
    {
        Assert.Equal("signed in as al; also", new Redaction([], account: "al").Apply("signed in as al; also"));
    }

    [Fact]
    public void ASecretTooShortToBeSafeIsIgnored()
    {
        Assert.Equal("volume 100, mic 10", new Redaction(["10"]).Apply("volume 100, mic 10"));
    }

    [Fact]
    public void ThePcsNameIsBlanked()
    {
        Assert.Equal("on (removed)", new Redaction([], machine: "DESKTOP-4F2K9").Apply("on DESKTOP-4F2K9"));
    }
}
