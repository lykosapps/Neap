using System.Text.RegularExpressions;

namespace Neap.Core.Diagnostics;

/// <summary>
/// Blanks what would identify a person or their hardware in text that is
/// meant to be sent to someone else.
/// </summary>
/// <remarks>
/// <para>
/// A recording goes into a bug report, and a bug report can be public. The
/// headset's serial number and the radio addresses of its transmitters are
/// unique to one person's hardware; the Windows account name, the PC's name
/// and the profile folder turn up in error messages. None of them helps
/// diagnose anything.
/// </para>
/// <para>
/// Anything shaped like a radio address is blanked, not only the addresses
/// the headset named, because the transmitter records hold addresses in
/// fields not yet identified.
/// </para>
/// </remarks>
public sealed partial class Redaction
{
    /// <summary>What a blanked value reads as.</summary>
    public const string Removed = "(removed)";

    /// <summary>The shortest value that is blanked wherever it appears.</summary>
    public const int ShortestSecret = 4;

    private readonly List<(string Find, string Replace)> _exact = [];
    private readonly Regex? _account;

    /// <param name="secrets">
    /// Values to blank wherever they appear, such as a serial number. One
    /// shorter than <see cref="ShortestSecret"/> is ignored: a value that
    /// short turns up inside ordinary numbers, and blanking it there would
    /// wreck the text.
    /// </param>
    /// <param name="profileFolder">The Windows profile folder, written as %USERPROFILE%.</param>
    /// <param name="account">The Windows account name, blanked where it stands as a word of its own.</param>
    /// <param name="machine">The PC's name.</param>
    public Redaction(IEnumerable<string> secrets, string profileFolder = "", string account = "",
        string machine = "")
    {
        // The profile folder goes first: it contains the account name, and
        // reads better as %USERPROFILE% than as C:\Users\(removed).
        if (profileFolder.Length > 0) _exact.Add((profileFolder.TrimEnd('\\'), "%USERPROFILE%"));
        foreach (string secret in secrets.Where(s => s.Trim().Length >= ShortestSecret).Distinct()
                     .OrderByDescending(s => s.Length))
            _exact.Add((secret, Removed));
        if (machine.Length > 0) _exact.Add((machine, Removed));

        // A short account name is also an ordinary word or part of one, and
        // blanking it everywhere would wreck the text around it.
        if (account.Length >= 3)
            _account = new Regex($@"(?<![\w]){Regex.Escape(account)}(?![\w])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// <summary>The redaction for this PC, with the given values from the hardware.</summary>
    public static Redaction ForThisPc(IEnumerable<string> secrets) => new(secrets,
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.UserName, Environment.MachineName);

    /// <summary>The text with everything identifying blanked.</summary>
    public string Apply(string text)
    {
        foreach (var (find, replace) in _exact)
            text = text.Replace(find, replace, StringComparison.OrdinalIgnoreCase);
        if (_account is not null) text = _account.Replace(text, Removed);
        return RadioAddress().Replace(text, m => IsEmpty(m.Value) ? m.Value : Removed);
    }

    /// <summary>An empty transmitter slot's address says only that the slot is empty, so it stays.</summary>
    private static bool IsEmpty(string address) => address.All(c => c is '0' or ':' or '-');

    [GeneratedRegex(@"(?<![0-9A-Fa-f:-])[0-9A-Fa-f]{2}([:-])[0-9A-Fa-f]{2}(\1[0-9A-Fa-f]{2}){4}(?![0-9A-Fa-f:-])")]
    private static partial Regex RadioAddress();
}
