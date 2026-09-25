using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace StealthPro.Core;

/// <summary>
/// A short plain-text record: one line per event, in time order, the most
/// recent kept.
/// </summary>
/// <remarks>
/// <para>
/// A line goes where its time belongs rather than at the end, so an event
/// written when it finishes but stamped when it began still reads in order.
/// </para>
/// <para>
/// A log that cannot be written must not lose what it was told, or say
/// nothing about it: the one launch it misses is the one being asked about.
/// So a write is tried a few times, since another program reading the file
/// can hold it for a moment. A line that still cannot be written is kept and
/// written with the next one that can, together with a line saying the log
/// could not be written and why.
/// </para>
/// <para>
/// Copies of the app can run at the same time, one of them only long enough
/// to find the other running. Each rewrites the whole file, so they take a
/// lock named for the file first; without it, one copy writes back what it
/// read before the other's line arrived.
/// </para>
/// </remarks>
/// <param name="path">The file to write.</param>
/// <param name="keepLines">How many lines to keep, dropping the oldest.</param>
public sealed class LogFile(string path, int keepLines = 1000)
{
    /// <summary>How many times a write is tried before its line is kept for later.</summary>
    public const int Attempts = 5;

    /// <summary>Length of the time stamp that starts every line, "yyyy-MM-dd HH:mm:ss.f".</summary>
    private const int StampLength = 21;

    private static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan LockWait = TimeSpan.FromSeconds(1);

    private readonly object _gate = new();
    private readonly List<string> _waiting = [];
    private string? _failure;
    private readonly string _lockName = @"Local\Neap.Log." + Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant())))[..16];

    /// <summary>Record a line.</summary>
    /// <param name="what">The line, without its time stamp.</param>
    /// <param name="at">When it happened, which may be earlier than now.</param>
    /// <returns>Whether it reached the file, with anything that was waiting.</returns>
    public bool Write(string what, DateTime at)
    {
        lock (_gate)
        {
            if (_waiting.Count >= keepLines) _waiting.RemoveAt(0);
            _waiting.Add(Line(at, what));
            Exception? last = null;
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                if (attempt > 0) Thread.Sleep(Pause);
                try
                {
                    Flush(at);
                    return true;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    last = e;
                }
            }
            _failure ??= $"{last!.GetType().Name}: {last.Message}";
            return false;
        }
    }

    private static string Line(DateTime at, string what) =>
        string.Create(CultureInfo.InvariantCulture, $"{at:yyyy-MM-dd HH:mm:ss.f}  {what}");

    /// <summary>Writes the waiting lines into the file, under the cross-copy lock. Under the gate.</summary>
    /// <param name="now">When the line that finally gets through happened, which the note of any failure takes.</param>
    private void Flush(DateTime now)
    {
        using var copies = new Mutex(false, _lockName);
        try
        {
            if (!copies.WaitOne(LockWait)) throw new IOException("another copy of the app is holding the log");
        }
        catch (AbandonedMutexException) { /* a copy that died holding it; the lock is ours now */ }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : [];
            if (_failure is not null)
                Insert(lines, Line(now, string.Create(CultureInfo.InvariantCulture,
                    $"log: {_waiting.Count} line(s) from {_waiting[0][..StampLength]} on were written late; "
                    + $"the log could not be written ({_failure})")));
            foreach (string line in _waiting) Insert(lines, line);
            if (lines.Count > keepLines) lines.RemoveRange(0, lines.Count - keepLines);
            File.WriteAllLines(path, lines);
            _waiting.Clear();
            _failure = null;
        }
        finally { copies.ReleaseMutex(); }
    }

    /// <summary>Puts a line after every line stamped no later than it.</summary>
    private static void Insert(List<string> lines, string line)
    {
        int i = lines.Count;
        while (i > 0 && string.CompareOrdinal(lines[i - 1], 0, line, 0, StampLength) > 0) i--;
        lines.Insert(i, line);
    }
}
