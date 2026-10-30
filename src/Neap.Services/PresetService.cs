using System.Globalization;
using Neap.Core.Presets;

namespace Neap.Services;

/// <summary>One equaliser bank as the UI needs it.</summary>
public sealed class BankState
{
    public required Bank Bank { get; init; }
    public required BankSpec Spec { get; init; }
    public required IReadOnlyList<Preset> Presets { get; init; }

    /// <summary>Which preset the live curve is a variation of. See <see cref="PresetService"/>.</summary>
    public Preset? Baseline { get; set; }

    /// <summary>Whether every custom slot is accounted for; see <see cref="CustomsRead.Complete"/>.</summary>
    public required bool FullyRead { get; init; }

    public IEnumerable<Preset> Custom => Presets.Where(p => p.Custom);
    public IReadOnlyList<int> FreeSlots => PresetStore.FreeSlots(Presets, FullyRead);
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

    /// <summary>Banks that chose the flat preset as a starting point rather than as a preset, until another is chosen.</summary>
    private readonly HashSet<Bank> _flatStart = new();

    /// <summary>Banks on a new preset, not yet saved.</summary>
    private readonly HashSet<Bank> _new = new();

    /// <summary>How many times where the banks were left has been noted; only the latest note is written.</summary>
    private int _leaves;

    /// <summary>Each bank's Compare, where the curve taken off the headset is kept until it is put back.</summary>
    private readonly Dictionary<Bank, CompareHold> _compares = new();

