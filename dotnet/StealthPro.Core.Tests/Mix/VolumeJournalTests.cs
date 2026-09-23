using StealthPro.Core.Mix;

namespace StealthPro.Core.Tests.Mix;

public sealed class VolumeJournalTests : IDisposable
{
    private const string Headset = "headset-endpoint";
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"journal-{Guid.NewGuid():N}.json");

    public void Dispose() => File.Delete(_path);

    private sealed class Session(string id, float volume) : ISessionVolume
    {
        public string? Id { get; } = id;
        public float Volume { get; set; } = volume;
    }

    /// <summary>Spotify at 0.8 and Firefox at 1.0, both held down by the mix.</summary>
    private VolumeJournal Holding()
    {
        var journal = new VolumeJournal(_path);
        journal.Remember("spotify", 0.8f);
        journal.Remember("firefox", 1.0f);
        journal.Commit(Headset, [("spotify", 0.2f), ("firefox", 0.25f)]);
        return journal;
    }

    [Fact]
    public void TheOriginalIsRecordedOnceAndNeverRatchets()
    {
        var journal = new VolumeJournal(_path);

        Assert.Equal(0.8f, journal.Remember("spotify", 0.8f));
        Assert.Equal(0.8f, journal.Remember("spotify", 0.2f));
    }

    [Fact]
    public void WhatIsAboutToBeAppliedIsOnDiskBeforehand()
    {
        Holding();

        var afterACrash = new VolumeJournal(_path);

        Assert.True(afterACrash.Owns(Headset));
        Assert.Equal(2, afterACrash.Count);
    }

    [Fact]
    public void OnlySessionsStillWhereTheMixLeftThemArePutBack()
    {
        var journal = Holding();
        var spotify = new Session("spotify", 0.2f);
        var firefox = new Session("firefox", 0.6f);      // moved by the person since

        journal.RestoreInto([spotify, firefox]);

        Assert.Equal(0.8f, spotify.Volume);
        Assert.Equal(0.6f, firefox.Volume);
        Assert.Equal(0, journal.Count);
        Assert.False(File.Exists(_path));
    }

    [Fact]
    public void AnAppNotRunningIsKeptForNextTime()
    {
        var journal = Holding();

        journal.RestoreInto([new Session("spotify", 0.2f)]);

        Assert.Equal(1, journal.Count);
        Assert.Equal(1, new VolumeJournal(_path).Count);
    }

    [Fact]
    public void ARunThatWroteNothingNeverClearsAnotherRunsRecord()
    {
        Holding();

        var throwaway = new VolumeJournal(_path);
        throwaway.RestoreInto([]);

        Assert.Equal(2, new VolumeJournal(_path).Count);
    }

    [Fact]
    public void AnotherDeviceIsNotOwned()
    {
        var journal = Holding();

        Assert.False(journal.Owns("speakers"));
    }
}
