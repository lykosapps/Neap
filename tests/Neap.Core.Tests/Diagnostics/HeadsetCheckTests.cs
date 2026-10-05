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

        Assert.Equal(Enum.GetValues<Feature>().Length, findings.Missing.Count);
    }

    [Fact]
    public void AFeatureIsFoundOnlyWhenEveryValueItNeedsWasReported()
    {
        var findings = HeadsetCheck.Of(Keys("240", "750"), Keys());

        Assert.Contains(Feature.Battery, findings.Found);
        Assert.Contains(Feature.NoiseControl, findings.Missing);
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
        Assert.EndsWith("Changed while recording: MasterVolume.", findings.Summary, StringComparison.Ordinal);
    }
}
