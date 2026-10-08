using Neap.Core.Diagnostics;

namespace Neap.Core.Tests.Diagnostics;

public class HeadsetCheckTests
{
    private static HashSet<string> Keys(params string[] hex) => new(hex, StringComparer.Ordinal);

    [Fact]
    public void EveryFeatureIsReadFromConfirmedSettings()
    {
        // HeadsetCheck looks each name up in the registry; one that is not
        // there throws here rather than in front of a person.
        var findings = HeadsetCheck.Of(Keys(), Keys());

        Assert.Equal(Enum.GetValues<Feature>().Length, findings.Missing.Count + findings.Unanswered.Count);
    }

    [Fact]
    public void AFeatureIsFoundOnlyWhenEveryValueItNeedsWasReported()
    {
        var findings = HeadsetCheck.Of(Keys("240", "750"), Keys());

        Assert.Contains(Feature.Battery, findings.Found);
        Assert.Contains(Feature.NoiseControl, findings.Missing);
    }

    [Fact]
    public void AFunctionWhoseWholeGroupWentUnansweredIsNotCalledMissing()
    {
        // Measured on an Atlas Air: nothing at all came back from 0x700 to 0x7FF,
        // where the Stealth Pro II keeps Superhuman Hearing, which the Atlas Air has.
        var findings = HeadsetCheck.Of(Keys("240", "630"), Keys());

        Assert.Contains(Feature.SuperhumanHearing, findings.Unanswered);
        Assert.Contains(Feature.NoiseGate, findings.Unanswered);
        Assert.DoesNotContain(Feature.SuperhumanHearing, findings.Missing);
    }

    [Fact]
    public void AGroupWithNothingReportedIsAskedAgain()
    {
        var silent = HeadsetCheck.Silent(Keys("240", "320", "630"));

        Assert.Contains("SAF", silent);
        Assert.DoesNotContain("GSI", silent);
        Assert.DoesNotContain("Mic", silent);
    }

    [Fact]
    public void AFunctionLeftOutOfAGroupThatAnsweredIsMissing()
    {
        var findings = HeadsetCheck.Of(Keys("700", "710"), Keys());

        Assert.Contains(Feature.NoiseGate, findings.Found);
        Assert.Contains(Feature.SuperhumanHearing, findings.Missing);
    }

    [Fact]
    public void AFeatureWhoseValueChangedIsSaidToHaveMoved()
    {
        var findings = HeadsetCheck.Of(Keys("2a0", "510"), Keys("2a0", "510"));

        Assert.Equal([Feature.MasterVolume, Feature.ChatWheel], findings.Moved);
    }

    [Fact]
    public void AHeadsetThatReportedNothingDidNotAnswer()
    {
        var findings = HeadsetCheck.Of(Keys(), Keys());

        Assert.False(findings.Answered);
        Assert.Equal("The headset didn't answer Neap.", findings.Summary);
    }

    [Fact]
    public void TheSummaryListsEachGroup()
    {
        var findings = HeadsetCheck.Of(Keys("240", "2a0"), Keys("2a0"));

        Assert.StartsWith("Found: Battery, MasterVolume. Not found: VoicePrompts, ", findings.Summary, StringComparison.Ordinal);
        Assert.Contains(". Didn't answer: ", findings.Summary, StringComparison.Ordinal);
        Assert.EndsWith("Changed while recording: MasterVolume.", findings.Summary, StringComparison.Ordinal);
    }
}
