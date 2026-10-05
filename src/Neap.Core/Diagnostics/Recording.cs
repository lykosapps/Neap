using System.Globalization;
using System.Text;
using System.Text.Json;
using Neap.Core.Audio;
using Neap.Core.Hid;
using Neap.Core.Protocol;
using Neap.Core.Settings;

namespace Neap.Core.Diagnostics;

/// <summary>What the headset, its transmitters and Windows showed at one moment.</summary>
/// <param name="At">When it was taken.</param>
/// <param name="Status">The connection state in one line; see <see cref="Connection.HeadsetStatus.Summary"/>.</param>
/// <param name="Plugged">The Turtle Beach collections Windows has, on every usage page.</param>
/// <param name="Transmitters">The headset's four transmitter slots, or none when they could not be read.</param>
/// <param name="Values">Everything the headset reported, keyed by lowercase hex.</param>
/// <param name="Sound">Where Windows sends sound and how loud, or null when it could not be read.</param>
/// <param name="Unread">What could not be read, and why, one line each.</param>
public sealed record Snapshot(
    DateTime At, string Status, IReadOnlyList<HidDeviceInfo> Plugged,
    IReadOnlyList<Transmitter> Transmitters, IReadOnlyDictionary<string, JsonElement> Values,
    SoundSurvey? Sound, IReadOnlyList<string> Unread);

/// <summary>
/// A record of a stretch of time, for a person to send with a bug report:
/// what was there at the start, everything the headset said and every level
/// Windows showed in between, what was there at the end, and the app's log.
/// </summary>
/// <remarks>
/// <para>
/// A problem is usually something that happens, not something that is: a
/// volume that drops, a setting that will not stay, a connection that comes
/// and goes. One reading after the fact shows where things settled, not what
/// moved them, so the headset's notifications and Windows' levels are kept
/// in time order between the two snapshots.
/// </para>
/// <para>
/// Unnamed values are kept and marked rather than dropped. They are how a
/// setting nobody has identified yet, or a headset Neap does not know, gets
/// mapped.
/// </para>
/// <para>
/// The timeline stops growing at <see cref="MostLines"/> and says so, so a
/// recording left running cannot grow without limit.
/// </para>
/// </remarks>
/// <param name="app">The app's name and version.</param>
/// <param name="windows">The Windows version.</param>
public sealed class Recording(string app, string windows)
{
    /// <summary>The most lines the timeline keeps.</summary>
    public const int MostLines = 5000;

    private readonly object _gate = new();
    private readonly List<string> _timeline = [];
    private readonly HashSet<string> _heard = [];
    private bool _full;
    private Snapshot? _start;
    private Snapshot? _end;
    private SoundSurvey? _lastSound;

    /// <summary>Values met in the recording that identify one person's hardware: the serial number and the radio addresses.</summary>
    public IReadOnlyList<string> Secrets
    {
        get
        {
            lock (_gate)
            {
                var found = new List<string>();
                foreach (var snapshot in new[] { _start, _end })
                {
                    if (snapshot is null) continue;
                    if (snapshot.Values.TryGetValue(SerialHex, out var serial)) found.Add(DeviceEvent.Render(serial));
                    found.AddRange(snapshot.Transmitters.Select(t => t.Address));
                }
                return found.Where(s => s.Length > 0).Distinct().ToList();
            }
        }
    }

    private static readonly string SerialHex = Registry.ByName["serial_number"].Key.ToString("x", CultureInfo.InvariantCulture);

    /// <summary>Takes the snapshot the recording starts from.</summary>
    public void Began(Snapshot snapshot)
    {
        lock (_gate)
        {
            _start = snapshot;
            _lastSound = snapshot.Sound;
        }
    }

    /// <summary>Notes something the headset said.</summary>
    public void Heard(DateTime at, DeviceEvent evt)
    {
        lock (_gate)
            foreach (var pair in evt.Values.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                _heard.Add(pair.Key);
                Add(at, $"headset  {evt.Kind}/{evt.Category}  {Value(pair.Key, pair.Value)}");
            }
    }

    /// <summary>Notes the levels Windows showed, and any change to where it sends sound.</summary>
    public void Sampled(DateTime at, SoundSurvey sound)
    {
        lock (_gate)
        {
            if (_lastSound is { } last)
                foreach (var now in sound.Defaults)
                {
                    var was = last.Defaults.FirstOrDefault(d => d.Output == now.Output && d.Role == now.Role);
                    if (was is null || was.Device != now.Device) Add(at, $"default  {Default(now)}");
                }
            _lastSound = sound;
            foreach (var device in sound.Devices) Add(at, $"sound    {Level(device)}");
        }
    }

