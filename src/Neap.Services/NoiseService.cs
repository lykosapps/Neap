using System.Globalization;
using Neap.Core.Connection;
using Neap.Core.Firmware;
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
/// Whether the headset's own firmware runs that cycle is read from its flash
/// each time it connects, since an update or an edit while it was off changes
/// it. What the flash last said is kept, for the moments it cannot be read.
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
    private readonly FirmwareEditService _firmware;
    private readonly NoiseControl _noise;
    private bool _connected;

    public NoiseService(HeadsetService headset, FirmwareEditService firmware)
    {
        _headset = headset;
        _firmware = firmware;
        var saved = AppSettings.Current;
        _noise = new NoiseControl(saved.NoiseBlocking)
        {
            Cycling = saved.ModeCyclesNoise,
            HeadsetCycles = saved.HeadsetCyclesNoise,
        };
        _headset.Changed += OnChanged;
        _headset.StatusChanged += OnStatus;
        _firmware.Changed += OnFirmware;
    }

    /// <summary>The mode, the remembered level or the cycling changed.</summary>
    public event Action? Changed;

    /// <summary>Gets the mode the headset is in, or null until it has reported it.</summary>
    public NoiseMode? Mode => _noise.Mode;

    /// <summary>Gets whether a press of the Mode button steps through all three modes.</summary>
    public bool Cycling => _noise.Cycling;

    /// <summary>Gets whether the headset's own firmware steps the Mode button through all three modes.</summary>
    public bool HeadsetCycles => _noise.HeadsetCycles;

    /// <summary>Gets the way noise control was last on, which a press of the mini panel's tile turns it back on to.</summary>
    /// <remarks>Noise cancellation until the headset has been seen in a mode other than off. Kept for as long as Neap runs.</remarks>
    public NoiseMode LastOn { get; private set; } = NoiseMode.Cancelling;

    /// <summary>Gets whether the mode is known and both of its settings can be written, on a headset whose settings Neap changes.</summary>
    public bool CanChoose => Mode is not null && !_headset.ReadOnly
        && Registry.ByKey[NoiseControl.AncKey].Writable
        && Registry.ByKey[NoiseControl.LevelKey].Writable;

    /// <summary>Put the headset in a mode.</summary>
    public void Choose(NoiseMode mode)
    {
        Send(_noise.Choose(mode), hold: true);
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
    /// <remarks>Left off while the headset's own firmware runs the cycle, as a profile saved on stock firmware can ask for it.</remarks>
    public void SetCycling(bool cycling)
    {
        _noise.Cycling = cycling;
        if (cycling && !_noise.Cycling)
            AppLog.Write("noise control: Neap's cycle stays off, as the headset's firmware runs the Mode button's cycle");
        AppSettings.Update(s => s.ModeCyclesNoise = _noise.Cycling);
        Changed?.Invoke();
    }

    /// <remarks>
    /// Read on every system, though edits are written only where
    /// <see cref="FirmwareEdit.Released"/> says: a headset edited on one PC
    /// runs its cycle on every other.
    /// </remarks>
    private async void OnStatus(HeadsetStatus status)
    {
        bool connected = status.Link == Link.Connected;
        bool justConnected = connected && !_connected;
        _connected = connected;
        bool hasEdits = _headset.Model is not { } model || FirmwareEdit.For(model).Count > 0;
        if (justConnected && hasEdits && !_firmware.Busy) await _firmware.ReadFlash();
    }

    private void OnFirmware()
    {
        if (FollowFirmware()) Changed?.Invoke();
    }

    /// <summary>Takes whether the headset runs the cycle from its flash as last read, and says whether that changed.</summary>
    private bool FollowFirmware()
    {
        if (_headset.Model is not { } model) return false;
        // An edit made for another headset is not on this one, whatever its firmware is numbered.
        var edit = FirmwareEdit.For(model).Contains(FirmwareEdit.NoiseCycle)
            ? _firmware.StatusOf(FirmwareEdit.NoiseCycle)
            : EditStatus.DoesNotFit;
        bool cycles = NoiseControl.HeadsetCyclesFor(edit, _noise.HeadsetCycles);
        if (cycles == _noise.HeadsetCycles) return false;
        bool cycled = _noise.Cycling;
        _noise.HeadsetCycles = cycles;
        AppSettings.Update(s => (s.HeadsetCyclesNoise, s.ModeCyclesNoise) = (cycles, _noise.Cycling));
        AppLog.Write(cycles
            ? "noise control: the headset's edited firmware runs the Mode button's cycle" + (cycled ? ", so Neap's own is turned off" : "")
            : "noise control: the headset's firmware no longer runs the Mode button's cycle, so the button turns noise cancellation on and off");
        return true;
    }

    private void OnChanged()
    {
        // The flash can be read before the headset has reported the firmware version it is judged against.
        FollowFirmware();
        var before = _noise.Mode;
        int? anc = Number(NoiseControl.AncKey), level = Number(NoiseControl.LevelKey);
        var writes = _noise.Observe(anc, level);
        // A mode Neap chose is already known here, so this is the headset's own doing: its Mode button, or another app.
        if (NoiseControl.Of(anc, level) is { } reported && reported != before)
            AppLog.Write(string.Create(CultureInfo.InvariantCulture,
                $"noise control: the headset reports {reported} (0x750 {anc}, 0x760 {level}), after {before?.ToString() ?? "no reading"}"));
        if (writes.Count > 0)
        {
            AppLog.Write(string.Create(CultureInfo.InvariantCulture,
                $"noise control: the Mode button left {before}, so it goes on to {NoiseControl.Next(before!.Value)}"));
            Send(writes, hold: false);
        }

        if (_noise.Mode is { } mode) LastOn = QuickToggle.LastOn(mode, NoiseMode.Off, LastOn);

        // Kept once noise cancellation is left, not while its slider moves.
        if (_noise.Mode is not NoiseMode.Cancelling && AppSettings.Current.NoiseBlocking != _noise.Blocking)
            AppSettings.Update(s => s.NoiseBlocking = _noise.Blocking);
        Changed?.Invoke();
    }

    private int? Number(int key) => _headset.TryGetNumberByKey(key, out int value) ? value : null;

    /// <remarks>
    /// A choice made here is held for a moment, so the headset's late report of
    /// the mode it was in is not taken for a press of the Mode button. The step
    /// that finishes a real press is not held: the next press must be heard.
    /// </remarks>
    private void Send(IReadOnlyList<SettingWrite> writes, bool hold)
    {
        foreach (var write in writes) _headset.SetKey(write.Key, write.Value, hold);
    }

    public void Dispose()
    {
        _headset.Changed -= OnChanged;
        _headset.StatusChanged -= OnStatus;
        _firmware.Changed -= OnFirmware;
    }
}
