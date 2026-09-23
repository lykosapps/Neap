using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace StealthPro.App.Services;

/// <summary>
/// The app's words, from Strings/en-US/Resources.resw. The XAML takes its
/// text from the same file through x:Uid, so one translation covers both.
/// </summary>
public static class Strings
{
    private static readonly ResourceLoader Loader = new();

    public static string Get(string key) => Loader.GetString(key);

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
