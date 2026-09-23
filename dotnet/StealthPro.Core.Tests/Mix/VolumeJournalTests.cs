using StealthPro.Core.Mix;

namespace StealthPro.Core.Tests.Mix;

public sealed class VolumeJournalTests : IDisposable
{
    private const string Dock = "{0.0.0.00000000}.{dock}";
    private const string Transmitter = "{0.0.0.00000000}.{transmitter}";
    private const string Spotify = Dock + "|spotify";
    private const string Firefox = Dock + "|firefox";
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"journal-{Guid.NewGuid():N}.json");

    public void Dispose() => File.Delete(_path);

    private sealed class Session(string id, float volume) : ISessionVolume
    {
        public string? Id { get; } = id;
        public float Volume { get; set; } = volume;
    }

    /// <summary>
    /// One pass at <paramref name="scale"/>, the way SessionMix runs it:
    /// journal first, then set. Returns what the session was set to.
    /// </summary>
    private static float Pass(VolumeJournal journal, string device, string id, float current, float scale)
    {
        if (journal.Wanted(device, id, current, scale) is not float wanted) return current;
        journal.Commit(device, [(id, wanted, scale)]);
        return wanted;
    }

    /// <summary>Spotify at 0.8 and Firefox at 1.0, both held to a quarter by the mix.</summary>
    private VolumeJournal Holding()
    {
        var journal = new VolumeJournal(_path);
        Pass(journal, Dock, Spotify, 0.8f, 0.25f);
        Pass(journal, Dock, Firefox, 1.0f, 0.25f);
        return journal;
    }

    [Fact]
    public void AnAppIsScaledFromTheLevelItWasFoundAt()
    {
        var journal = new VolumeJournal(_path);

        Assert.Equal(0.4f, Pass(journal, Dock, Spotify, 0.8f, 0.5f), 3);
    }

    [Fact]
    public void ALaterPassDoesNotRatchet()
    {
        var journal = new VolumeJournal(_path);
        float held = Pass(journal, Dock, Spotify, 0.8f, 0.5f);

        Assert.Null(journal.Wanted(Dock, Spotify, held, 0.5f));
        Assert.Equal(0.8f, Pass(journal, Dock, Spotify, held, 1f), 3);
    }

    [Fact]
    public void AVolumeThePersonSetIsLeftAlone()
    {
        var journal = new VolumeJournal(_path);
        Pass(journal, Dock, Firefox, 1.0f, 1f);

        // Turned down to half in Windows' own mixer, with the mix at centre.
        Assert.Null(journal.Wanted(Dock, Firefox, 0.5f, 1f));
        Assert.Null(journal.Wanted(Dock, Firefox, 0.5f, 1f));
    }

    [Fact]
    public void TheMixScalesFromThePersonsLevel()
    {
        var journal = new VolumeJournal(_path);
        Pass(journal, Dock, Firefox, 1.0f, 0.5f);

        // Turned down to 0.4 while held at half: that is what they want at
        // half, so at centre it is twice that.
        Assert.Null(journal.Wanted(Dock, Firefox, 0.4f, 0.5f));
        Assert.Equal(0.8f, Pass(journal, Dock, Firefox, 0.4f, 1f), 3);
    }

    [Fact]
    public void AnAppTurnedUpWhileSilencedStaysUp()
    {
        var journal = new VolumeJournal(_path);
        Pass(journal, Dock, Firefox, 1.0f, 0f);

        Assert.Null(journal.Wanted(Dock, Firefox, 0.3f, 0f));
        Assert.Null(journal.Wanted(Dock, Firefox, 0.3f, 0f));
        Assert.Equal(0.3f, Pass(journal, Dock, Firefox, 0.3f, 1f), 3);
    }

    [Fact]
    public void WhatIsAboutToBeAppliedIsOnDiskBeforehand()
    {
        Holding();

        var afterACrash = new VolumeJournal(_path);

        Assert.True(afterACrash.Owns(Dock));
        Assert.Equal(2, afterACrash.Count);
    }

    [Fact]
    public void OnlySessionsStillWhereTheMixLeftThemArePutBack()
    {
        var journal = Holding();
        var spotify = new Session(Spotify, 0.2f);
        var firefox = new Session(Firefox, 0.6f);      // moved by the person since

        journal.RestoreInto(Dock, [spotify, firefox]);

        Assert.Equal(0.8f, spotify.Volume);
        Assert.Equal(0.6f, firefox.Volume);
        Assert.Equal(0, journal.Count);
        Assert.False(File.Exists(_path));
    }

    [Fact]
    public void AnAppNotRunningIsKeptForNextTime()
    {
        var journal = Holding();

        journal.RestoreInto(Dock, [new Session(Spotify, 0.2f)]);

        Assert.Equal(1, journal.Count);
        Assert.Equal(1, new VolumeJournal(_path).Count);
    }

    [Fact]
    public void ARunThatWroteNothingNeverClearsAnotherRunsRecord()
    {
        Holding();

        var throwaway = new VolumeJournal(_path);
        throwaway.RestoreInto(Dock, []);

        Assert.Equal(2, new VolumeJournal(_path).Count);
    }

    [Fact]
    public void EachDeviceKeepsItsOwnRecord()
    {
        var journal = Holding();
        Pass(journal, Transmitter, Transmitter + "|discord", 1.0f, 0f);

        Assert.True(journal.Owns(Dock));
        Assert.True(journal.Owns(Transmitter));

        journal.RestoreInto(Transmitter, [new Session(Transmitter + "|discord", 0f)]);

        Assert.False(journal.Owns(Transmitter));
        Assert.True(journal.Owns(Dock));
    }

    [Fact]
    public void AnOldJournalIsSplitByDevice()
    {
        // The format before one record per device: everything under the last
        // device written to, including an entry stranded on another.
        File.WriteAllText(_path,
            $$$"""
            {"endpoint":"{{{Dock}}}","pid":1,"sessions":{
              "{{{Spotify}}}":{"original":0.8,"applied":0.4},
              "{{{Transmitter}}}|discord":{"original":1,"applied":0}
            } }
            """);

        var journal = new VolumeJournal(_path);
        var discord = new Session(Transmitter + "|discord", 0f);
        journal.RestoreInto(Transmitter, [discord]);

        Assert.True(journal.Owns(Dock));
        Assert.Equal(1f, discord.Volume);
        Assert.Equal(0.8f, Pass(journal, Dock, Spotify, 0.4f, 1f), 3);
    }
}
