using System.Text.Json;
using System.Text.Json.Serialization;

namespace Neap.Core.Mix;

/// <summary>One application's volume on one device, as the journal sees it.</summary>
internal interface ISessionVolume
{
    string? Id { get; }
    float Volume { get; set; }
}

/// <summary>
/// What other applications' volumes were before the mix changed them,
/// persisted so a later launch can put them back.
/// </summary>
/// <remarks>
/// <para>
/// The crossfade works by holding other applications down on the headset, and
/// this process is the only thing that knows what they were. However it is
/// killed (Task Manager, a crash, a power cut), the next launch has to be able
/// to put them back, or somebody is left with a permanently quiet Spotify and
/// no record of why. Five rules follow from that.
/// </para>
/// <para>
/// Write before changing, not after. A kill between the two costs nothing; a
/// kill the other way round loses the originals.
/// </para>
/// <para>
/// Restore only what is still where it was left. Each entry records both the
/// original and the value applied. If a session is no longer at the applied
/// value, the person has moved it since and their choice wins.
/// </para>
/// <para>
/// A level someone else sets while the mix runs is theirs too. A session found
/// away from the applied value is taken as the person's new level, and the mix
/// scales from that. A session whose share the mix has not changed is left
/// alone, so a sweep never undoes a change made in Windows' own mixer.
/// </para>
/// <para>
/// Keep one record per device. Windows keeps each application's volume
/// separately on each device, and the headset has several (the Charging Dock,
/// the USB Transmitter and its own USB-C cable), so a session is restored on
/// the device it was held down on, whichever device is in use now.
/// </para>
/// <para>
/// Never clear a journal this process did not write. A throwaway launch that
/// only lists devices must not wipe the record of a crashed run; if it does,
/// volumes ratchet 1.00, 0.50, 0.25, 0.12 across three crashes. Entries owed
/// to an application that is not running are kept for next time rather than
/// discarded.
/// </para>
/// </remarks>
internal sealed class VolumeJournal
{
    /// <summary>How far a volume may sit from the applied value and still count as ours.</summary>
    internal const float Tolerance = 0.01f;

    private sealed class Entry
    {
        [JsonPropertyName("original")] public float Original { get; set; }
        [JsonPropertyName("applied")] public float Applied { get; set; }

        /// <summary>
        /// The share of <see cref="Original"/> that <see cref="Applied"/> was
        /// set as.
        /// </summary>
        /// <remarks>
        /// Absent from older journals; <see cref="VolumeJournal.Load"/> derives
        /// it from the other two.
        /// </remarks>
        [JsonPropertyName("scale")] public float? Scale { get; set; }
    }

    private sealed class Record
    {
        /// <summary>Device id, then session id.</summary>
        [JsonPropertyName("devices")]
        public Dictionary<string, Dictionary<string, Entry>> Devices { get; set; } = new();