    /// <summary>Notes something that could not be read during the recording.</summary>
    public void Failed(DateTime at, string what)
    {
        lock (_gate) Add(at, $"failed   {what}");
    }

    /// <summary>Takes the snapshot the recording ends with.</summary>
    public void Ended(Snapshot snapshot)
    {
        lock (_gate) _end = snapshot;
    }

    private void Add(DateTime at, string line)
    {
        if (_full) return;
        if (_timeline.Count >= MostLines)
        {
            _timeline.Add(Invariant($"{at:HH:mm:ss.f}  later lines were not kept: the recording reached {MostLines} lines"));
            _full = true;
            return;
        }
        _timeline.Add(Invariant($"{at:HH:mm:ss.f}  {line}"));
    }

    /// <summary>Whether the hardware plugged in is Turtle Beach's but none of it is hardware Neap knows; see <see cref="IssueForm.SupportRequest"/>.</summary>
    public bool ForAnotherHeadset
    {
        get
        {
            lock (_gate) return IssueForm.SupportRequest(Plugged());
        }
    }

    /// <summary>Which of Neap's functions the headset reported the values for; see <see cref="HeadsetCheck"/>.</summary>
    public HeadsetFindings Findings()
    {
        lock (_gate)
        {
            var reported = new HashSet<string>(_heard, StringComparer.Ordinal);
            var moved = new HashSet<string>(_heard, StringComparer.Ordinal);
            foreach (var snapshot in new[] { _start, _end })
                if (snapshot is not null) reported.UnionWith(snapshot.Values.Keys);
            if (_start is { } start && _end is { } end)
                foreach (var (key, value) in end.Values)
                    if (start.Values.TryGetValue(key, out var was) && DeviceEvent.Render(was) != DeviceEvent.Render(value))
                        moved.Add(key);
            return HeadsetCheck.Of(reported, moved);
        }
    }

    /// <summary>The form on GitHub to send this recording with, filled in; see <see cref="IssueForm"/>.</summary>
    /// <remarks>A request to support another headset carries the <see cref="Findings"/> with it.</remarks>
    public Uri Issue()
    {
        lock (_gate)
        {
            var plugged = Plugged();
            return IssueForm.For(plugged, app, windows,
                IssueForm.SupportRequest(plugged) ? Findings().Summary : null);
        }
    }

    /// <summary>The product ids plugged in at the start or the end. Under the gate.</summary>
    private List<string> Plugged() => new[] { _start, _end }
        .SelectMany(s => s?.Plugged ?? [])
        .Select(d => d.ProductId.ToString("X4", CultureInfo.InvariantCulture))
        .ToList();

    /// <summary>The recording as text, with everything that identifies the person blanked.</summary>
    /// <param name="log">The app's log, oldest line first.</param>
    /// <param name="redaction">What to blank; built from <see cref="Secrets"/> and the PC.</param>
    public string Write(IReadOnlyList<string> log, Redaction redaction)
    {
        var text = new StringBuilder();
        lock (_gate)
        {
            text.AppendLine("Neap recording");
            text.AppendLine(Culture, $"App: {app}");
            text.AppendLine(Culture, $"Windows: {windows}");
            if (_start is { } first)
            {
                text.AppendLine(Invariant($"Started: {first.At:yyyy-MM-dd HH:mm:ss}"));
                if (_end is { } last)
                    text.AppendLine(Invariant($"Ended: {last.At:yyyy-MM-dd HH:mm:ss}, {Duration(last.At - first.At)}"));
            }

            if (_start is { } start)
            {
                text.AppendLine().AppendLine("== At the start ==");
                WriteSnapshot(text, start, since: null);
            }

            text.AppendLine().AppendLine("== During ==");
            if (_timeline.Count == 0) text.AppendLine("Nothing happened.");
            foreach (string line in _timeline) text.AppendLine(line);

            if (_end is { } end)
            {
                text.AppendLine().AppendLine("== At the end ==");
                WriteSnapshot(text, end, since: _start);
            }
        }

        text.AppendLine().AppendLine("== App log ==");
        foreach (string line in log) text.AppendLine(line);
        return redaction.Apply(text.ToString());
    }