    /// <summary>How many writes to a band or a preset choice have been refused, so a run of writes can tell whether any was.</summary>
    private int _refusals;

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
        _headset.Wrote += attempt =>
        {
            if (attempt.Refused is not null
                && _banks.Values.Any(state => state.Spec.Bands.Contains(attempt.Key) || state.Spec.Select == attempt.Key))
                Interlocked.Increment(ref _refusals);
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
            // One slot per job, so a setting changed meanwhile waits for the
            // slot being read rather than the whole bank: an empty slot
            // never answers, and a bank of empty slots took seconds.
            var customs = await PresetStore.ReadCustoms(bank,
                index => _headset.Post(client => PresetStore.ReadSlot(client, bank, index)),
                () => _headset.Post(client => PresetStore.Count(client, bank)));
            if (!customs.Complete)
                AppLog.Write($"presets: not every {bank} slot could be read, so none is offered as free ({customs.Describe()})");
            var state = new BankState
            {
                Bank = bank,
                Spec = PresetStore.Spec(bank),
                Presets = [.. PresetStore.Factory(bank).Select(PresetNames.Shown), .. customs.Presets],
                FullyRead = customs.Complete,
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
        var same = state.Presets.FirstOrDefault(p => p.Bands.Count == live.Length && p.Bands.SequenceEqual(live));
        if (same is not null) { Adopt(state, same); return; }

        // An edit from before a restart: the headset has forgotten its
        // preset, so take the one it was left on.
        string? preset = AppSettings.Current.LeftOnFor(state.Bank);
        Adopt(state, state.Presets.FirstOrDefault(p => p.Name == preset));
        LeaveSoon();
    }

    /// <summary>Notes where every bank is, to be written once changes stop.</summary>
    /// <remarks>
    /// Taken here, on the caller's thread, and written a second later if
    /// nothing has changed since, so a drag writes the settings file once
    /// rather than at every step.
    /// </remarks>
    private void LeaveSoon()
    {
        var leaving = _banks.Values.ToDictionary(state => state.Bank, state => state.Baseline?.Name);
        int note = Interlocked.Increment(ref _leaves);
        Task.Delay(TimeSpan.FromSeconds(1)).ContinueWith(_ =>
        {
            if (note != Volatile.Read(ref _leaves)) return;
            AppSettings.Update(settings =>
            {
                foreach (var (bank, preset) in leaving) settings.SetLeftOn(bank, preset);
            });
        }, TaskScheduler.Default);
    }

    /// <summary>Takes a preset as the baseline.</summary>
    private void Adopt(BankState state, Preset? preset)
    {
        state.Baseline = preset;
        _flatStart.Remove(state.Bank);
        _new.Remove(state.Bank);
        LeaveSoon();
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
        // Playing flat for a comparison is not choosing the flat preset.
        if (_compares.TryGetValue(state.Bank, out var comparing) && (comparing.Held || comparing.Owes)) return true;
        if (!_headset.TryGetNumberByKey(state.Spec.Select, out int selected)
            || state.Presets.FirstOrDefault(p => p.Id == selected) is not { } chosen) return false;
        // A new preset, or parametric started flat, chooses the flat preset
        // (see ChooseFlat); that is where an edit starts, not a new baseline.
        bool startedFlat = _flatStart.Contains(state.Bank) && chosen.Bands.All(band => band == 0);
        if (state.Baseline?.Id != chosen.Id && !startedFlat) Adopt(state, chosen);
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
        if (_new.Contains(bank)) return Strings.Get("Equaliser_NewPreset");
        // The baseline first: the name the headset reports lags a preset
        // chosen here, and is emptied the moment a band is touched, and a
        // parametric curve names the flat preset it started from.
        if (state.Baseline is { } baseline) return baseline.Name;
        string nameKey = state.Spec.NameKey.ToString("x", CultureInfo.InvariantCulture);
        string reported = _headset.Values.TryGetValue(nameKey, out var raw) ? raw.ToString() : "";
        // A curve that is no preset's still says what it is, rather than nothing.
        return string.IsNullOrWhiteSpace(reported) ? Strings.Get("Equaliser_Unsaved") : reported;
    }

    public void SetBand(Bank bank, int index, int tenths)
    {
        if (!_banks.TryGetValue(bank, out var state)) return;
        if (index < 0 || index >= state.Spec.Bands.Count) return;
        _headset.SetKey(state.Spec.Bands[index], Math.Clamp(PresetStore.Snap(tenths), BandFloor, BandCeiling));
    }

    /// <summary>The curve as an undo would put it back, or null if the headset has not reported its bands.</summary>
    public EqualiserSnapshot? Snapshot(Bank bank) =>
        _banks.TryGetValue(bank, out var state) && LiveBands(state) is { } live
            ? new EqualiserSnapshot(live, state.Baseline?.Id)
            : null;

    /// <summary>Puts a curve back the way an edit does: the preset first, if it was another, then whichever bands differ.</summary>
    /// <remarks>
    /// A preset that has since been deleted cannot be chosen again, so the
    /// bands are put back under whichever preset the curve is on now.
    /// </remarks>
    public async Task Restore(Bank bank, EqualiserSnapshot snapshot)
    {
        if (!_banks.TryGetValue(bank, out var state)) return;

        if (snapshot.BaselineId != state.Baseline?.Id
            && state.Presets.FirstOrDefault(p => p.Id == snapshot.BaselineId) is { } preset)
        {
            await Select(bank, preset);
        }

        var live = LiveBands(state);
        for (int i = 0; i < snapshot.Bands.Count; i++)
            if (live is null || live[i] != snapshot.Bands[i]) SetBand(bank, i, snapshot.Bands[i]);
    }

    // -- compare -----------------------------------------------------------

    /// <summary>Whether the headset is playing the bank flat for a comparison.</summary>
    public bool IsComparing(Bank bank) => _compares.TryGetValue(bank, out var hold) && hold.Held;

    /// <summary>
    /// Plays the bank flat, to hear the curve against none, by choosing the
    /// headset's own flat preset, the one write it always acts on.
    /// </summary>
    /// <remarks>
    /// Writing the bands to zero one at a time showed flat on screen without
    /// changing the sound. The preset and bands are kept here and
    /// put back by <see cref="EndCompare"/>; the baseline and
    /// the saved presets are not touched.
    /// </remarks>
    /// <returns>Whether the headset took every write.</returns>
    public async Task<bool> BeginCompare(Bank bank)
    {
        if (!_banks.TryGetValue(bank, out var state) || LiveBands(state) is not { } live) return false;
        var hold = Compare(bank);
        if (!hold.Press(live, state.Baseline?.Id)) return true;

        if (await Settle(state, () => PlayFlat(state))) return true;
        hold.PressFailed();
        AppLog.Write($"compare: the {bank} equaliser could not be played flat");
        return false;
    }

    /// <summary>Puts the curve back after a comparison, or tries again after a restore that failed.</summary>
    /// <returns>Whether the headset took every write, which is also true when nothing was owed.</returns>
    public async Task<bool> EndCompare(Bank bank)
    {
        if (!_banks.TryGetValue(bank, out var state) || !_compares.TryGetValue(bank, out var hold)) return true;
        if (hold.Release() is not { } owed) return true;

        if (await Settle(state, () => Restore(state, owed))) return true;
        hold.RestoreFailed(owed);
        AppLog.Write($"compare: the {bank} equaliser's curve could not be put back");
        return false;
    }

    /// <summary>Puts back every curve taken off for a comparison, for when the app is closing; the writes are queued, not waited for.</summary>
    /// <returns>Whether any curve was put back, so the writes are worth waiting for.</returns>
    public bool EndAllCompares()
    {
        bool any = false;
        foreach (var (bank, hold) in _compares)
            if (_banks.TryGetValue(bank, out var state) && hold.Release() is { } owed)
            {
                Restore(state, owed);
                any = true;
            }
        return any;
    }

    private CompareHold Compare(Bank bank)
    {
        if (!_compares.TryGetValue(bank, out var hold)) _compares[bank] = hold = new CompareHold();
        return hold;
    }

    /// <summary>Chooses the flat preset, and marks the bands as zero for the screen.</summary>
    private void PlayFlat(BankState state)
    {
        var flat = PresetStore.Factory(state.Bank).First(p => p.Bands.All(band => band == 0));
        _headset.SetKey(state.Spec.Select, flat.Id);
        foreach (int key in state.Spec.Bands) _headset.SetKeyLocally(key, 0);
    }

    /// <summary>
    /// Chooses the preset the curve came from again, then writes whichever
    /// bands differ from it the way an edit does.
    /// </summary>
    /// <remarks>
    /// With no preset to choose, the flat one stays chosen, as it does for a
    /// new preset, and every band is written.
    /// </remarks>
    private void Restore(BankState state, CompareRestore owed)
    {
        var preset = owed.PresetId is int id ? state.Presets.FirstOrDefault(p => p.Id == id) : null;
        if (preset is null)
        {
            _flatStart.Add(state.Bank);
            for (int i = 0; i < owed.Bands.Length; i++) SetBand(state.Bank, i, owed.Bands[i]);
            return;
        }

        _headset.SetKey(state.Spec.Select, preset.Id);
        for (int i = 0; i < preset.Bands.Count && i < state.Spec.Bands.Count; i++)
            _headset.SetKeyLocally(state.Spec.Bands[i], preset.Bands[i]);
        for (int i = 0; i < owed.Bands.Length; i++)
            if (i >= preset.Bands.Count || preset.Bands[i] != owed.Bands[i]) SetBand(state.Bank, i, owed.Bands[i]);
    }

    /// <summary>Makes the writes and waits until the headset has had them all.</summary>
    /// <remarks>
    /// The headset sends no reply to a set. A write refused or lost before it
    /// is sent is raised as it happens, and a job queued after the writes
    /// finishes once they have all been tried, so counting the refusals
    /// across it says whether any was.
    /// </remarks>
    private async Task<bool> Settle(BankState state, Action write)
    {
        int refusedBefore = Volatile.Read(ref _refusals);
        write();
        try { await _headset.Post(_ => true); }
        catch (Exception ex)
        {
            AppLog.Write($"compare: the headset did not take the {state.Bank} equaliser's bands: {ex.Message}");
            return false;
        }
        return Volatile.Read(ref _refusals) == refusedBefore;
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
        Adopt(state, preset);
        _headset.SetKey(state.Spec.Select, preset.Id);
        for (int i = 0; i < preset.Bands.Count && i < state.Spec.Bands.Count; i++)
            _headset.SetKeyLocally(state.Spec.Bands[i], preset.Bands[i]);
        await Task.CompletedTask;
    }

    /// <summary>Starts a new preset: flat, belonging to no preset, in whichever form the bank is shaped.</summary>
    /// <remarks>
    /// With no baseline there is nothing to discard or overwrite, so only
    /// saving as new is offered, and a new preset needs no other to be
    /// chosen and changed first.
    /// </remarks>
    public void StartNew(Bank bank)
    {
        if (!_banks.TryGetValue(bank, out var state)) return;
        Adopt(state, null);
        ChooseFlat(state);
        _new.Add(bank);
    }

    /// <summary>Whether the bank is on a new preset, not yet saved.</summary>
    public bool IsNew(Bank bank) => _new.Contains(bank);

    /// <summary>Makes the curve flat by choosing the headset's own flat preset, as a starting point.</summary>
    /// <remarks>
    /// One write the headset always acts on. Setting the bands to zero one by
    /// one showed flat on screen while the sound did not change.
    /// </remarks>
    private void ChooseFlat(BankState state)
    {
        var flat = PresetStore.Factory(state.Bank).First(p => p.Bands.All(band => band == 0));
        _headset.SetKey(state.Spec.Select, flat.Id);
        foreach (int key in state.Spec.Bands) _headset.SetKeyLocally(key, 0);
        _flatStart.Add(state.Bank);
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

        // A slot not read may hold a preset, so the bank may be full and a
        // name in it unseen. Saving over a preset needs no free slot.
        if (replacing is null && !state.FullyRead) return Strings.Get("Preset_SlotsUnread");
        var free = state.FreeSlots;
        if (replacing is null && free.Count == 0)
            return Strings.Format("Preset_Full", PresetStore.CustomSlots);

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

        if (await ReadBack(bank, "a save"))
            Adopt(_banks[bank], _banks[bank].Custom.FirstOrDefault(p => p.Name == name));
        return null;
    }

    public async Task<string?> Delete(Bank bank, string name)
    {
        try { await _headset.Post(client => { PresetStore.Delete(client, name, bank); return true; }); }
        catch (Exception ex) { return ex.Message; }
        await ReadBack(bank, "a delete");
        return null;
    }

    /// <summary>Reads the bank again after a save or delete the headset took; says whether it could.</summary>
    /// <remarks>
    /// The change is already on the headset, so a read that fails here, as
    /// when the headset goes just after, is logged rather than reported as
    /// the change failing.
    /// </remarks>
    private async Task<bool> ReadBack(Bank bank, string after)
    {
        try
        {
            await Load(bank);
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Write($"presets: the {bank} equaliser's presets could not be read again after {after}: {ex.Message}");
            return false;
        }
    }
}
