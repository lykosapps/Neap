namespace Neap.Services;

/// <summary>
/// Global keyboard shortcuts that move the mix, whichever system takes the keys.
/// </summary>
/// <remarks>
/// <para>
/// What the keys are, which are on, what each does and what is said when one
/// is refused is the same on every system, so it is kept here once. A system
/// supplies only how to take a set of keys and how to let them go again.
/// </para>
/// <para>
/// A refused key says so. Another program holding a combination is the normal
/// failure, and it is invisible: the keys simply do nothing. Every key is
/// checked, and what failed is shown beside the key it failed for.
/// </para>
/// </remarks>
public abstract class MixHotkeys : IHotkeys
{
    /// <summary>How far one press moves the mix, on the same 0-100 scale.</summary>
    private const int Step = 10;

    private readonly MixService _mix;
    private readonly object _gate = new();
    private readonly Dictionary<MixKey, Shortcut> _keys = new();
    private readonly Dictionary<MixKey, string> _trouble = new();

    protected MixHotkeys(MixService mix)
    {
        _mix = mix;
        foreach (var (which, shortcut) in MixShortcuts.Defaults) _keys[which] = shortcut;
        foreach (var (which, shortcut) in AppSettings.Current.Shortcuts())
            if (shortcut.Sane) _keys[which] = shortcut;
    }

    /// <summary>A key was registered, refused, changed, or given up.</summary>
    public event Action? Changed;

    public bool Supported => true;

    public bool Enabled { get; private set; }

    public Shortcut Key(MixKey which)
    {
        lock (_gate) return _keys[which];
    }

    /// <summary>What the system said when it refused this one, or null.</summary>
    public string? Trouble(MixKey which)
    {
        lock (_gate) return _trouble.GetValueOrDefault(which);
    }

    public void Enable(bool enabled)
    {
        if (enabled == Enabled) return;
        if (enabled) Begin(); else End();
    }

    /// <summary>
    /// Rebind one of them. Takes the lot again rather than the one, which is
    /// simpler than asking a system to change a key it holds.
    /// </summary>
    public void Rebind(MixKey which, Shortcut shortcut)
    {
        if (!shortcut.Sane) return;
        bool was = Enabled;
        if (was) End();
        lock (_gate) _keys[which] = shortcut;
        AppSettings.Update(s => s.SetShortcut(which, shortcut));
        if (was) Begin(); else Changed?.Invoke();
    }

    public void ResetToDefaults()
    {
        bool was = Enabled;
        if (was) End();
        lock (_gate)
            foreach (var (which, shortcut) in MixShortcuts.Defaults) _keys[which] = shortcut;
        AppSettings.Update(s => s.ClearShortcuts());
        if (was) Begin(); else Changed?.Invoke();
    }

    /// <summary>Takes these keys, and says which could not be taken and why. Calls <see cref="Pressed"/> for each press from then on.</summary>
    protected abstract IReadOnlyDictionary<MixKey, string> Start(IReadOnlyDictionary<MixKey, Shortcut> wanted);

    /// <summary>Lets the keys go.</summary>
    protected abstract void LetGo();

    /// <summary>One of the keys was pressed; may be called from any thread.</summary>
    protected void Pressed(MixKey which) => Platform.Post(() => Move(which));

    private void Begin()
    {
        Dictionary<MixKey, Shortcut> wanted;
        lock (_gate) wanted = new Dictionary<MixKey, Shortcut>(_keys);

        var refused = Start(wanted);
        lock (_gate)
        {
            _trouble.Clear();
            foreach (var (which, why) in refused) _trouble[which] = why;
        }
        Enabled = true;
        Changed?.Invoke();
    }

    private void End()
    {
        if (!Enabled) return;
        Enabled = false;
        LetGo();
        lock (_gate) _trouble.Clear();
        Changed?.Invoke();
    }

    /// <summary>
    /// Move the mix one step. It goes through <see cref="MixService.Apply"/>,
    /// so a press lands in the centre detent exactly as the wheel does,
    /// including its cue.
    /// </summary>
    private void Move(MixKey which)
    {
        if (!_mix.Running) return;
        int now = _mix.Mix;
        _mix.Apply(which switch
        {
            MixKey.TowardGame => now - Step,
            MixKey.TowardChat => now + Step,
            _ => 50,
        }, "keyboard");
    }

    public void Dispose()
    {
        End();
        GC.SuppressFinalize(this);
    }
}
