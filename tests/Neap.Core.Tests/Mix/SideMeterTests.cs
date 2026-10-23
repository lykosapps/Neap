using Neap.Core.Mix;

namespace Neap.Core.Tests.Mix;

public class SideMeterTests
{
    private sealed class Session(string program, float peak, bool ours = false) : IPlaybackSession
    {
        public string? Id => program;
        public string Program => program;
        public string Display => program;
        public bool Ours => ours;
        public bool Playing => true;
        public float Volume { get; set; } = 1;
        public float Peak { get; set; } = peak;
    }

    private sealed class Device(params Session[] sessions) : IPlaybackDevice
    {
        public string Id => "headset";
        public string Name => "Headset";
        public int Listed { get; private set; }
        /// <summary>How many listings fail before one succeeds, as a device being unplugged fails.</summary>
        public int Failing { get; set; }
        public IReadOnlyList<IPlaybackSession> Sessions()
        {
            Listed++;
            // AUDCLNT_E_DEVICE_INVALIDATED, raised as the runtime raises it for Windows.
            if (Failing-- > 0) System.Runtime.InteropServices.Marshal.ThrowExceptionForHR(unchecked((int)0x88890004));
            return sessions;
        }
        public void Dispose() { }
    }

    private sealed class Playback(Device? headset) : IPlayback
    {
        public IPlaybackDevice? Headset() => headset;
        public IEnumerable<IPlaybackDevice> Others(IPlaybackDevice headset) => [];
        public IPlaybackDevice? Find(string id) => null;
        public void Dispose() { }
    }

    [Fact]
    public void ReadsChatFromTheChosenAppsAndGameFromTheRestAndLeavesOutItsOwnSound()
    {
        var device = new Device(new Session("Discord", 0.4f), new Session("Game", 0.7f), new Session("Neap", 1f, ours: true));
        using var meter = new SideMeter(new Playback(device), () => ["Discord"]);

        Assert.Equal(new SideLevel(0.7f, 0.4f), meter.Read());
    }

    [Fact]
    public void ListsTheApplicationsOnceBetweenReadings()
    {
        var device = new Device(new Session("Game", 0.7f));
        using var meter = new SideMeter(new Playback(device), () => []);

        meter.Read();
        meter.Read();
        meter.Read();

        Assert.Equal(1, device.Listed);
    }

    [Fact]
    public void ADeviceGoingAwayReadsAsSilenceAndIsListedAgain()
    {
        var device = new Device(new Session("Game", 0.7f)) { Failing = 1 };
        using var meter = new SideMeter(new Playback(device), () => []);

        Assert.Equal(default, meter.Read());
        Assert.Equal(new SideLevel(0.7f, 0), meter.Read());
        Assert.Equal(2, device.Listed);
    }

    [Fact]
    public void IsSilentWhenTheHeadsetIsNotTheOutput()
    {
        using var meter = new SideMeter(new Playback(null), () => ["Discord"]);

        Assert.Equal(default, meter.Read());
    }

    [Fact]
    public void ReadsNothingOnceDisposed()
    {
        var meter = new SideMeter(new Playback(new Device(new Session("Game", 0.7f))), () => []);
        meter.Dispose();

        Assert.Equal(default, meter.Read());
    }
}
