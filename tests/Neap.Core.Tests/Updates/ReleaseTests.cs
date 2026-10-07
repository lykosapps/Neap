using Neap.Core.Updates;

namespace Neap.Core.Tests.Updates;

public class ReleaseTests
{
    private const string Hash = "8f14e45fceea167a5a36dedd4bea2543a1b3c9d5e8f7a6b5c4d3e2f1a0b9c8d7";

    /// <summary>GitHub's description of a release, cut to the fields Neap reads, as the workflow publishes one.</summary>
    private static string Json(
        string tag = "v0.3.0",
        bool draft = false,
        bool prerelease = false,
        string host = "github.com",
        string repository = "lykosapps/Neap",
        string? zipName = null,
        string? body = "## Notes\n\n- It does a thing.")
    {
        string version = tag.TrimStart('v');
        zipName ??= $"Neap-{version}-win-x64.zip";
        string download = $"https://{host}/{repository}/releases/download/{tag}";
        return $$"""
            {
              "tag_name": "{{tag}}",
              "html_url": "https://github.com/lykosapps/Neap/releases/tag/{{tag}}",
              "draft": {{(draft ? "true" : "false")}},
              "prerelease": {{(prerelease ? "true" : "false")}},
              "body": {{(body is null ? "null" : System.Text.Json.JsonSerializer.Serialize(body))}},
              "assets": [
                { "name": "{{zipName}}", "browser_download_url": "{{download}}/{{zipName}}" },
                { "name": "{{zipName}}.sha256", "browser_download_url": "{{download}}/{{zipName}}.sha256" }
              ]
            }
            """;
    }

    [Fact]
    public void AReleaseAsTheWorkflowPublishesItIsRead()
    {
        var release = Release.Parse(Json());

        Assert.Equal(new Version(0, 3, 0), release.Version);
        Assert.Equal("Neap-0.3.0-win-x64.zip", release.ZipName);
        Assert.EndsWith("/v0.3.0/Neap-0.3.0-win-x64.zip", release.Zip?.AbsoluteUri, StringComparison.Ordinal);
        Assert.EndsWith("/v0.3.0/Neap-0.3.0-win-x64.zip.sha256", release.Hash?.AbsoluteUri, StringComparison.Ordinal);
        Assert.Equal("https://github.com/lykosapps/Neap/releases/tag/v0.3.0", release.Page.AbsoluteUri);
    }

    [Theory]
    [InlineData("0.2.0", true)]
    [InlineData("0.2.9", true)]
    [InlineData("0.3.0", false)]
    [InlineData("0.3.1", false)]
    [InlineData("1.0.0", false)]
    public void OnlyANewerVersionIsNewer(string running, bool newer)
    {
        Assert.Equal(newer, Release.Parse(Json(tag: "v0.3.0")).IsNewerThan(Version.Parse(running)));
    }

    [Fact]
    public void TheBuildsFourthPartDoesNotMakeItOlder()
    {
        // The running app's version comes from its assembly, as 0.3.0.0.
        Assert.False(Release.Parse(Json(tag: "v0.3.0")).IsNewerThan(new Version(0, 3, 0, 0)));
    }

    [Fact]
    public void TheNotesComeWithTheRelease()
    {
        Assert.Equal("## Notes\n\n- It does a thing.", Release.Parse(Json()).Notes);
    }

    [Fact]
    public void ASystemThatOnlyPointsToTheReleasePageDoesNotNeedTheZip()
    {
        string noZip = Json(zipName: "Neap-0.3.0-linux-x64.tar.gz");

        Assert.Throws<FormatException>(() => Release.Parse(noZip));

        var release = Release.Parse(noZip, installable: false);
        Assert.Equal(new Version(0, 3, 0), release.Version);
        Assert.Equal("https://github.com/lykosapps/Neap/releases/tag/v0.3.0", release.Page.AbsoluteUri);
        Assert.Null(release.Zip);
        Assert.Null(release.Hash);
    }

    [Fact]
    public void AReleaseWithoutADownloadIsReadBackAsItWas()
    {
        var release = Release.Parse(Json(zipName: "Neap-0.3.0-linux-x64.tar.gz"), installable: false);

        Assert.Equal(release, Release.Parse(release.ToJson(), installable: false));
    }

