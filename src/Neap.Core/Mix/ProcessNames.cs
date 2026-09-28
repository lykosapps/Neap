namespace Neap.Core.Mix;

/// <summary>
/// The names of the programs behind audio sessions, looked up once per
/// program rather than on every pass of the mix.
/// </summary>
/// <remarks>
/// <para>
/// The mix passes over every session every two seconds while it runs.
/// Asking Windows each time opened every program playing, a game included,
/// thirty times a minute for as long as it played; once is all a name needs.
/// </para>
/// <para>
/// A program's id can be given to a new program once the old one has gone,
/// so a name not asked for in <see cref="Forget"/>'s time is looked up afresh.
/// </para>
/// </remarks>
/// <param name="lookUp">Asks Windows for a program's name; empty when it cannot say.</param>
/// <param name="now">The time, for forgetting programs no longer seen.</param>
/// <param name="lookedUp">Told each name actually looked up, so the log says when Neap reached another program.</param>
public sealed class ProcessNames(Func<uint, string> lookUp, Func<TimeSpan> now, Action<string>? lookedUp = null)
{
    /// <summary>How long a program can go unasked before its name is looked up again.</summary>
    public static readonly TimeSpan Forget = TimeSpan.FromSeconds(10);

    private readonly Dictionary<uint, (string Name, TimeSpan Seen)> _known = new();
    private readonly object _gate = new();

    /// <summary>The name of the program with this id, or empty when there is none or Windows cannot say.</summary>
    public string Of(uint pid)
    {
        if (pid == 0) return "";
        var at = now();
        lock (_gate)
        {
            foreach (uint gone in _known.Where(k => at - k.Value.Seen > Forget).Select(k => k.Key).ToList())
                _known.Remove(gone);
            if (_known.TryGetValue(pid, out var known))
            {
                _known[pid] = (known.Name, at);
                return known.Name;
            }
        }

        string name = lookUp(pid);
        if (name.Length == 0) return "";
        lookedUp?.Invoke(name);
        lock (_gate) _known[pid] = (name, at);
        return name;
    }
}
