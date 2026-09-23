using System.Text.Json;
using System.Text.Json.Serialization;

namespace StealthPro.Core.Mix;

/// <summary>One application's volume on one device, as the journal sees it.</summary>
internal interface ISessionVolume
{
    string? Id { get; }
    float Volume { get; set; }
}

/// <summary>
/// What other applications' volumes were before we touched them.
///
/// This is the most important thing in the mix. The crossfade works by
/// holding other applications down on the headset, and this process is the
/// only thing that knows what they were. Kill it any way you like — Task
/// Manager, a crash, a power cut — and the next launch has to be able to put
/// them back, or somebody is left with a permanently quiet Spotify and no
/// record of why.
///
/// Five rules, each of which was learned rather than designed:
///
/// <b>Write before changing, not after.</b> A kill between the two costs
/// nothing. A kill the other way round loses the originals.
///
/// <b>Restore only what is still where we left it.</b> Each entry records
/// both the original and the value we applied. If a session is no longer
/// sitting at what we applied, the person has moved it since and their
/// choice wins.
///
/// <b>A level someone else set is theirs while the mix runs, too.</b> A
/// session found away from what we applied is taken as the person's new
/// level, and the mix scales from that. A session the mix has not moved for
/// is left alone, so a sweep never undoes a change made in Windows' own
/// mixer.
///
/// <b>One record per device.</b> Windows keeps each application's volume
/// separately on each device, and the headset has several — the Charging
/// Dock, the USB Transmitter and its own cable — so a session is restored on
/// the device it was held down on, whichever device is in use now.
///
/// <b>Never clear a journal you do not own.</b> An early version cleared it
/// on exit whether or not it had written anything, so a throwaway launch
/// that only listed devices wiped the record of a crashed run — and the
/// volumes ratcheted 1.00, 0.50, 0.25, 0.12 across three crashes. Entries
/// owed to an application that is not running are kept for next time rather
/// than discarded.
/// </summary>
internal sealed class VolumeJournal
{
    /// <summary>How far a volume may sit from what we set and still be ours.</summary>
    internal const float Tolerance = 0.01f;

    private sealed class Entry
    {
        [JsonPropertyName("original")] public float Original { get; set; }
        [JsonPropertyName("applied")] public float Applied { get; set; }

        /// <summary>
        /// The share of <see cref="Original"/> that <see cref="Applied"/> was
        /// set as. Journals from before it existed are read without it.
        /// </summary>
        [JsonPropertyName("scale")] public float? Scale { get; set; }
    }

    private sealed class Record
    {
        /// <summary>Device id, then session id.</summary>
        [JsonPropertyName("devices")]
        public Dictionary<string, Dictionary<string, Entry>> Devices { get; set; } = new();

        // The format before one record per device. Read, moved into Devices,
        // and never written again.
        [JsonPropertyName("endpoint"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Endpoint { get; set; }

        [JsonPropertyName("sessions"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, Entry>? Sessions { get; set; }
    }

    /// <summary>The one the app uses, in the user's local app data.</summary>
    internal static VolumeJournal Shared { get; } = new(System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StealthProIIControl", "session-mix-journal.json"));

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
    ///
    /// Recorded the first time a session is seen, at the level it was found
    /// at. Recording it again on a later pass is what ratchets: the value read
    /// then is one we set ourselves. After that the entry follows whoever else
    /// moves the session, so the person's own changes stand.
    /// </summary>
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

    /// <summary>Write down what is about to be applied, before applying it.</summary>
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
    /// Put one device's sessions back, keeping anything we could not reach.
    /// Only a session still sitting at the value we applied is restored — if
    /// it has moved since, that was the person and their choice stands.
    /// </summary>
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
                    // Drop it only once it is actually back, so a failure part
                    // way through leaves the rest for the next launch.
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

        // A journal from before one record per device. Windows starts every
        // session identifier with the id of the device it plays on, so each
        // entry can be put back under the device it belongs to — including
        // the ones the old format had stranded.
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
