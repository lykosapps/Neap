using System.Globalization;

namespace Neap.Services;

/// <summary>
/// The app's words, from Strings/en-US/Resources.resw. The screens take their
/// text from the same file, so one translation covers both.
/// </summary>
/// <remarks>
/// The file is read by whatever the app was built on: set <see cref="Source"/>
/// once at launch, before any service starts.
/// </remarks>
public static class Strings
{
    /// <summary>Looks a key up in the resource file.</summary>
    public static Func<string, string> Source { get; set; } =
        key => throw new InvalidOperationException($"no string source was set, so '{key}' cannot be read");

    public static string Get(string key) => Source(key);

    /// <summary>A sentence with the parts that vary put into it, as {0}, {1}.</summary>
    public static string Format(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);

    /// <summary>"a", "a and b", "a, b and c", in the words of the language.</summary>
    public static string List(IReadOnlyList<string> items) => items.Count switch
    {
        0 => "",
        1 => items[0],
        _ => Format("List_Last",
            string.Join(Get("List_Separator"), items.Take(items.Count - 1)), items[^1]),
    };
}