        // The older single-device format. Read, moved into Devices, and never
        // written again.
        [JsonPropertyName("endpoint"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Endpoint { get; set; }

        [JsonPropertyName("sessions"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, Entry>? Sessions { get; set; }
    }

    /// <summary>The one the app uses, in the user's local app data.</summary>
    internal static VolumeJournal Shared { get; } =
        new(System.IO.Path.Combine(AppFolder.Path, "session-mix-journal.json"));

    private readonly string _path;
    private readonly object _gate = new();
    private readonly Record _record;

    internal VolumeJournal(string path)
    {
        _path = path;
        _record = Load(path);
    }

    /// <summary>Every session held, on every device.</summary>
    internal int Count { get { lock (_gate) return _record.Devices.Values.Sum(d => d.Count); } }

    /// <summary>The devices that have something to put back.</summary>
    internal IReadOnlyList<string> Devices { get { lock (_gate) return _record.Devices.Keys.ToList(); } }

    internal bool Owns(string deviceId)
    {
        lock (_gate) return _record.Devices.TryGetValue(deviceId, out var held) && held.Count > 0;
    }

    /// <summary>
    /// What a session should be set to for the mix to be at
    /// <paramref name="scale"/> of its level, or null to leave it alone.
    /// </summary>
    /// <remarks>
    /// The original is recorded the first time a session is seen, at the level
    /// it was found at. Recording it again on a later pass ratchets, because
    /// the value read then is one the mix set. After that the entry follows
    /// whoever else moves the session, so the person's own changes stand.
    /// </remarks>
    internal float? Wanted(string deviceId, string sessionId, float current, float scale)
    {
        lock (_gate)
        {
            if (!_record.Devices.TryGetValue(deviceId, out var held))
                _record.Devices[deviceId] = held = new Dictionary<string, Entry>();

            if (!held.TryGetValue(sessionId, out var entry))
            {
                held[sessionId] = entry = new Entry { Original = current, Applied = current, Scale = 1f };
            }
            else if (MathF.Abs(current - entry.Applied) > Tolerance)
            {
                // Moved since we set it, by the person or by the application:
                // that is the level now, at the share we had it at.
                float was = entry.Scale ?? 1f;
                entry.Original = was > 0 ? MathF.Min(1f, current / was) : current;
                entry.Applied = current;
            }

            if (MathF.Abs((entry.Scale ?? 1f) - scale) < 0.0001f) return null;

            float wanted = Math.Clamp(entry.Original * scale, 0f, 1f);
            if (MathF.Abs(current - wanted) <= 0.001f)
            {
                entry.Applied = current;
                entry.Scale = scale;
                return null;
            }
            return wanted;
        }
    }

    /// <summary>Records what is about to be applied, before it is applied.</summary>
    internal void Commit(string deviceId, IEnumerable<(string Id, float Wanted, float Scale)> pending)
    {
        lock (_gate)
        {
            if (!_record.Devices.TryGetValue(deviceId, out var held)) return;
            foreach (var (id, wanted, scale) in pending)
                if (held.TryGetValue(id, out var entry))
                {
                    entry.Applied = wanted;
                    entry.Scale = scale;
                }
            Save();
        }
    }

    /// <summary>
    /// Puts one device's sessions back, keeping anything that could not be
    /// reached.
    /// </summary>
    /// <remarks>
    /// Only a session still at the applied value is restored. If it has moved
    /// since, that was the person, and their choice stands.
    /// </remarks>
    internal void RestoreInto(string deviceId, IEnumerable<ISessionVolume> sessions)
    {
        lock (_gate)
        {
            if (!_record.Devices.TryGetValue(deviceId, out var held) || held.Count == 0) return;
            var done = new List<string>();
            foreach (var session in sessions)
            {
                string? id = session.Id;
                if (string.IsNullOrEmpty(id)) continue;
                if (!held.TryGetValue(id, out var entry)) continue;
                try
                {
                    if (MathF.Abs(session.Volume - entry.Applied) <= Tolerance)
                        session.Volume = entry.Original;
                    // Drop it only once it is handled, restored or left to the
                    // person, so a failure part way through leaves the rest
                    // for the next launch.
                    done.Add(id);
                }
                catch { }
            }
            foreach (string id in done) held.Remove(id);
            if (held.Count == 0) _record.Devices.Remove(deviceId);
            Save();
        }
    }

    private static Record Load(string path)
    {
        Record record;
        try
        {
            record = JsonSerializer.Deserialize<Record>(File.ReadAllText(path)) ?? new Record();
        }
        catch { return new Record(); }

        // An older single-device journal. Windows starts every session
        // identifier with the id of the device it plays on, so each entry can
        // be filed under the device it belongs to, including entries the old
        // format recorded against the wrong endpoint.
        if (record.Sessions is { } old)
        {
            foreach (var (id, entry) in old)
            {
                int bar = id.IndexOf('|', StringComparison.Ordinal);
                string device = bar > 0 ? id[..bar] : record.Endpoint ?? "";
                if (device.Length == 0) continue;
                if (!record.Devices.TryGetValue(device, out var held))
                    record.Devices[device] = held = new Dictionary<string, Entry>();
                held[id] = entry;
            }
            record.Sessions = null;
            record.Endpoint = null;
        }

        foreach (var entry in record.Devices.Values.SelectMany(d => d.Values))
            entry.Scale ??= entry.Original > 0 ? Math.Clamp(entry.Applied / entry.Original, 0f, 1f) : 1f;
        return record;
    }

    private void Save()
    {
        try
        {
            if (_record.Devices.Count == 0) { File.Delete(_path); return; }
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_record));
        }
        catch { /* a journal we cannot write is bad; throwing here is worse */ }
    }
}
