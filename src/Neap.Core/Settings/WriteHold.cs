using System.Collections.Concurrent;

namespace Neap.Core.Settings;

/// <summary>The values the person has just set, each held as theirs for a moment.</summary>
/// <remarks>
/// A report of a value can be one the headset sent before the person's
/// change reached it. Taking it in would pull a slider back under their
/// finger, so a report for a held key is ignored until the hold ends. A full
/// read is authoritative and is always taken. Times are in any one unit, such
/// as Stopwatch ticks, as long as the length is in the same one.
/// </remarks>
/// <param name="length">How long a hold lasts.</param>
public sealed class WriteHold(long length)
{
    private readonly ConcurrentDictionary<string, long> _until = new();

    /// <summary>Whether nothing is held.</summary>
    public bool IsEmpty => _until.IsEmpty;

    /// <summary>Holds a key the person has just set, from <paramref name="now"/>.</summary>
    public void Hold(string key, long now) => _until[key] = now + length;

    /// <summary>Ends a key's hold.</summary>
    public void Release(string key) => _until.TryRemove(key, out _);

    /// <summary>Ends every hold.</summary>
    public void Clear() => _until.Clear();

    /// <summary>Whether a reported value for the key is taken in; one that is ends the key's hold.</summary>
    public bool Takes(string key, long now, bool authoritative)
    {
        if (!authoritative && _until.TryGetValue(key, out long until) && until > now) return false;
        Release(key);
        return true;
    }
}
