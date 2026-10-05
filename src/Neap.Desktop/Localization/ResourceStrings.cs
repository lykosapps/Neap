using System.Reflection;
using System.Xml.Linq;

namespace Neap.Desktop.Localization;

/// <summary>The app's words, read from the resource file the Windows app uses.</summary>
/// <remarks>
/// <para>
/// One file holds every sentence, so a translation covers both. Entries are
/// named <c>Key</c> for a sentence asked for from code and <c>Uid.Property</c>
/// for one a screen's control takes for itself; see <see cref="Uid"/>.
/// </para>
/// <para>
/// A key that is not there throws rather than showing a blank: a missing
/// string is a fault to find at once, not a control with no name.
/// </para>
/// </remarks>
public static class ResourceStrings
{
    private static readonly Lazy<IReadOnlyDictionary<string, string>> Entries = new(Load);

    /// <summary>The text for a key.</summary>
    /// <exception cref="KeyNotFoundException">The resource file has no such entry.</exception>
    public static string Get(string key) =>
        Entries.Value.TryGetValue(key, out string? text)
            ? text
            : throw new KeyNotFoundException($"the resource file has no entry named '{key}'");

    /// <summary>Every property a control with this uid takes its text for, by property name.</summary>
    public static IEnumerable<KeyValuePair<string, string>> Of(string uid)
    {
        string prefix = uid + ".";
        foreach (var (key, text) in Entries.Value)
            if (key.StartsWith(prefix, StringComparison.Ordinal))
                yield return new KeyValuePair<string, string>(key[prefix.Length..], text);
    }

    private static IReadOnlyDictionary<string, string> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Resources.resw")
            ?? throw new InvalidOperationException("the resource file is not part of this build");
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var data in XDocument.Load(stream).Root!.Elements("data"))
            entries[(string)data.Attribute("name")!] = data.Element("value")?.Value ?? "";
        return entries;
    }
}
