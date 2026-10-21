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
    private readonly SettingKey _arm = Registry.ByName[Microphone.ArmSetting];
    private readonly SettingKey _builtIn = Registry.ByName[Microphone.BuiltInSetting];
    private int? _last;

    public MicFollowsHeadset(HeadsetService headset)
    {
        _headset = headset;
        _headset.Changed += OnChanged;
    }

    private async void OnChanged()
    {
        if (Microphone.HeadsetMute(Read(_arm), Read(_builtIn)) is not { } now) return;
        var before = _last;
        _last = now;

        if (Microphone.SystemMuteFor(before, now, AppSettings.Current.MicMutedByHeadset) is not { } mute) return;
        AppLog.Write($"microphone: the headset's own mute is {(mute ? "on" : "off")}, so the system's is set to match");
        await SoundVolume.SetMuted(mute, Flow.Input);
        AppSettings.Update(settings => settings.MicMutedByHeadset = mute);
    }

    private int? Read(SettingKey key) => _headset.TryGetNumberByKey(key.Key, out int value) ? value : null;

    /// <summary>Mutes or unmutes the system's microphone for the person, which a lowered arm then leaves alone.</summary>
    public static async Task SetByPerson(bool muted)
    {
        await SoundVolume.SetMuted(muted, Flow.Input);
        AppSettings.Update(settings => settings.MicMutedByHeadset = false);
    }

    public void Dispose() => _headset.Changed -= OnChanged;
}
