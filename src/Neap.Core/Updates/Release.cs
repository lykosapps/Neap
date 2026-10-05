using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Neap.Core.Updates;

/// <summary>A published version of Neap: its number, its release page and its download.</summary>
/// <remarks>
/// <para>
/// This is Neap's own update, never the headset's firmware, which Neap leaves
/// to Swarm II.
/// </para>
/// <para>
/// Read from GitHub's description of the latest release. Anything in it that
/// is not exactly what the release workflow publishes is refused rather than
/// guessed at, so a release page edited by hand, or an answer from something
/// other than GitHub, reads as "cannot tell", never as "nothing new".
/// </para>
/// </remarks>
/// <param name="Version">The version, as major, minor and patch.</param>
/// <param name="Page">The release's page on GitHub, which carries what's new.</param>
/// <param name="Zip">Where the app's zip downloads from.</param>
/// <param name="Hash">Where the zip's SHA-256 file downloads from.</param>
/// <param name="Notes">What's new, as the release page says it, in Markdown; read by <see cref="ReleaseNotes"/>.</param>
public sealed partial record Release(Version Version, Uri Page, Uri Zip, Uri Hash, string Notes)
{
    /// <summary>Where GitHub describes the latest release, leaving out drafts and pre-releases.</summary>
    public static Uri Latest { get; } = new("https://api.github.com/repos/lykosapps/Neap/releases/latest");

    /// <summary>The zip's file name, as the release workflow names it.</summary>
    public string ZipName => ZipNameFor(Version);

    private static string ZipNameFor(Version version) =>
        string.Create(CultureInfo.InvariantCulture, $"Neap-{version.ToString(3)}-win-x64.zip");

    /// <summary>Whether this release is newer than the version running.</summary>
    /// <remarks>Only major, minor and patch count: a build's fourth part is always zero.</remarks>
    public bool IsNewerThan(Version running) =>
        Version > new Version(running.Major, running.Minor, Math.Max(running.Build, 0));

    /// <summary>Reads GitHub's description of a release.</summary>
    /// <exception cref="FormatException">It is not a release as the workflow publishes one.</exception>
    public static Release Parse(string json)
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

            string zipName = ZipNameFor(version);
            Uri? zip = null, hash = null;
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                string name = asset.GetProperty("name").GetString() ?? "";
                if (name == zipName) zip = OnGitHub(asset.GetProperty("browser_download_url"));
                else if (name == zipName + ".sha256") hash = OnGitHub(asset.GetProperty("browser_download_url"));
            }
            if (zip is null || hash is null)
                throw new FormatException($"the release has no {zipName} with its .sha256 beside it");

            string notes = root.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.String
                ? body.GetString() ?? "" : "";
            return new Release(version, OnGitHub(root.GetProperty("html_url")), zip, hash, notes);
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
        assets = new[]
        {
            new { name = ZipName, browser_download_url = Zip },
            new { name = ZipName + ".sha256", browser_download_url = Hash },
        },
    });

    /// <summary>Reads the zip's SHA-256 file and gives its hash, in lower case.</summary>
    /// <remarks>The file is one line: the hash, two spaces, and the zip's name.</remarks>
    /// <exception cref="FormatException">It is not a hash for this release's zip.</exception>
    public string HashFrom(string file)
    {
        var match = HashLine().Match(file.Trim());
        if (!match.Success || match.Groups[2].Value != ZipName)
            throw new FormatException($"the hash file is not one for {ZipName}");
        return match.Groups[1].Value.ToLowerInvariant();
    }

    /// <summary>A link from the release, which must be to GitHub over HTTPS.</summary>
    private static Uri OnGitHub(JsonElement value)
    {
        if (!Uri.TryCreate(value.GetString(), UriKind.Absolute, out var link)
            || link.Scheme != Uri.UriSchemeHttps
            || !link.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            throw new FormatException($"the release links somewhere other than GitHub: {value.GetString()}");
        return link;
    }

    [GeneratedRegex(@"^v\d{1,5}\.\d{1,5}\.\d{1,5}$")]
    private static partial Regex Tag();

    [GeneratedRegex(@"^([0-9a-fA-F]{64}) {1,2}\*?(\S+)$")]
    private static partial Regex HashLine();
}
