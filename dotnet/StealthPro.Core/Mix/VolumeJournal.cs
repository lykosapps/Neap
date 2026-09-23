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
/// Three rules, each of which was learned rather than designed:
///
/// <b>Write before changing, not after.</b> A kill between the two costs
/// nothing. A kill the other way round loses the originals.
///
/// <b>Restore only what is still where we left it.</b> Each entry records
/// both the original and the value we applied. If a session is no longer
/// sitting at what we applied, the person has moved it since and their
/// choice wins. The old journal stored a single scale for everything, which
/// cannot express a crossfade with two sides.
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
    private sealed class Entry
    {
        [JsonPropertyName("original")] public float Original { get; set; }
        [JsonPropertyName("applied")] public float Applied { get; set; }
    }

    private sealed class Record
    {
        [JsonPropertyName("endpoint")] public string Endpoint { get; set; } = "";
        [JsonPropertyName("pid")] public int Pid { get; set; }
        [JsonPropertyName("sessions")] public Dictionary<string, Entry> Sessions { get; set; } = new();
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

    internal int Count { get { lock (_gate) return _record.Sessions.Count; } }

    internal bool Owns(string endpointId)
    {
        lock (_gate) return _record.Sessions.Count > 0 && _record.Endpoint == endpointId;
    }

    /// <summary>
    /// The original volume for a session, recording it the first time it is
    /// seen. Recording it again on a later pass is what ratchets: the value
    /// read then is one we set ourselves.
    /// </summary>
    internal float Remember(string sessionId, float current)
    {
        lock (_gate)
        {
            if (_record.Sessions.TryGetValue(sessionId, out var existing)) return existing.Original;
            _record.Sessions[sessionId] = new Entry { Original = current, Applied = current };
            return current;
        }
    }

    /// <summary>Write down what is about to be applied, before applying it.</summary>
    internal void Commit(string endpointId, IEnumerable<(string Id, float Wanted)> pending)
    {
        lock (_gate)
        {
            _record.Endpoint = endpointId;
            _record.Pid = Environment.ProcessId;
            foreach (var (id, wanted) in pending)
                if (_record.Sessions.TryGetValue(id, out var entry)) entry.Applied = wanted;
            Save();
        }
    }

    /// <summary>
    /// Put sessions back, keeping anything we could not reach. Only a session
    /// still sitting at the value we applied is restored — if it has moved
    /// since, that was the person and their choice stands.
    /// </summary>
    internal void RestoreInto(IEnumerable<ISessionVolume> sessions)
    {
        lock (_gate)
        {
            if (_record.Sessions.Count == 0) return;
            var done = new List<string>();
            foreach (var session in sessions)
            {
                string? id = session.Id;
                if (string.IsNullOrEmpty(id)) continue;
                if (!_record.Sessions.TryGetValue(id, out var entry)) continue;
                try
                {
                    if (MathF.Abs(session.Volume - entry.Applied) <= 0.01f)
                        session.Volume = entry.Original;
                    // Drop it only once it is actually back, so a failure part
                    // way through leaves the rest for the next launch.
                    done.Add(id);
                }
                catch { }
            }
            foreach (string id in done) _record.Sessions.Remove(id);
            Save();
        }
    }

    private static Record Load(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<Record>(File.ReadAllText(path)) ?? new Record();
        }
        catch { return new Record(); }
    }

    private void Save()
    {
        try
        {
            if (_record.Sessions.Count == 0) { File.Delete(_path); return; }
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_record));
        }
        catch { /* a journal we cannot write is bad; throwing here is worse */ }
    }
}
