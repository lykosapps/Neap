using System.Globalization;
using StealthPro.Core;
using StealthPro.Core.Presets;

namespace StealthPro.App.Services;

/// <summary>One equaliser bank as the UI needs it.</summary>
public sealed class BankState
{
    public required Bank Bank { get; init; }
    public required BankSpec Spec { get; init; }
    public required IReadOnlyList<Preset> Presets { get; init; }

    /// <summary>Which preset the live curve is a variation of. See <see cref="PresetService"/>.</summary>
    public Preset? Baseline { get; set; }

    public IEnumerable<Preset> Custom => Presets.Where(p => p.Custom);
    public IReadOnlyList<int> FreeSlots => PresetStore.FreeSlots(Presets);
}

/// <summary>
/// The equaliser: the headset's presets, and the curve in front of them.
/// </summary>
/// <remarks>
/// <para>
/// The headset forgets which preset you were on the moment you touch a band.
/// Its selected-preset value goes to 0 on the first band write (measured).
/// That is how we know the curve has been edited, but it also destroys the one
/// thing needed to offer a way back, so the baseline is remembered here and
/// held until another preset is chosen. Without it, a changed curve is
/// indistinguishable from the preset it came from, and there is nothing to
/// revert a single band to.
/// </para>
/// <para>
/// Saving always creates a new preset. The slot id in a save is only a hint:
/// ask for an occupied slot and the headset makes a second preset rather than
/// overwriting. So "save over this one" is a delete followed by a save, two
/// operations with a gap in the middle and the irreversible one first.
/// Everything that could refuse the save is checked before the delete.
/// </para>
/// <para>
/// Band values are tenths of a decibel, -90 to +90, matching the +9 dB..-9 dB
/// scale printed in Swarm's own resources.
/// </para>
/// </remarks>
public sealed class PresetService
{
    public const int BandFloor = -90;
    public const int BandCeiling = 90;

    private readonly HeadsetService _headset;
    private readonly Dictionary<Bank, BankState> _banks = new();
    private readonly Dictionary<Bank, Task<BankState>> _reading = new();

    /// <remarks>
    /// Follows the headset before any screen paints from it: the service
    /// subscribes first, so every screen sees the preset chosen on the
    /// headset, not only the one that happens to follow it.
    /// </remarks>
    public PresetService(HeadsetService headset)
    {
        _headset = headset;
        _headset.Changed += () =>
        {
            foreach (var state in _banks.Values) Follow(state);
        };
    }

    public BankState? State(Bank bank) => _banks.GetValueOrDefault(bank);

    /// <summary>
    /// Read a bank's presets off the headset. Ten reads, one per custom
    /// slot, so this is a deliberate action rather than something to do on
    /// every repaint.
    /// </summary>
    /// <remarks>
    /// A read already under way is shared rather than started again: Home and
    /// a page can both ask as they load, and each read is ten round trips.
    /// </remarks>
    public Task<BankState> Load(Bank bank)
    {
        if (!_reading.TryGetValue(bank, out var reading))
        {
            reading = Read(bank);
            if (!reading.IsCompleted) _reading[bank] = reading;
        }
        return reading;
    }

    private async Task<BankState> Read(Bank bank)
    {
        try
        {
            var presets = await _headset.Post(client => PresetStore.ReadBank(client, bank));
            var state = new BankState
            {
                Bank = bank,
                Spec = PresetStore.Spec(bank),
                Presets = presets,
                Baseline = _banks.GetValueOrDefault(bank)?.Baseline,
            };
            _banks[bank] = state;
            AdoptBaseline(state);
            return state;
        }
        finally { _reading.Remove(bank); }
    }

    /// <summary>Work out which preset the live curve came from, and keep it.</summary>
    /// <remarks>
    /// Three ways in, in order of confidence: the headset still names a
    /// selected preset; we already knew and are mid-edit, so keep what we
    /// have; or the live curve matches some preset exactly, which is the
    /// only evidence available after a restart.
    /// </remarks>
    private void AdoptBaseline(BankState state)
    {
        if (Follow(state)) return;
        if (state.Baseline is not null) return;

        var live = LiveBands(state);
        if (live is null) return;
        state.Baseline = state.Presets.FirstOrDefault(
            p => p.Bands.Count == live.Length && p.Bands.SequenceEqual(live));
    }

    /// <summary>
    /// Takes the preset the headset says it is on as the baseline, for a
    /// preset chosen on the headset itself.
    /// </summary>
    /// <remarks>
    /// The mode button can step through presets. Without this the baseline
    /// stays on the one chosen in the app, and the new curve reads as an
    /// edit of it.
    /// </remarks>
    private bool Follow(BankState state)
    {
        if (!_headset.TryGetNumberByKey(state.Spec.Select, out int selected)
            || state.Presets.FirstOrDefault(p => p.Id == selected) is not { } chosen) return false;
        state.Baseline = chosen;
        return true;
    }

    /// <summary>The curve the headset is on right now, or null if not read yet.</summary>
    public int[]? LiveBands(Bank bank) =>
        _banks.TryGetValue(bank, out var state) ? LiveBands(state) : null;

