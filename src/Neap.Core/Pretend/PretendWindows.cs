using System.Globalization;
using Neap.Core.Audio;
using Neap.Core.Mix;

namespace Neap.Core.Pretend;

/// <summary>An application with audio open on the pretend headset.</summary>
public sealed record PretendSession(string Process, string Display, bool Playing);

/// <summary>
/// The half of the app's world that Windows owns, held in memory for a run
/// against the <see cref="PretendHeadset"/>: where sound and the microphone
/// are routed, the headset's volume, microphone level and formats, and the
/// applications playing to it.
/// </summary>
/// <remarks>
/// <para>
/// A pretend run happens on a machine somebody is using, so nothing here
/// reaches Windows. Changing the volume or format changes only these values,
/// and the mix sets no application's volume.
/// </para>
/// <para>
/// Windows points every role at the pretend Charging Dock and there is no
/// USB-C cable, so the app reads as connected with sound going to the right
/// place.
/// </para>
/// </remarks>
public sealed class PretendWindows
{
    public const string OutputName = "Pretend Headset (Charging Dock)";
    public const string InputName = "Pretend Headset Microphone (Charging Dock)";

    private static readonly string Dock =
        PretendHeadset.DockProduct.ToString("X4", CultureInfo.InvariantCulture);

    private readonly object _gate = new();
    private readonly Endpoint _output = new(OutputName, 45,
        [new(16, 48000, 2), new(16, 96000, 2), new(24, 48000, 2), new(24, 96000, 2)], 2);
    private readonly Endpoint _input = new(InputName, 70,
        [new(16, 16000, 1), new(16, 48000, 1)], 1);
    private int? _mix;

    private sealed class Endpoint(string name, int percent, AudioFormat[] options, int current)
    {
        public string Name { get; } = name;
        public int Percent { get; set; } = percent;
        public bool Muted { get; set; }
        public AudioFormat[] Options { get; } = options;
        public AudioFormat Current { get; set; } = options[current];
    }

    private Endpoint Of(Flow flow) => flow == Flow.Output ? _output : _input;

    /// <summary>
    /// Gets or sets the loudest level the pretend microphone hears, from 0 to
    /// 1, for the level meter; a real microphone is never opened.
    /// </summary>
    public float MicrophonePeak { get; set; } = 0.2f;

    /// <summary>The test tone last opened, or null if none has been.</summary>
    public PretendTone? Tone { get; private set; }

    /// <summary>Opens the test tone, which plays nowhere; what it is asked to play is kept for a script to read.</summary>
    public IPlayingTone OpenTone(double frequency) => Tone = new PretendTone(frequency);

    // -- routing ------------------------------------------------------------

    /// <summary>
    /// The default output or microphone, as <see cref="Routing.Default"/>
    /// reports it. The communications default is the same device.
    /// </summary>
    public static Routed Default(bool output) => new(output ? OutputName : InputName, Dock);

    /// <summary>The headset's own device over its cable; there is none.</summary>
    public static string Cable() => "";

    /// <summary>The devices belonging to a transmitter, as <see cref="Routing.Belonging"/> lists them.</summary>
    public static List<string> Belonging(string product, bool output) =>
        string.Equals(product, Dock, StringComparison.OrdinalIgnoreCase)
            ? [output ? OutputName : InputName] : [];

    // -- volume and format --------------------------------------------------

    public EndpointInfo Describe(Flow flow)
    {
        lock (_gate)
        {
            var endpoint = Of(flow);
            return new EndpointInfo(endpoint.Name, MatchedHeadset: true, endpoint.Percent, endpoint.Muted);
        }
    }

    public void SetPercent(int percent, Flow flow)
    {
        lock (_gate) Of(flow).Percent = Math.Clamp(percent, 0, 100);
    }

    public void SetMuted(bool muted, Flow flow)
    {
        lock (_gate) Of(flow).Muted = muted;
    }

    public FormatReport Formats(Flow flow)
    {
        lock (_gate)
        {
            var endpoint = Of(flow);
            return new FormatReport(endpoint.Name, endpoint.Current, endpoint.Options);
        }
    }

    /// <exception cref="Audio.FormatException">The device does not offer that format.</exception>
    public void ApplyFormat(int bits, int rate, Flow flow)
    {
        lock (_gate)
        {
            var endpoint = Of(flow);
            endpoint.Current = endpoint.Options.FirstOrDefault(f => f.Bits == bits && f.Rate == rate)
                ?? throw new Audio.FormatException($"{endpoint.Name} does not offer {bits}-bit {rate} Hz");
        }
    }

    // -- the mix ------------------------------------------------------------

    /// <summary>The applications with audio open on the headset.</summary>
    public static IReadOnlyList<PretendSession> Sessions { get; } =
    [
        new("PretendChat", "Pretend Chat", Playing: true),
        new("PretendGame", "Pretend Game", Playing: true),
        new("PretendMusic", "Pretend Music", Playing: false),
    ];

    /// <summary>The mix last applied while one was running, or null when none is.</summary>
    public int? Mix
    {
        get { lock (_gate) return _mix; }
    }

    /// <summary>A mix that records what it would set, and sets nothing.</summary>
    public IMixEngine CreateMix(IEnumerable<string> chatApps) => new PretendMix(this, chatApps);

    private sealed class PretendMix(PretendWindows windows, IEnumerable<string> chatApps) : IMixEngine
    {
        private List<string> _chatApps = chatApps.Where(a => !string.IsNullOrWhiteSpace(a)).ToList();
        private int _mix = 50;
        private bool _running;

        public IReadOnlyList<string> ChatApps
        {
            get { lock (windows._gate) return _chatApps.ToList(); }
            set { lock (windows._gate) _chatApps = value.ToList(); }
        }

        public SessionMixStatus Status
        {
            get
            {
                lock (windows._gate)
                {
                    int chat = Sessions.Count(s => _chatApps.Contains(s.Process, StringComparer.OrdinalIgnoreCase));
                    int game = _running ? Sessions.Count - chat : 0;
                    return new SessionMixStatus(_running, _mix, chat, game, _running ? chat + game : 0,
                        HeadsetIsOutput: true, string.Join(", ", _chatApps), Elsewhere: null);
                }
            }
        }

        public void Start()
        {
            lock (windows._gate)
            {
                _running = true;
                windows._mix = _mix;
            }
        }

        public int SetMix(int percent)
        {
            lock (windows._gate)
            {
                _mix = Math.Clamp(percent, 0, 100);
                if (_running) windows._mix = _mix;
                return _mix;
            }
        }

        public void Dispose()
        {
            lock (windows._gate)
            {
                _running = false;
                windows._mix = null;
            }
        }
    }
}
