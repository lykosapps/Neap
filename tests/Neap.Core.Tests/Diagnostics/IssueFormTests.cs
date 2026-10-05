using Neap.Core.Diagnostics;

namespace Neap.Core.Tests.Diagnostics;

public class IssueFormTests
{
    private static Dictionary<string, string> Fields(Uri link) =>
        link.Query.TrimStart('?').Split('&')
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));

    [Fact]
    public void HardwareNeapKnowsOpensTheProblemForm()
    {
        var fields = Fields(IssueForm.For(["229B"], "Neap 0.1.0", "10.0.26200.0"));

        Assert.Equal(IssueForm.Problem, fields["template"]);
        Assert.Equal("Charging Dock (229B)", fields["hardware"]);
    }

    [Fact]
    public void OnlyUnknownTurtleBeachHardwareOpensTheHeadsetForm()
    {
        var fields = Fields(IssueForm.For(["2201", "2201"], "Neap 0.1.0", "10.0.26200.0"));

        Assert.Equal(IssueForm.Headset, fields["template"]);
        Assert.Equal("Unrecognised Turtle Beach device (2201)", fields["hardware"]);
    }

    [Fact]
    public void AStealthProIISwitchedOffIsStillAProblemReport()
    {
        // Its transmitter is plugged in whether or not the headset answers.
        Assert.Equal(IssueForm.Problem, Fields(IssueForm.For(["229d", "2201"], "Neap", "10"))["template"]);
    }

    [Fact]
    public void NothingPluggedInIsAProblemReportWithTheHardwareLeftBlank()
    {
        var fields = Fields(IssueForm.For([], "Neap 0.1.0", "10.0.26200.0"));

        Assert.Equal(IssueForm.Problem, fields["template"]);
        Assert.False(fields.ContainsKey("hardware"));
    }

    [Fact]
    public void TheVersionsAreFilledInAndEscaped()
    {
        var link = IssueForm.For(["229B"], "Neap 0.1.0", "10.0.26200.0");

        Assert.StartsWith(IssueForm.NewIssue + "?template=", link.AbsoluteUri, StringComparison.Ordinal);
        Assert.Contains("version=Neap%200.1.0", link.AbsoluteUri, StringComparison.Ordinal);
        Assert.Equal("10.0.26200.0", Fields(link)["windows"]);
    }

    [Fact]
    public void EveryFieldTheLinkFillsIsInBothForms()
    {
        string folder = Path.Combine(Resources.AppSource.Folder, "..", "..", ".github", "ISSUE_TEMPLATE");
        foreach (string form in new[] { IssueForm.Problem, IssueForm.Headset })
        {
            string definition = File.ReadAllText(Path.Combine(folder, form));
            foreach (string id in new[] { "version", "windows", "hardware" })
                Assert.Contains($"id: {id}", definition, StringComparison.Ordinal);
        }
    }
}