    private int[]? LiveBands(BankState state)
    {
        var bands = new int[state.Spec.Bands.Count];
        for (int i = 0; i < bands.Length; i++)
            if (!_headset.TryGetNumberByKey(state.Spec.Bands[i], out bands[i])) return null;
        return bands;
    }

    /// <summary>Where a band sits in its baseline, for the revert affordance.</summary>
    public int? StoredBand(Bank bank, int index)
    {
        var baseline = _banks.GetValueOrDefault(bank)?.Baseline;
        return baseline is not null && index < baseline.Bands.Count
            ? baseline.Bands[index] : null;
    }

    public bool IsEdited(Bank bank)
    {
        if (!_banks.TryGetValue(bank, out var state) || state.Baseline is null) return false;
        var live = LiveBands(state);
        if (live is null) return false;
        return !state.Baseline.Bands.SequenceEqual(live);
    }

    /// <summary>The name to show beside the equaliser, including mid-edit.</summary>
    public string CurrentName(Bank bank)
    {
        if (!_banks.TryGetValue(bank, out var state)) return "";
        // The headset stops reporting a name the moment a band is touched,
        // which would empty the label exactly when it is most wanted.
        string nameKey = state.Spec.NameKey.ToString("x", CultureInfo.InvariantCulture);
        string? reported = _headset.Values.TryGetValue(nameKey, out var raw)
            ? raw.ToString() : null;
        return string.IsNullOrWhiteSpace(reported) ? state.Baseline?.Name ?? "" : reported;
    }

    public void SetBand(Bank bank, int index, int tenths)
    {
        if (!_banks.TryGetValue(bank, out var state)) return;
        if (index < 0 || index >= state.Spec.Bands.Count) return;
        _headset.SetKey(state.Spec.Bands[index], Math.Clamp(tenths, BandFloor, BandCeiling));
    }

    /// <summary>Select a preset.</summary>
    /// <remarks>
    /// The headset reloads all ten bands itself, so this is one write rather
    /// than ten, and the local store is moved at once rather than waiting on a
    /// full read. A full read costs about 1.2 seconds, and doing one after
    /// every action makes switching presets feel broken.
    /// </remarks>
    public async Task Select(Bank bank, Preset preset)
    {
        if (!_banks.TryGetValue(bank, out var state)) return;
        state.Baseline = preset;
        _headset.SetKey(state.Spec.Select, preset.Id);
        for (int i = 0; i < preset.Bands.Count && i < state.Spec.Bands.Count; i++)
            _headset.SetKeyLocally(state.Spec.Bands[i], preset.Bands[i]);
        await Task.CompletedTask;
    }

    /// <summary>Throw away unsaved edits and go back to the stored curve.</summary>
    public Task Discard(Bank bank)
    {
        var baseline = _banks.GetValueOrDefault(bank)?.Baseline;
        return baseline is null ? Task.CompletedTask : Select(bank, baseline);
    }

    /// <summary>Put one band back where its baseline has it.</summary>
    public void RevertBand(Bank bank, int index)
    {
        if (StoredBand(bank, index) is int stored) SetBand(bank, index, stored);
    }

    /// <summary>
    /// Save the live curve into a custom slot, optionally over one that is
    /// already there.
    /// </summary>
    /// <remarks>
    /// Everything that could refuse is checked first, because replacing
    /// deletes before it writes.
    /// </remarks>
    /// <returns>Null on success, or what went wrong.</returns>
    public async Task<string?> Save(Bank bank, string name, string? replacing)
    {
        if (!_banks.TryGetValue(bank, out var state)) return Strings.Get("Preset_NotRead");
        name = name.Trim();
        if (name.Length == 0) return Strings.Get("Preset_NeedsName");
        // Checked here and not only in the store: replacing deletes the old
        // preset first, and a name that cannot be written would throw after
        // that, losing the preset it was meant to replace.
        if (!PresetStore.NameFits(name, bank))
            return Strings.Format("Preset_TooLong", PresetStore.MaxNameLength);

        var free = state.FreeSlots;
        if (replacing is null && free.Count == 0)
            return Strings.Get("Preset_Full");

        var over = replacing is null
            ? null
            : state.Custom.FirstOrDefault(p => p.Name == replacing);
        if (replacing is not null && over is null)
            return Strings.Format("Preset_Missing", replacing);

        var bands = LiveBands(state);
        if (bands is null) return Strings.Get("Preset_Unreadable");

        try
        {
            await _headset.Post(client =>
            {
                int? slot = free.Count > 0 ? free[0] : null;
                if (over is not null)
                {
                    PresetStore.Delete(client, over.Name, bank);
                    Thread.Sleep(400);
                    // The slot just freed is the lowest one available, so
                    // the replacement lands where the original was.
                    slot = free.Append(over.Id).Min();
                }
                PresetStore.Save(client, name, bands, bank, slot);
                return true;
            });
        }
        catch (Exception ex) { return ex.Message; }

        await Load(bank);
        _banks[bank].Baseline = _banks[bank].Custom.FirstOrDefault(p => p.Name == name);
        return null;
    }

    public async Task<string?> Delete(Bank bank, string name)
    {
        try { await _headset.Post(client => { PresetStore.Delete(client, name, bank); return true; }); }
        catch (Exception ex) { return ex.Message; }
        await Load(bank);
        return null;
    }
}
