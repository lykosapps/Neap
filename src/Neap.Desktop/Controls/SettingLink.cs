using Avalonia.Controls;
using Neap.Core;
using Neap.Core.Settings;

namespace Neap.Desktop.Controls;

/// <summary>
/// Ties a control to one registry setting: finds the setting, paints the
/// control whenever the headset reports, and sends the person's changes back.
/// </summary>
/// <remarks>
/// <para>
/// Listens on every load, not only the first. A control is unloaded and
/// loaded again whenever it moves; a control that listens only once stays at
/// whatever it showed then, a dash if the headset was off. A move can raise
/// Loaded at the new place before Unloaded at the old one, so an Unloaded
/// while still loaded is ignored; otherwise the control stops listening for
/// good and misses the headset's own buttons.
/// </para>
/// <para>
/// A setting for a function the connected headset doesn't have is hidden, with
/// the control around it; see <see cref="HeadsetModels.Shows(HeadsetModel?, string)"/>.
/// </para>
/// <para>
/// A name the registry does not know is a mistake in a page, so it is logged
/// and the control is disabled rather than left looking as if it works.
/// </para>
/// </remarks>
internal sealed class SettingLink
{
    private readonly Control _owner;
    private readonly Func<string> _name;
    private readonly Action<SettingKey> _build;
    private readonly Action _paint;
    private bool _listening;

    /// <param name="owner">The control whose loading decides when to listen.</param>
    /// <param name="name">The setting's registry name, read at the first load.</param>
    /// <param name="build">Builds the control for the setting, once.</param>
    /// <param name="paint">Shows the setting's current value.</param>
    public SettingLink(Control owner, Func<string> name, Action<SettingKey> build, Action paint)
    {
        _owner = owner;
        _name = name;
        _build = build;
        _paint = paint;
        owner.Loaded += (_, _) =>
        {
            if (!Resolve()) return;
            Listen(true);
            Paint();
        };
        owner.Unloaded += (_, _) =>
        {
            if (!owner.IsLoaded) Listen(false);
        };
    }

    /// <summary>Gets the setting, once the control has loaded and found it.</summary>
    public SettingKey? Key { get; private set; }

    /// <summary>Gets whether the setting can be changed: one Neap writes, on a headset whose settings it changes.</summary>
    public bool CanWrite => Key is { Writable: true } && !AppServices.Headset.ReadOnly;

    /// <summary>Gets whether the control is being painted, when its change events are not the person's.</summary>
    public bool Painting { get; private set; }

    /// <summary>Gets the setting's last reported value, or null if the headset has not reported it.</summary>
    public int? Value =>
        Key is not null && AppServices.Headset.TryGetNumberByKey(Key.Key, out int value) ? value : null;

    /// <summary>Sends a value the person chose. Ignored while painting.</summary>
    public void Write(int value)
    {
        if (Painting || Key is null) return;
        AppServices.Headset.SetKey(Key.Key, value);
    }

    private bool Resolve()
    {
        if (Key is not null) return true;
        string name = _name();
        if (!Registry.ByName.TryGetValue(name, out var key))
        {
            AppLog.Write($"a control asked for setting '{name}', which the registry does not have");
            _owner.IsEnabled = false;
            return false;
        }
        Key = key;
        _build(key);
        return true;
    }

    private void Listen(bool on)
    {
        if (on == _listening) return;
        _listening = on;
        if (on) AppServices.Headset.Changed += Paint;
        else AppServices.Headset.Changed -= Paint;
    }

    private void Paint()
    {
        if (Key is not null) _owner.IsVisible = HeadsetModels.Shows(AppServices.Headset.Model, Key.Name);
        Painting = true;
        try { _paint(); }
        finally { Painting = false; }
    }
}