    private static void WriteSnapshot(StringBuilder text, Snapshot snapshot, Snapshot? since)
    {
        text.AppendLine(Culture, $"Headset: {snapshot.Status}");

        text.AppendLine("Plugged in:");
        if (snapshot.Plugged.Count == 0) text.AppendLine("  nothing from Turtle Beach");
        foreach (var device in snapshot.Plugged)
            text.AppendLine(Invariant($"  {Named(device.ProductId.ToString("X4", CultureInfo.InvariantCulture))}, usage page {device.UsagePage:x4}"));

        if (snapshot.Transmitters.Count > 0)
        {
            text.AppendLine("Transmitter slots:");
            foreach (var slot in snapshot.Transmitters)
                text.AppendLine(slot.Paired
                    ? $"  {slot.Slot}  {slot.Kind} ({slot.ProductId}), firmware {slot.Firmware}, "
                      + $"address {slot.Address}{(slot.Active ? ", in use" : "")}"
                      + $"  info [{string.Join(", ", slot.Info)}]  control [{string.Join(", ", slot.Control)}]"
                    : $"  {slot.Slot}  empty");
        }

        var values = snapshot.Values.OrderBy(p => Order(p.Key)).ThenBy(p => p.Key, StringComparer.Ordinal).ToList();
        if (since is null)
        {
            int unnamed = values.Count(p => NameOf(p.Key) is null);
            text.AppendLine(Invariant($"Headset values: {values.Count}, {unnamed} not yet identified"));
            foreach (var pair in values) text.AppendLine(Culture, $"  {Value(pair.Key, pair.Value)}");
        }
        else
        {
            var changed = values.Where(p => !since.Values.TryGetValue(p.Key, out var was)
                                            || DeviceEvent.Render(was) != DeviceEvent.Render(p.Value)).ToList();
            var gone = since.Values.Keys.Where(k => !snapshot.Values.ContainsKey(k)).Order(StringComparer.Ordinal).ToList();
            text.AppendLine(changed.Count + gone.Count == 0
                ? "Headset values: none changed since the start"
                : "Headset values changed since the start:");
            foreach (var pair in changed)
                text.AppendLine(since.Values.TryGetValue(pair.Key, out var was)
                    ? $"  {Value(pair.Key, pair.Value)}  (was {DeviceEvent.Render(was)})"
                    : $"  {Value(pair.Key, pair.Value)}  (new)");
            foreach (string key in gone) text.AppendLine(Culture, $"  0x{key,-5} {NameOf(key) ?? Unnamed,-26} no longer reported");
        }

        if (snapshot.Sound is { } sound)
        {
            text.AppendLine("Sound:");
            foreach (var role in sound.Defaults) text.AppendLine(Culture, $"  {Default(role)}");
            text.AppendLine(Culture, $"  headset output format: {sound.OutputFormat}");
            text.AppendLine(Culture, $"  headset microphone format: {sound.InputFormat}");
            if (sound.Devices.Count == 0) text.AppendLine("  none of the headset's devices are active in Windows");
            foreach (var device in sound.Devices) text.AppendLine(Culture, $"  {Level(device)}");
        }

        if (snapshot.Unread.Count > 0)
        {
            text.AppendLine("Could not read:");
            foreach (string line in snapshot.Unread) text.AppendLine(Culture, $"  {line}");
        }
    }

    private const string Unnamed = "not yet identified";

    private static string Value(string key, JsonElement value) =>
        $"0x{key,-5} {NameOf(key) ?? Unnamed,-26} {DeviceEvent.Render(value)}";

    private static string? NameOf(string key) =>
        int.TryParse(key, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int number)
        && Registry.ByKey.TryGetValue(number, out var setting) ? setting.Name : null;

    /// <summary>Numeric order for hex keys; anything else after them.</summary>
    private static int Order(string key) =>
        int.TryParse(key, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int number) ? number : int.MaxValue;

    private static string Default(SoundDefault role) =>
        $"{(role.Output ? "output" : "input"),-6} {role.Role,-14} "
        + (role.Device.Length == 0 ? "(none)" : $"{role.Device}  [{(role.Product.Length > 0 ? Named(role.Product) : "not the headset")}]");

    private static string Level(SoundDevice device)
    {
        string line = Invariant(
            $"{(device.Output ? "output" : "input")} {device.Name} [{Named(device.Product)}]  {device.Percent}%{(device.Muted ? " muted" : "")}  peak {device.Peak:0.000}");
        if (device.Apps.Count == 0) return line;
        return line + "  apps: " + string.Join(", ", device.Apps.Select(a =>
            Invariant($"{a.Name} {a.Percent}%{(a.Muted ? " muted" : "")}{(a.Playing ? " playing" : "")}")));
    }

    private static string Named(string product) =>
        Transmitters.Hardware.TryGetValue(product, out var name) ? $"{name} {product}" : product;

    private static string Duration(TimeSpan length) =>
        length.TotalMinutes >= 1
            ? Invariant($"{(int)length.TotalMinutes} min {length.Seconds} s")
            : Invariant($"{length.Seconds} s");

    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private static string Invariant(FormattableString text) => FormattableString.Invariant(text);
}
