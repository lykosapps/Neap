using System.Text.RegularExpressions;

namespace Neap.Core.Updates;

/// <summary>What a block of release notes is.</summary>
public enum NoteKind { Heading, Paragraph, Bullet }

/// <summary>A run of text in a block: plain, bold or a link.</summary>
/// <param name="Text">The words.</param>
/// <param name="Bold">Whether they are in bold.</param>
/// <param name="Link">Where they link to, or null.</param>
public sealed record NoteSpan(string Text, bool Bold = false, Uri? Link = null);

/// <summary>A heading, paragraph or bullet of release notes.</summary>
public sealed record NoteBlock(NoteKind Kind, IReadOnlyList<NoteSpan> Spans);

/// <summary>
/// Reads the notes a release carries into blocks the app can draw, so what
/// the person reads in Neap is what the release page says.
/// </summary>
/// <remarks>
/// <para>
/// The release page's notes are the version's section of the changelog with
/// the install steps added, in Markdown, under a heading for each system:
/// Install on Windows, Install on Linux. The install steps are for someone
/// downloading by hand, so they are left out here: the person reading is
/// already running Neap.
/// </para>
/// <para>
/// Only what the changelog uses is read: headings, bullets, bold and links.
/// Anything else is shown as the words it is. A link is kept only if it is
/// to a web page over HTTPS, so notes can't offer anything else to open.
/// </para>
/// </remarks>
public static partial class ReleaseNotes
{
    /// <summary>The first word of the headings of the sections left out.</summary>
    private const string InstallHeading = "Install";

    /// <summary>The changelog Neap was built with, in Markdown; empty if it was not built in.</summary>
    public static string BuiltIn { get; } = ReadBuiltIn();

    /// <summary>Reads the notes. An empty or unreadable body gives no blocks.</summary>
    /// <remarks>
    /// A line that is not a heading or a new bullet carries on the paragraph or
    /// bullet before it, as the changelog wraps its lines.
    /// </remarks>
    public static IReadOnlyList<NoteBlock> Of(string body)
    {
        var blocks = new List<NoteBlock>();
        var lines = new List<string>();
        var kind = NoteKind.Paragraph;
        bool skipping = false;

        void End()
        {
            if (lines.Count > 0) blocks.Add(new(kind, Spans(string.Join(' ', lines))));
            lines.Clear();
        }

        // The release workflow writes the notes with a byte-order mark, which
        // would show as a stray space.
        foreach (string raw in body.Replace("﻿", "", StringComparison.Ordinal).ReplaceLineEndings("\n").Split('\n'))
        {
            string line = raw.Trim();
            var heading = Heading().Match(line);
            if (heading.Success)
            {
                End();
                string text = heading.Groups[1].Value.Trim();
                skipping = text.Split(' ')[0].Equals(InstallHeading, StringComparison.OrdinalIgnoreCase);
                if (!skipping) blocks.Add(new(NoteKind.Heading, Spans(text)));
            }
            else if (skipping)
            {
                continue;
            }
            else if (line.Length == 0)
            {
                End();
            }
            else if (Bullet().Match(line) is { Success: true } bullet)
            {
                End();
                kind = NoteKind.Bullet;
                lines.Add(bullet.Groups[1].Value);
            }
            else
            {
                if (lines.Count == 0) kind = NoteKind.Paragraph;
                lines.Add(line);
            }
        }
        End();
        return blocks;
    }

    /// <summary>Reads one version's section of the changelog, as <see cref="Of"/> reads a release's notes.</summary>
    /// <param name="changelog">The changelog, in Markdown.</param>
    /// <param name="version">The version, as major, minor and patch.</param>
    /// <returns>The section's blocks, or none when the changelog has no section for the version.</returns>
    public static IReadOnlyList<NoteBlock> ForVersion(string changelog, Version version)
    {
        string[] lines = changelog.ReplaceLineEndings("\n").Split('\n');
        string heading = "## " + version.ToString(3);
        int start = Array.FindIndex(lines, l => l == heading || l.StartsWith(heading + " ", StringComparison.Ordinal));
        if (start < 0) return [];
        int end = Array.FindIndex(lines, start + 1, l => l.StartsWith("## ", StringComparison.Ordinal));
        return Of(string.Join('\n', lines[(start + 1)..(end < 0 ? lines.Length : end)]));
    }

    private static string ReadBuiltIn()
    {
        using var stream = typeof(ReleaseNotes).Assembly.GetManifestResourceStream("Neap.CHANGELOG.md");
        if (stream is null) return "";
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static List<NoteSpan> Spans(string text)
    {
        var spans = new List<NoteSpan>();
        int at = 0;
        foreach (Match match in Inline().Matches(text))
        {
            if (match.Index > at) spans.Add(new(text[at..match.Index]));
            at = match.Index + match.Length;

            if (match.Groups[1].Success) spans.Add(new(match.Groups[1].Value, Bold: true));
            else if (match.Groups[4].Success) spans.Add(new(match.Groups[4].Value));
            else if (Uri.TryCreate(match.Groups[3].Value, UriKind.Absolute, out var link) && link.Scheme == Uri.UriSchemeHttps)
                spans.Add(new(match.Groups[2].Value, Link: link));
            else spans.Add(new(match.Groups[2].Value));
        }
        if (at < text.Length) spans.Add(new(text[at..]));
        return spans;
    }

    [GeneratedRegex(@"^#{1,6}\s+(.+)$")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^(?:[-*]|\d+\.)\s+(.+)$")]
    private static partial Regex Bullet();

    /// <summary>Bold, a link, or code, in the order they are tried at each place.</summary>
    [GeneratedRegex(@"\*\*(.+?)\*\*|\[([^\]]+)\]\(([^)\s]+)\)|`([^`]+)`")]
    private static partial Regex Inline();
}
