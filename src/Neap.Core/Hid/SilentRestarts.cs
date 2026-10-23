namespace Neap.Core.Hid;

/// <summary>Which silent devices to restart: each once while it stays plugged in.</summary>
/// <remarks>
/// <para>
/// A transmitter that opens and gives only empty reports is either locked up,
/// because nothing read it from the moment it was plugged in, or simply has
/// no headset to speak for, because the headset is off. The two look the same
/// from the PC, so one restart is tried and no more: a locked-up transmitter
/// is put right by it, and an idle one loses nothing but a moment of a
/// connection it was not using.
/// </para>
/// <para>
/// A device that goes away and comes back is a new plug-in, and can be
/// restarted again.
/// </para>
/// </remarks>
public sealed class SilentRestarts
{
    private readonly HashSet<string> _restarted = new(StringComparer.Ordinal);

    /// <summary>The silent devices to restart now, each recorded as restarted.</summary>
    /// <param name="silent">The paths of the devices that opened and gave only empty reports.</param>
    /// <param name="present">The paths of every device plugged in now.</param>
    public IReadOnlyList<string> Due(IEnumerable<string> silent, IEnumerable<string> present)
    {
        _restarted.IntersectWith(present);
        return silent.Where(_restarted.Add).ToList();
    }

    /// <summary>Whether a device has been restarted since it was plugged in.</summary>
    public bool Restarted(string path) => _restarted.Contains(path);

    /// <summary>Makes a device due again, after a restart that could not be tried at all, such as one the system refused.</summary>
    public void Retry(string path) => _restarted.Remove(path);

    /// <summary>Whether a silent transmitter's answer to <see cref="LinkInquiry"/> calls for a restart now.</summary>
    /// <param name="linked">Its answer, or null when it gave none.</param>
    /// <param name="ruleOutOfDate">Whether the access rule installed is older than this version's; see <see cref="UdevRule"/>.</param>
    /// <remarks>
    /// <para>
    /// One that answers that no headset is linked has a working channel and
    /// simply has no headset to speak for: the headset is off or out of
    /// range, and a restart would only switch it off once it comes back. One
    /// that gives no answer, or says the headset is linked and still answers
    /// nothing else, has a channel that has stopped, and is restarted.
    /// </para>
    /// <para>
    /// A stopped channel under an older rule waits for the new one. The older
    /// rule lets the system put the transmitter back to sleep, so a restart
    /// under it is used up for nothing: on one tester's CachyOS it brought
    /// back no reports at all.
    /// </para>
    /// </remarks>
    public static bool Needed(bool? linked, bool ruleOutOfDate) => linked switch
    {
        false => false,
        null => !ruleOutOfDate,
        true => true,
    };

    /// <summary>Whether a silent transmitter's answer to <see cref="LinkInquiry"/> shows its control channel has stopped.</summary>
    /// <param name="linked">Its answer, or null when it gave none.</param>
    /// <remarks>
    /// The transmitter answers the question itself, so no answer at all is a
    /// channel that has stopped, which keeping it awake (see
    /// <see cref="UdevRule"/>) can prevent. One that answers either way has a
    /// working channel, and an older rule is no reason to ask the person for
    /// their password.
    /// </remarks>
    public static bool ChannelStopped(bool? linked) => linked is null;
}
