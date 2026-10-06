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
/// the install steps added, in Markdown. The install steps are for someone
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
    /// <summary>The heading of the section left out.</summary>
    private const string InstallHeading = "Install";

    /// <summary>Reads the notes. An empty or unreadable body gives no blocks.</summary>
    public static IReadOnlyList<NoteBlock> Of(string body)
    {
        var blocks = new List<NoteBlock>();
        var paragraph = new List<string>();
        bool skipping = false;

        void EndParagraph()
        {
            if (paragraph.Count > 0) blocks.Add(new(NoteKind.Paragraph, Spans(string.Join(' ', paragraph))));
            paragraph.Clear();
        }

        // The release workflow writes the notes with a byte-order mark, which
        // would show as a stray space.
        foreach (string raw in body.Replace("﻿", "", StringComparison.Ordinal).ReplaceLineEndings("\n").Split('\n'))
        {
            string line = raw.Trim();
            var heading = Heading().Match(line);
            if (heading.Success)
            {
                EndParagraph();
                string text = heading.Groups[1].Value.Trim();
                skipping = text.Equals(InstallHeading, StringComparison.OrdinalIgnoreCase);
                if (!skipping) blocks.Add(new(NoteKind.Heading, Spans(text)));
            }
            else if (skipping)
            {
                continue;
            }
            else if (line.Length == 0)
            {
                EndParagraph();
            }
            else if (Bullet().Match(line) is { Success: true } bullet)
            {
                EndParagraph();
                blocks.Add(new(NoteKind.Bullet, Spans(bullet.Groups[1].Value)));
            }
            else
            {
                paragraph.Add(line);
            }
        }
        EndParagraph();
        return blocks;
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
