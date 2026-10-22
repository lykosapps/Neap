using Neap.Core.Audio;
using Neap.Core.Settings;

namespace Neap.Services;

/// <summary>
/// Mutes and unmutes the system's microphone when the headset mutes itself,
/// with the boom arm or, with the boom microphone off, its mute button.
/// </summary>
/// <remarks>
/// <para>
/// What the headset does is decided by <see cref="Microphone.SystemMuteFor"/>.
/// This watches for it and carries it out, whichever screen is showing or
/// none, so a call in another application is silent exactly when the headset
/// is.
/// </para>
/// <para>
/// The person's own mute from a tile is the system's alone and the headset
/// never hears of it, so it is only undone when the headset's own mute
/// moves.
/// </para>
/// </remarks>
public sealed class MicFollowsHeadset : IDisposable
{
    private readonly HeadsetService _headset;
    private int? _last;

    public MicFollowsHeadset(HeadsetService headset)
    {
        _headset = headset;
        _headset.Changed += OnChanged;
    }

    private async void OnChanged()
    {
        if (_headset.Microphone.Mute is not { } now) return;
        var before = _last;
        _last = now;

        if (Microphone.SystemMuteFor(before, now, AppSettings.Current.MicMutedByHeadset) is not { } mute) return;
        AppLog.Write($"microphone: the headset's own mute is {(mute ? "on" : "off")}, so the system's is set to match ({_headset.Microphone})");
        await SoundVolume.SetMuted(mute, Flow.Input);
        AppSettings.Update(settings => settings.MicMutedByHeadset = mute);
    }

    /// <summary>Mutes or unmutes the system's microphone for the person, which a lowered arm then leaves alone.</summary>
    /// <remarks>
    /// On a headset whose monitoring would otherwise go on through the mute,
    /// monitoring is turned down with it and put back after; see
    /// <see cref="MonitoringWhileMuted"/>.
    /// </remarks>
    public static async Task SetByPerson(bool muted)
    {
        await SoundVolume.SetMuted(muted, Flow.Input);
        AppSettings.Update(settings => settings.MicMutedByHeadset = false);
        QuietMonitoring(AppServices.Headset, muted);
    }

    private static void QuietMonitoring(HeadsetService headset, bool muted)
    {
        if (headset.Model is not { QuietsMonitoringWhileMuted: true }) return;
        int key = Registry.ByName["mic_monitoring"].Key;
        int? now = headset.TryGetNumberByKey(key, out int level) ? level : null;
        int? before = AppSettings.Current.MonitoringBeforeMute;
        if (MonitoringWhileMuted.LevelFor(muted, now, before) is { } set)
        {
            AppLog.Write($"microphone: mic monitoring {(muted ? $"down from {now} while muted" : $"back to {set}")}, as the headset's own mute does");
            headset.SetKey(key, set, hold: false);
        }
        if (muted) AppSettings.Update(settings => settings.MonitoringBeforeMute = MonitoringWhileMuted.KeptFor(now) ?? settings.MonitoringBeforeMute);
        else AppSettings.Update(settings => settings.MonitoringBeforeMute = null);
    }

    public void Dispose() => _headset.Changed -= OnChanged;
}
