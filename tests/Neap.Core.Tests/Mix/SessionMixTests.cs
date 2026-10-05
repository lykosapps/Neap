using Neap.Core.Mix;

namespace Neap.Core.Tests.Mix;

public sealed class SessionMixTests : IDisposable
{
    private readonly string _journal = Path.Combine(Path.GetTempPath(), $"journal-{Guid.NewGuid():N}.json");

    public void Dispose() => File.Delete(_journal);

    private sealed class Session(string program, float volume, bool ours = false) : IPlaybackSession
    {
        public string? Id => program;
        public string Program => program;
        public string Display => program;
        public bool Ours => ours;
        public bool Playing { get; set; } = true;
        public float Volume { get; set; } = volume;
    }

    private sealed class Device(string id, params Session[] sessions) : IPlaybackDevice
    {
        public string Id => id;
        public string Name => id;
        public IReadOnlyList<IPlaybackSession> Sessions() => sessions;
        public void Dispose() { }
    }

    /// <summary>The outputs present, and the one the person is listening on when it is the headset.</summary>
    private sealed class Playback(params Device[] devices) : IPlayback
    {
        public Device? Listening { get; set; } = devices[0];
        public IPlaybackDevice? Headset() => Listening;
        public IEnumerable<IPlaybackDevice> Others(IPlaybackDevice headset) => devices.Where(d => d.Id != headset.Id);
        public IPlaybackDevice? Find(string id) => devices.FirstOrDefault(d => d.Id == id);
        public void Dispose() { }
    }

    private SessionMix Running(Playback playback, int mix)
    {
        var running = new SessionMix(playback, ["Discord"], new VolumeJournal(_journal));
        running.Start();
        running.SetMix(mix);
        running.ApplyNow();
        return running;
    }

    [Fact]
    public void ChatAndEverythingElseAreEachHeldToTheirSideOfTheMix()
    {
        Session discord = new("Discord", 1.0f), spotify = new("Spotify", 0.8f);
        using var mix = Running(new Playback(new Device("dock", discord, spotify)), 75);

        Assert.Equal(1.0f, discord.Volume, 3);
        Assert.Equal(0.4f, spotify.Volume, 3);

        mix.SetMix(25);
        mix.ApplyNow();

        Assert.Equal(0.5f, discord.Volume, 3);
        Assert.Equal(0.8f, spotify.Volume, 3);
    }

    [Fact]
    public void ItsOwnSoundIsLeftAlone()
    {
        Session ours = new("Neap", 1.0f, ours: true);
        using var mix = Running(new Playback(new Device("dock", ours)), 100);

        Assert.Equal(1.0f, ours.Volume, 3);
    }

    [Fact]
    public void EverythingGoesBackWhenTheHeadsetIsNoLongerTheOutput()
    {
        Session spotify = new("Spotify", 0.8f);
        var playback = new Playback(new Device("dock", spotify));
        using var mix = Running(playback, 75);

        playback.Listening = null;
        mix.ApplyNow();

        Assert.Equal(0.8f, spotify.Volume, 3);
        Assert.False(mix.Status.HeadsetIsOutput);
    }

    [Fact]
    public void MovingToAnotherOfTheHeadsetsOutputsPutsTheFirstBack()
    {
        Session onDock = new("Spotify", 0.8f), onTransmitter = new("Spotify", 0.8f);
        Device dock = new("dock", onDock), transmitter = new("transmitter", onTransmitter);
        var playback = new Playback(dock, transmitter);
        using var mix = Running(playback, 75);

        playback.Listening = transmitter;
        mix.ApplyNow();

        Assert.Equal(0.8f, onDock.Volume, 3);
        Assert.Equal(0.4f, onTransmitter.Volume, 3);
    }

    [Fact]
    public void StoppingPutsEverythingBack()
    {
        Session spotify = new("Spotify", 0.8f);
        var mix = Running(new Playback(new Device("dock", spotify)), 75);

        mix.Dispose();

        Assert.Equal(0.8f, spotify.Volume, 3);
    }

    [Fact]
    public void ChatPlayingOnAnOutputThatIsNotTheHeadsetIsFound()
    {
        var playback = new Playback(new Device("dock", new Session("Spotify", 1.0f)),
            new Device("speakers", new Session("Discord", 1.0f)));
        using var mix = Running(playback, 50);

        Assert.Equal(new ChatElsewhere("Discord", "speakers"), mix.Status.Elsewhere);
    }

    [Fact]
    public void ChatPlayingOnAnotherDeviceIsReported()
    {
        var found = SessionMix.Elsewhere([
            new("Discord", "Speakers (2- Stealth Pro II Xbox)", Playing: true),
        ]);

        Assert.Equal(new ChatElsewhere("Discord", "Speakers (2- Stealth Pro II Xbox)"), found);
    }

    [Fact]
    public void AnIdleSessionLeftOnAnotherDeviceIsNotReported()
    {
        // Applications keep idle sessions on every device they have played to.
        Assert.Null(SessionMix.Elsewhere([
            new("Discord", "Speakers (Realtek(R) Audio)", Playing: false),
        ]));
    }

    [Fact]
    public void ThePlayingDeviceIsReportedOverAnIdleOne()
    {
        var found = SessionMix.Elsewhere([
            new("Discord", "Speakers (Realtek(R) Audio)", Playing: false),
            new("Discord", "Speakers (2- Stealth Pro II Xbox)", Playing: true),
        ]);

        Assert.Equal("Speakers (2- Stealth Pro II Xbox)", found?.Device);
    }
}
