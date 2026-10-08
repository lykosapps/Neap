using System.Text.RegularExpressions;
using Neap.Core.Tests.Resources;
using Neap.Core.Updates;

namespace Neap.Core.Tests.Updates;

public class ReleaseNotesTests
{
    /// <summary>The notes as the release workflow writes them: a lead, the install steps, then the sections.</summary>
    private const string Notes = """
        **Own another headset? Help bring Neap to it.** Start from a [recording](https://github.com/lykosapps/Neap/blob/main/docs/MAPPING.md).

        To update from 0.1.0, quit Neap.

        ### Install on Windows

        1. Download **Neap-0.2.0-win-x64.zip** below.
        2. Unzip it and run **Neap.exe**.

        ### Install on Linux

        1. Download **Neap-0.2.0-x86_64.AppImage** below, make it runnable, and run it.

        ### Updates

        - **Neap updates itself.** It asks GitHub once a day.
        - Nothing about you is sent.
        """;

    private static IReadOnlyList<NoteBlock> Read(string body) => ReleaseNotes.Of(body);

    private static string Words(NoteBlock block) => string.Concat(block.Spans.Select(s => s.Text));

    [Fact]
    public void TheLeadSectionsAndBulletsComeInOrder()
    {
        var blocks = Read(Notes);

        Assert.Equal(
            [NoteKind.Paragraph, NoteKind.Paragraph, NoteKind.Heading, NoteKind.Bullet, NoteKind.Bullet],
            blocks.Select(b => b.Kind));
        Assert.Equal("Updates", Words(blocks[2]));
    }

    [Fact]
    public void TheInstallStepsAreLeftOut()
    {
        var all = string.Concat(Read(Notes).Select(Words));

        Assert.DoesNotContain("Unzip", all, StringComparison.Ordinal);
        Assert.DoesNotContain("AppImage", all, StringComparison.Ordinal);
        Assert.DoesNotContain("Install", all, StringComparison.Ordinal);
    }

    [Fact]
    public void WhatFollowsTheInstallStepsIsKept()
    {
        Assert.Equal("Nothing about you is sent.", Words(Read(Notes)[^1]));
    }

    [Fact]
    public void BoldWordsAreBold()
    {
        var bullet = Read(Notes)[3];

        Assert.Equal(new NoteSpan("Neap updates itself.", Bold: true), bullet.Spans[0]);
        Assert.Equal(new NoteSpan(" It asks GitHub once a day."), bullet.Spans[1]);
    }

    [Fact]
    public void ALinkOverHttpsKeepsItsAddress()
    {
        var lead = Read(Notes)[0];
        var link = lead.Spans.Single(s => s.Link is not null);

        Assert.Equal("recording", link.Text);
        Assert.Equal("https://github.com/lykosapps/Neap/blob/main/docs/MAPPING.md", link.Link!.AbsoluteUri);
    }

    [Theory]
    [InlineData("[open](http://example.com)")]
    [InlineData("[run](file:///C:/Windows/notepad.exe)")]
    [InlineData("[go](ms-settings:sound)")]
    [InlineData("[x](not a link)")]
    public void ALinkToAnythingButAWebPageIsJustItsWords(string markdown)
    {
        var spans = Read(markdown)[0].Spans;

        Assert.All(spans, s => Assert.Null(s.Link));
    }

    [Fact]
    public void AByteOrderMarkIsNotShown()
    {
        Assert.Equal("A Windows app", Words(Read("﻿A Windows app")[0]));
    }

    [Fact]
    public void CodeIsShownAsItsWords()
    {
        Assert.Equal("Press Ctrl now", Words(Read("Press `Ctrl` now")[0]));
    }

    [Fact]
    public void WrappedLinesOfOneParagraphJoinIntoOne()
    {
        var blocks = Read("one two\nthree four\n\nfive");

        Assert.Equal(["one two three four", "five"], blocks.Select(Words));
    }

    [Fact]
    public void MarkdownNotInTheChangelogIsShownAsTheWordsItIs()
    {
        Assert.Equal("a * b ** c", Words(Read("a * b ** c")[0]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n\n  ")]
    [InlineData("### Install\n\n1. Download it.")]
    [InlineData("### Install on Windows\n\n1. Download it.")]
    public void NothingToReadGivesNoBlocks(string body)
    {
        Assert.Empty(Read(body));
    }

    /// <summary>A changelog as the repository keeps it: wrapped lines, newest version first.</summary>
    private const string Changelog = """
        # Changelog

        Intro, not part of any version.

        ## Unreleased

        - Not out yet.

        ## 0.2.1 — 2026-10-08

        **A small fix.**

        ### Fixed

        - The beep plays again on Windows.
          In 0.2.0 it went silent.

        ## 0.2.0 — 2026-10-08

        - Linux.
        """;

    [Fact]
    public void AVersionsSectionIsReadUpToTheNextVersion()
    {
        var blocks = ReleaseNotes.ForVersion(Changelog, new Version(0, 2, 1));

        Assert.Equal([NoteKind.Paragraph, NoteKind.Heading, NoteKind.Bullet], blocks.Select(b => b.Kind));
        Assert.Equal("Fixed", Words(blocks[1]));
    }

    [Fact]
    public void AWrappedBulletIsOneBullet()
    {
        var blocks = ReleaseNotes.ForVersion(Changelog, new Version(0, 2, 1));

        Assert.Equal("The beep plays again on Windows. In 0.2.0 it went silent.", Words(blocks[^1]));
    }

    [Fact]
    public void AVersionTheChangelogLacksHasNoNotes()
    {
        Assert.Empty(ReleaseNotes.ForVersion(Changelog, new Version(0, 2, 2)));
    }

    [Fact]
    public void TheBuiltInChangelogHasNotesForTheVersionBeingBuilt()
    {
        string props = File.ReadAllText(Path.Combine(AppSource.RepoRoot(), "Directory.Build.props"));
        var version = Version.Parse(Regex.Match(props, "<Version>([^<]+)</Version>").Groups[1].Value);

        Assert.NotEmpty(ReleaseNotes.ForVersion(ReleaseNotes.BuiltIn, version));
    }
}