    [Fact]
    public void ASystemThatPointsToThePageStillRefusesALinkOutsideNeapsRepository()
    {
        string elsewhere = Json().Replace("https://github.com/lykosapps/Neap/", "https://github.com/someone/Else/", StringComparison.Ordinal);

        Assert.Throws<FormatException>(() => Release.Parse(elsewhere, installable: false));
    }

    [Fact]
    public void AReleaseKeptAsJsonIsReadBackAsItWas()
    {
        var release = Release.Parse(Json(body: "## Notes\n\n- It does a \"thing\"."));

        Assert.Equal(release, Release.Parse(release.ToJson()));
    }

    [Fact]
    public void AKeptReleaseWhoseLinkWasChangedIsRefused()
    {
        string tampered = Release.Parse(Json()).ToJson()
            .Replace("https://github.com/", "https://example.net/", StringComparison.Ordinal);

        Assert.Throws<FormatException>(() => Release.Parse(tampered));
    }

    [Fact]
    public void AReleaseWithoutNotesHasEmptyNotes()
    {
        Assert.Equal("", Release.Parse(Json(body: null)).Notes);
    }

    [Theory]
    [InlineData("0.3")]
    [InlineData("v0.3")]
    [InlineData("v0.3.0-beta")]
    [InlineData("release-1")]
    public void ATagThatIsNotAVersionIsRefused(string tag)
    {
        Assert.Throws<FormatException>(() => Release.Parse(Json(tag: tag)));
    }

    [Fact]
    public void ADraftOrAPreReleaseIsRefused()
    {
        Assert.Throws<FormatException>(() => Release.Parse(Json(draft: true)));
        Assert.Throws<FormatException>(() => Release.Parse(Json(prerelease: true)));
    }

    [Fact]
    public void ADownloadFromAnywhereButGitHubIsRefused()
    {
        Assert.Throws<FormatException>(() => Release.Parse(Json(host: "github.com.example.net")));
    }

    [Theory]
    [InlineData("someone/else")]
    [InlineData("lykosapps/Neapx")]
    [InlineData("lykosapps/Neap/../Other")]
    public void ADownloadFromAnotherRepositoryIsRefused(string repository)
    {
        Assert.Throws<FormatException>(() => Release.Parse(Json(repository: repository)));
    }

    [Fact]
    public void AReleasePageInAnotherRepositoryIsRefused()
    {
        string other = Json().Replace("\"html_url\": \"https://github.com/lykosapps/Neap/",
            "\"html_url\": \"https://github.com/someone/else/", StringComparison.Ordinal);

        Assert.Throws<FormatException>(() => Release.Parse(other));
    }

    [Fact]
    public void ARepositoryNameInADifferentCaseIsStillNeaps()
    {
        // GitHub treats the two as the same repository.
        Assert.Equal(new Version(0, 3, 0), Release.Parse(Json(repository: "LykosApps/neap")).Version);
    }

    [Fact]
    public void AReleaseWithoutTheZipForItsVersionIsRefused()
    {
        Assert.Throws<FormatException>(() => Release.Parse(Json(tag: "v0.3.0", zipName: "Neap-0.2.0-win-x64.zip")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""{ "message": "API rate limit exceeded" }""")]
    [InlineData("""{ "tag_name": 3, "draft": false, "prerelease": false }""")]
    public void AnAnswerThatIsNotAReleaseIsRefused(string json)
    {
        Assert.Throws<FormatException>(() => Release.Parse(json));
    }

    [Fact]
    public void TheHashFileGivesTheHashInLowerCase()
    {
        var release = Release.Parse(Json());
        Assert.Equal(Hash, release.HashFrom($"{Hash.ToUpperInvariant()}  Neap-0.3.0-win-x64.zip\r\n"));
    }

    [Theory]
    [InlineData(Hash + "  Neap-0.2.0-win-x64.zip")]
    [InlineData("abc  Neap-0.3.0-win-x64.zip")]
    [InlineData(Hash)]
    [InlineData("")]
    public void AHashFileForAnythingElseIsRefused(string file)
    {
        Assert.Throws<FormatException>(() => Release.Parse(Json()).HashFrom(file));
    }
}
