using System.Globalization;
using Neap.Core.Settings;

namespace Neap.Services;

/// <summary>
/// Noise control on the headset: noise cancellation, transparency and off.
/// </summary>
/// <remarks>
/// <para>
/// The rules are <see cref="NoiseControl"/>'s. This keeps it fed from the
/// headset, sends the writes it asks for, and remembers the two things the
/// headset cannot: the level noise cancellation comes back at, and whether
/// the Mode button cycles through all three modes.
/// </para>
/// <para>
/// Every change the app makes to noise control goes through here, so that a
/// change nobody here made can be taken for a press of the Mode button.
/// Runs on the UI thread, with the headset service's events.
/// </para>
/// </remarks>
public sealed class NoiseService : IDisposable
{
    private readonly HeadsetService _headset;
    private readonly NoiseControl _noise;

    public NoiseService(HeadsetService headset)
    {
        _headset = headset;
        var saved = AppSettings.Current;
        _noise = new NoiseControl(saved.NoiseBlocking) { Cycling = saved.ModeCyclesNoise };
        _headset.Changed += OnChanged;
    }

    /// <summary>The mode, the remembered level or the cycling changed.</summary>
    public event Action? Changed;

    /// <summary>Gets the mode the headset is in, or null until it has reported it.</summary>
    public NoiseMode? Mode => _noise.Mode;

    /// <summary>Gets whether a press of the Mode button steps through all three modes.</summary>
    public bool Cycling => _noise.Cycling;

    /// <summary>Gets whether the mode is known and both of its settings can be written.</summary>
    public bool CanChoose => Mode is not null
        && Registry.ByKey[NoiseControl.AncKey].Writable
        && Registry.ByKey[NoiseControl.LevelKey].Writable;

    /// <summary>Put the headset in a mode.</summary>
    public void Choose(NoiseMode mode)
    {
        Send(_noise.Choose(mode));
        Changed?.Invoke();
    }

    /// <summary>Put the headset in a mode, at this level when the mode is cancelling.</summary>
    /// <remarks>A profile is the caller: elsewhere the level comes only from the headset itself.</remarks>
    public void Choose(NoiseMode mode, int level)
    {
        if (mode == NoiseMode.Cancelling) _noise.SetBlocking(level);
        Choose(mode);
    }

    /// <summary>Set whether a press of the Mode button steps through all three modes.</summary>
    public void SetCycling(bool cycling)
    {
        _noise.Cycling = cycling;
        AppSettings.Update(s => s.ModeCyclesNoise = cycling);
        Changed?.Invoke();
    }

    private void OnChanged()
    {
        var before = _noise.Mode;
        var writes = _noise.Observe(Number(NoiseControl.AncKey), Number(NoiseControl.LevelKey));
        if (writes.Count > 0)
        {
            AppLog.Write(string.Create(CultureInfo.InvariantCulture,
                $"noise control: the Mode button left {before}, so it goes on to {NoiseControl.Next(before!.Value)}"));
            Send(writes);
        }

        // Kept once noise cancellation is left, not while its slider moves.
        if (_noise.Mode is not NoiseMode.Cancelling && AppSettings.Current.NoiseBlocking != _noise.Blocking)
            AppSettings.Update(s => s.NoiseBlocking = _noise.Blocking);
        Changed?.Invoke();
    }

    private int? Number(int key) => _headset.TryGetNumberByKey(key, out int value) ? value : null;

    private void Send(IReadOnlyList<SettingWrite> writes)
    {
        foreach (var write in writes) _headset.SetKey(write.Key, write.Value, hold: false);
    }

    public void Dispose() => _headset.Changed -= OnChanged;
}
