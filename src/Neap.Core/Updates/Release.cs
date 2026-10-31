using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Neap.Core.Updates;

/// <summary>The download a system puts in place of the running copy when it updates.</summary>
public enum Package
{
    /// <summary>None: the system only points people to the release page.</summary>
    None,

    /// <summary>Windows' zip, unzipped over Neap's folder.</summary>
    WindowsZip,

    /// <summary>Linux's AppImage, put in place of the one running.</summary>
    AppImage,
}

/// <summary>A published version of Neap: its number, its release page and its download.</summary>
/// <remarks>
/// <para>
/// This is Neap's own update, never the headset's firmware, which Neap leaves
/// to Swarm II.
/// </para>
/// <para>
/// Read from GitHub's description of the latest release. Anything in it that
/// is not exactly what the release workflow publishes is refused rather than
/// guessed at, and every link in it must be to Neap's own repository, so a release page edited by hand, or an answer from something
/// other than GitHub, reads as "cannot tell", never as "nothing new".
/// </para>
/// </remarks>
/// <param name="Version">The version, as major, minor and patch.</param>
/// <param name="Page">The release's page on GitHub, which carries what's new.</param>
/// <param name="Package">The download this system puts in place of the running one, or <see cref="Package.None"/>.</param>
/// <param name="Download">Where that download comes from; null for a system that only points people to the release page.</param>
/// <param name="Hash">Where the download's SHA-256 file comes from; null with <paramref name="Download"/>.</param>
/// <param name="Notes">What's new, as the release page says it, in Markdown; read by <see cref="ReleaseNotes"/>.</param>
public sealed partial record Release(Version Version, Uri Page, Package Package, Uri? Download, Uri? Hash, string Notes)
{
    /// <summary>Where GitHub describes the latest release, leaving out drafts and pre-releases.</summary>
    public static Uri Latest { get; } = new("https://api.github.com/repos/lykosapps/Neap/releases/latest");

    /// <summary>Where every link in a release must start: Neap's own repository, so a release can't name another's files.</summary>
    private const string Repository = "/lykosapps/Neap/";

    /// <summary>The download's file name, as the release workflow names it, or null for <see cref="Package.None"/>.</summary>
    public string? DownloadName => NameFor(Package, Version);

    private static string? NameFor(Package package, Version version) => package switch
    {
        Package.WindowsZip => string.Create(CultureInfo.InvariantCulture, $"Neap-{version.ToString(3)}-win-x64.zip"),
        Package.AppImage => string.Create(CultureInfo.InvariantCulture, $"Neap-{version.ToString(3)}-x86_64.AppImage"),
        _ => null,
    };

    /// <summary>A version as Neap shows it: a release's three numbers, or a test build's four.</summary>
    /// <remarks>A test build carries its tag's fourth number, which tells a report from a test build apart from one from the release.</remarks>
    public static string Display(Version version) => version.ToString(version.Revision > 0 ? 4 : 3);

    /// <summary>Whether this release is newer than the version running.</summary>
    /// <remarks>Only major, minor and patch count: a test build's fourth number never makes it newer than the release it follows.</remarks>
    public bool IsNewerThan(Version running) =>
        Version > new Version(running.Major, running.Minor, Math.Max(running.Build, 0));

    /// <summary>Reads GitHub's description of a release.</summary>
    /// <param name="json">GitHub's description of the release.</param>
    /// <param name="package">
    /// The download this system puts in place of the running one, which the
    /// release must have with its hash. <see cref="Package.None"/> where the
    /// system only tells people a version is out and points them to the
    /// release page.
    /// </param>
    /// <exception cref="FormatException">It is not a release as the workflow publishes one.</exception>
    public static Release Parse(string json, Package package = Package.WindowsZip)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean())
                throw new FormatException("the release is a draft or a pre-release");

            string tag = root.GetProperty("tag_name").GetString() ?? "";
            if (!Tag().IsMatch(tag)) throw new FormatException($"the release's tag {tag} is not a version");
            var version = Version.Parse(tag[1..]);

            string? wanted = NameFor(package, version);
            Uri? download = null, hash = null;
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                string name = asset.GetProperty("name").GetString() ?? "";
                if (name == wanted) download = InNeapRepository(asset.GetProperty("browser_download_url"));
                else if (name == wanted + ".sha256") hash = InNeapRepository(asset.GetProperty("browser_download_url"));
            }
            if (wanted is not null && (download is null || hash is null))
                throw new FormatException($"the release has no {wanted} with its .sha256 beside it");

            string notes = root.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.String
                ? body.GetString() ?? "" : "";
            return new Release(version, InNeapRepository(root.GetProperty("html_url")), package, download, hash, notes);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new FormatException($"the release could not be read: {ex.Message}", ex);
        }
    }

    /// <summary>The release in the shape GitHub describes one, with only what <see cref="Parse"/> reads.</summary>
    /// <remarks>
    /// Kept so that a version found today is still known after a restart,
    /// until the next check. Reading it back goes through <see cref="Parse"/>,
    /// so what was kept is checked as strictly as what GitHub sent.
    /// </remarks>
    public string ToJson() => JsonSerializer.Serialize(new
    {
        tag_name = $"v{Version.ToString(3)}",
        html_url = Page,
        draft = false,
        prerelease = false,
        body = Notes,
        assets = Download is null || Hash is null ? Array.Empty<object>()
            : new object[]
            {
                new { name = DownloadName, browser_download_url = Download },
                new { name = DownloadName + ".sha256", browser_download_url = Hash },
            },
    });

    /// <summary>Reads the download's SHA-256 file and gives its hash, in lower case.</summary>
    /// <remarks>The file is one line: the hash, two spaces, and the download's name.</remarks>
    /// <exception cref="FormatException">It is not a hash for this release's download.</exception>
    public string HashFrom(string file)
    {
        var match = HashLine().Match(file.Trim());
        if (DownloadName is null || !match.Success || match.Groups[2].Value != DownloadName)
            throw new FormatException($"the hash file is not one for {DownloadName}");
        return match.Groups[1].Value.ToLowerInvariant();
    }

    /// <summary>A link from the release, which must be to a page of Neap's repository on GitHub, over HTTPS.</summary>
    /// <remarks>
    /// <see cref="Uri"/> has already resolved any <c>..</c> in the path by
    /// the time it is checked, so a link can't climb out of the repository.
    /// </remarks>
    private static Uri InNeapRepository(JsonElement value)
    {
        if (!Uri.TryCreate(value.GetString(), UriKind.Absolute, out var link)
            || link.Scheme != Uri.UriSchemeHttps
            || !link.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || !link.AbsolutePath.StartsWith(Repository, StringComparison.OrdinalIgnoreCase))
            throw new FormatException($"the release links somewhere other than Neap's repository on GitHub: {value.GetString()}");
        return link;
    }

    [GeneratedRegex(@"^v\d{1,5}\.\d{1,5}\.\d{1,5}$")]
    private static partial Regex Tag();

    [GeneratedRegex(@"^([0-9a-fA-F]{64}) {1,2}\*?(\S+)$")]
    private static partial Regex HashLine();
}
