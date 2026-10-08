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

    /// <summary>The device the pretend headset's sound goes through: the Charging Dock, or an Atlas Air's transmitter.</summary>
    private static readonly string Dock =
        (Environment.GetCommandLineArgs().Contains(PretendHeadset.AtlasFlag, StringComparer.OrdinalIgnoreCase)
            ? PretendHeadset.AtlasTransmitterProduct : PretendHeadset.DockProduct)
        .ToString("X4", CultureInfo.InvariantCulture);

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

    /// <summary>Where sound goes and how loud, as <see cref="Routing.Survey"/> reports it.</summary>
    /// <remarks>Every program reads as full volume, because the pretend mix sets none.</remarks>
    public SoundSurvey Survey()
    {
        lock (_gate)
        {
            string[] roles = ["Multimedia", "Console", "Communications"];
            var defaults = roles.Select(r => new SoundDefault(true, r, OutputName, Dock))
                .Concat(roles.Select(r => new SoundDefault(false, r, InputName, Dock))).ToList();
            var apps = Sessions.Select(s => new SoundApp(s.Process, 100, false, s.Playing)).ToList();
            return new SoundSurvey(defaults,
            [
                new(true, OutputName, Dock, _output.Percent, _output.Muted, 0, apps),
                new(false, InputName, Dock, _input.Percent, _input.Muted, MicrophonePeak, []),
            ],
                _output.Current.Label, _input.Current.Label);
        }
    }

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

    // -- spatial sound ------------------------------------------------------

    private Guid _spatial = Guid.Empty;

    /// <summary>Whether the pretend headset takes a spatial format: Windows Sonic and Dolby Atmos.</summary>
    public static bool SpatialSupported(Guid subtype) =>
        subtype == SpatialSound.Subtypes[SpatialFormat.WindowsSonic]
        || subtype == SpatialSound.Subtypes[SpatialFormat.DolbyAtmos];

    /// <summary>The spatial format on, empty for off.</summary>
    public Guid Spatial
    {
        get { lock (_gate) return _spatial; }
    }

    /// <summary>Gets or sets whether Dolby Atmos is licensed, so a script can see the note when it is not.</summary>
    public bool DolbyLicensed { get; set; } = true;

    public SpatialResult SetSpatial(Guid subtype)
    {
        lock (_gate)
        {
            if (subtype != Guid.Empty && !SpatialSupported(subtype)) return SpatialResult.NotSupportedOnAudioEndpoint;
            if (subtype == SpatialSound.Subtypes[SpatialFormat.DolbyAtmos] && !DolbyLicensed)
                return SpatialResult.LicenseNotValidForAudioEndpoint;
            _spatial = subtype;
            return SpatialResult.Succeeded;
        }
    }

    // -- running programs ---------------------------------------------------

    private readonly HashSet<string> _programs = new(Sessions.Select(s => s.Process), StringComparer.OrdinalIgnoreCase);

    /// <summary>Every program running, by process name: those playing to the headset, and whatever a script started.</summary>
    public IReadOnlyList<string> Programs()
    {
        lock (_gate) return _programs.Order(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>A program starts, as far as the app can tell.</summary>
    public void Start(string process)
    {
        lock (_gate) _programs.Add(process);
    }

    /// <summary>A program closes.</summary>
    /// <exception cref="ArgumentException">Nothing by that name is running.</exception>
    public void Stop(string process)
    {
        lock (_gate)
        {
            if (!_programs.Remove(process)) throw new ArgumentException($"no program called '{process}' is running");
        }
    }

    private readonly HashSet<string> _recording = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every program recording from a microphone, by process name: whatever a script started recording.</summary>
    public IReadOnlyList<string> Recording()
    {
        lock (_gate) return _recording.Order(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>A program starts recording from a microphone, as on a call.</summary>
    public void Record(string process)
    {
        lock (_gate) _recording.Add(process);
    }

    /// <summary>A program stops recording.</summary>
    /// <exception cref="ArgumentException">Nothing by that name is recording.</exception>
    public void StopRecording(string process)
    {
        lock (_gate)
        {
            if (!_recording.Remove(process)) throw new ArgumentException($"no program called '{process}' is recording");
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
