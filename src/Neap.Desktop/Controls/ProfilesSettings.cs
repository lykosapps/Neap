using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Neap.Core.Profiles;

namespace Neap.Desktop.Controls;

/// <summary>
/// Every saved profile, for renaming, deleting, and choosing which apps it
/// belongs to, and which one is the default. Switching by hand is the header
/// bar's job, not this list's; see <see cref="ProfileBar"/>. Switching as apps
/// start and close is <see cref="AutoSwitchService"/>'s.
/// </summary>
/// <remarks>
/// The rows are rebuilt only when something they show has changed. The
/// profiles change on every read of the headset, and rebuilding each time
/// would take the keyboard's place out from under whoever is on a row. When
/// they are rebuilt, the keyboard is put back; see <see cref="Refocus"/>.
/// </remarks>
public sealed class ProfilesSettings : UserControl
{
    private readonly StackPanel _rows = new() { Classes = { "dialogflow" } };

    private readonly TextBlock _empty = new()
    {
        Text = Strings.Get("Profiles_Empty"),
        Classes = { "secondary" },
    };

    private readonly ComboBox _default = new();
    private readonly SettingsCard _defaultCard;

    /// <summary>The profile whose delete is pending confirmation, if any.</summary>
    private string? _confirmingDeleteId;

    /// <summary>What a row's button does, which says where the keyboard goes back to after a rebuild.</summary>
    private enum Role { Apps, Rename, Delete, Confirm, Keep }

    /// <summary>The rows' buttons, by profile and by what each does.</summary>
    private readonly Dictionary<(string Id, Role Role), Button> _buttons = [];

    /// <summary>The profiles the rows were last built for, in order.</summary>
    private List<string> _ids = [];

    /// <summary>What the rows and the default were last built from; see the remarks.</summary>
    private string _shown = "";

    /// <summary>The profiles the default's list was last filled with.</summary>
    private string _offered = "";
    private bool _painting;

    /// <summary>An app's display name, by process, learned whenever the app picker loads candidates.</summary>
    private readonly Dictionary<string, string> _names = new(StringComparer.OrdinalIgnoreCase);

    public ProfilesSettings()
    {
        string header = Strings.Get("Profiles_DefaultHeader");
        AutomationProperties.SetName(_default, header);
        _defaultCard = new SettingsCard { Header = header, Content = _default };
        // After the list has finished choosing, not during: setting the default
        // repaints, and a list changed while it is still choosing is not safe.
        _default.SelectionChanged += (_, _) =>
        {
            if (_painting || _default.SelectedItem is not ComboBoxItem chosen) return;
            string? id = chosen.Tag as string;
            Dispatcher.UIThread.Post(() => AppServices.Profiles.SetDefault(id));
        };

        Content = new StackPanel { Classes = { "dialogflow" }, Children = { _defaultCard, _rows, _empty } };
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Profiles.Changed += Paint;
        // An app starting or stopping moves the profile in use without a change to the profiles.
        AppServices.AutoSwitch.Changed += Paint;
        Paint();
        await LoadNames();
        Paint();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Profiles.Changed -= Paint;
        AppServices.AutoSwitch.Changed -= Paint;
    }

    private async Task LoadNames()
    {
        foreach (var candidate in await AppServices.Mix.Candidates("the names in the profiles list"))
            _names[candidate.Process] = candidate.Display;
    }

    private void Paint()
    {
        var profiles = AppServices.Profiles.All;
        string? defaultId = AppServices.Profiles.DefaultId;
        string shown = string.Join("\n", profiles.Select(p => $"{p.Id}|{p.Name}|{string.Join(",", p.AssignedApps.Select(app => Described(p, app)))}"))
            + $"\n{_confirmingDeleteId}\n{defaultId}\n{AppServices.Profiles.ActiveId}";
        if (shown == _shown) return;
        _shown = shown;

        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        var was = _buttons.FirstOrDefault(b => ReferenceEquals(b.Value, focused)).Key;
        int at = was.Id is null ? -1 : _ids.IndexOf(was.Id);

        _empty.IsVisible = profiles.Count == 0;
        _defaultCard.IsVisible = profiles.Count != 0;
        _rows.Children.Clear();
        _buttons.Clear();
        _ids = profiles.Select(p => p.Id).ToList();
        foreach (var profile in profiles)
            _rows.Children.Add(_confirmingDeleteId == profile.Id ? ConfirmRow(profile) : Row(profile));
        if (was.Id is not null) Refocus(was, at);

        _painting = true;
        try
        {
            string offered = string.Join("\n", profiles.Select(p => $"{p.Id}|{p.Name}"));
            if (offered != _offered)
            {
                _offered = offered;
                _default.Items.Clear();
                _default.Items.Add(new ComboBoxItem { Content = Strings.Get("Profile_DefaultNone") });
                foreach (var profile in profiles)
                    _default.Items.Add(new ComboBoxItem { Content = profile.Name, Tag = profile.Id });
            }
            var chosen = _default.Items.OfType<ComboBoxItem>().FirstOrDefault(i => i.Tag as string == defaultId);
            if (!ReferenceEquals(_default.SelectedItem, chosen)) _default.SelectedItem = chosen;
        }
        finally { _painting = false; }
    }

    /// <summary>Puts the keyboard back where it was before the rows were rebuilt, or on the nearest button left.</summary>
    /// <remarks>
    /// Rebuilding takes the focused button away, and with nothing focused the
    /// next Tab starts again from the top of the window. The same button on
    /// the same profile takes it back; leaving a delete's Delete and Cancel
    /// goes back to the row's delete button; the row of a deleted profile
    /// hands it to the row now in its place. Asking to delete needs nothing
    /// here, as Cancel takes the keyboard itself. Posted, as the new rows are
    /// not laid out yet.
    /// </remarks>
    private void Refocus((string Id, Role Role) was, int at)
    {
        Button? target;
        if (_buttons.TryGetValue(was, out var same)) target = same;
        else if (_ids.Contains(was.Id)) target = was.Role is Role.Confirm or Role.Keep ? _buttons.GetValueOrDefault((was.Id, Role.Delete)) : null;
        else if (_ids.Count > 0)
        {
            string next = _ids[Math.Min(at, _ids.Count - 1)];
            target = _buttons.GetValueOrDefault((next, Role.Apps)) ?? _buttons.GetValueOrDefault((next, Role.Keep));
        }
        else target = null;

        if (target is not null) Dispatcher.UIThread.Post(() => target.Focus(), DispatcherPriority.Loaded);
    }

    /// <summary>Keeps a row's button, so <see cref="Refocus"/> can find it after the next rebuild.</summary>
    private Button Kept(Button button, Profile profile, Role role)
    {
        _buttons[(profile.Id, role)] = button;
        return button;
    }

    private static PathIcon Icon(string key)
    {
        var resources = Application.Current!;
        double size = (double)resources.FindResource("NeapIconRow")!;
        return new PathIcon { Width = size, Height = size, Data = (Geometry)resources.FindResource(key)! };
    }

    /// <summary>
    /// A profile's row: its name and assigned apps, tinted and marked "In use" while it is the active profile, a way to choose apps, a
    /// way to rename it, and a way to delete it.
    /// </summary>
    private Control Row(Profile profile)
    {
        var apps = Kept(new Button { Content = Icon("IconApps"), Classes = { "iconbutton" } }, profile, Role.Apps);
        string appsName = Strings.Format("Profile_AppsTitle", profile.Name);
        AutomationProperties.SetName(apps, appsName);
        // Unlike rename and delete, this icon has no meaning anyone already
        // knows, so it gets the one exception to no help text: a tooltip.
        ToolTip.SetTip(apps, appsName);
        apps.Click += async (_, _) => await OpenAppPicker(profile);

        // The row's line of apps is drawn, not reached from the keyboard, so the
        // button that changes them says them to a screen reader too.
        string assigned = profile.AssignedApps.Count > 0
            ? Strings.List(profile.AssignedApps.Select(app => Described(profile, app)).ToList())
            : Strings.Get("Profile_NoApps");
        AutomationProperties.SetHelpText(apps, assigned);

        var rename = Kept(new Button { Content = Icon("IconEdit"), Classes = { "iconbutton" } }, profile, Role.Rename);
        AutomationProperties.SetName(rename, Strings.Format("Profile_RenameNamed", profile.Name));
        rename.Click += async (_, _) =>
        {
            string typed = await ProfileNaming.Ask(this, Strings.Get("Profile_RenameTitle"), profile.Name, profile.Id);
            if (typed.Length > 0) AppServices.Profiles.Rename(profile.Id, typed);
        };

        var delete = Kept(new Button { Content = Icon("IconDelete"), Classes = { "iconbutton" } }, profile, Role.Delete);
        AutomationProperties.SetName(delete, Strings.Format("Profile_DeleteNamed", profile.Name));
        delete.Click += (_, _) =>
        {
            _confirmingDeleteId = profile.Id;
            Paint();
        };

        var card = new SettingsCard
        {
            Header = profile.Name,
            Description = assigned,
            Content = new StackPanel { Classes = { "gaprow" }, Children = { apps, rename, delete } },
        };
        if (profile.Id == AppServices.Profiles.ActiveId)
        {
            string inUse = Strings.Get("Profile_InUse");
            card.Below = new TextBlock { Text = inUse, Classes = { "caption", "inuse" } };
            card.Bind(BackgroundProperty, card.GetResourceObservable("NeapPresetSelectedBrush"));
            AutomationProperties.SetItemStatus(apps, inUse);
        }
        return card;
    }

    /// <summary>
    /// A dialog, not a flyout: checking a box saves at once, through
    /// <see cref="ProfileService"/>, which repaints every row including this
    /// one's, and a flyout closes when the button that owns it is rebuilt out
    /// from under it. A dialog sits on the window itself and is not.
    /// </summary>
    /// <remarks>
    /// Lists what is making sound now, and every app already assigned to this
    /// profile whether it is running or not, so it can always be taken away.
    /// A program that is not running, such as a game about to be started, is
    /// picked as a file. Each app checked has a choice of when it switches:
    /// while it is open, or while it is using the microphone.
    /// </remarks>
    private async Task OpenAppPicker(Profile profile)
    {
        await LoadNames();
        var saved = AppServices.Profiles.All.FirstOrDefault(p => p.Id == profile.Id) ?? profile;
        var candidates = await AppServices.Mix.Candidates("a profile's apps picker");

        var list = new StackPanel { Classes = { "dialogflow" } };
        var boxes = new Dictionary<string, CheckBox>(StringComparer.OrdinalIgnoreCase);

        void Add(string process, string display, bool isChecked)
        {
            if (boxes.ContainsKey(process)) return;
            var box = new CheckBox { Content = display, IsChecked = isChecked };
            var when = new ComboBox
            {
                Items =
                {
                    new ComboBoxItem { Content = Strings.Get("Profile_WhileOpen") },
                    new ComboBoxItem { Content = Strings.Get("Profile_WhileOnMicrophone") },
                },
                SelectedIndex = saved.MicrophoneApps.Contains(process, StringComparer.OrdinalIgnoreCase) ? 1 : 0,
                IsVisible = isChecked,
            };
            AutomationProperties.SetName(when, Strings.Format("Profile_SwitchWhen", display));
            when.SelectionChanged += (_, _) => AppServices.Profiles.SwitchOnMicrophone(process, when.SelectedIndex == 1);
            box.IsCheckedChanged += (_, _) =>
            {
                when.IsVisible = box.IsChecked == true;
                if (box.IsChecked == true) AppServices.Profiles.Assign(profile.Id, process, display);
                else
                {
                    AppServices.Profiles.Unassign(process);
                    // Unassigning forgets when it switched, so checking it again starts from open.
                    when.SelectedIndex = 0;
                }
            };
            boxes[process] = box;
            DockPanel.SetDock(when, Dock.Right);
            list.Children.Add(new DockPanel { Children = { when, box } });
        }

        // Added with the box already in its state, so adding a row is not a choice.
        foreach (var candidate in candidates)
            Add(candidate.Process, candidate.Display, saved.AssignedApps.Contains(candidate.Process, StringComparer.OrdinalIgnoreCase));
        foreach (string process in saved.AssignedApps)
            Add(process, Named(process), true);

        var hint = new TextBlock
        {
            Text = Strings.Get("Profile_AppsHint"),
            Classes = { "caption", "secondary" },
            IsVisible = boxes.Count == 0,
        };

        var browse = new Button { Content = Strings.Get("Profile_Browse") };
        browse.Click += async (_, _) =>
        {
            if (await PickProgram() is not { } picked) return;
            hint.IsVisible = false;
            _names[picked.Process] = picked.Display;
            Add(picked.Process, picked.Display, isChecked: false);

            // Picking a program means assigning it, whether or not it was already listed.
            boxes[picked.Process].IsChecked = true;
        };

        await new NeapDialog
        {
            Heading = Strings.Format("Profile_AppsTitle", profile.Name),
            Body = new ScrollViewer
            {
                Classes = { "dialogscroll" },
                Content = new StackPanel { Classes = { "dialogflow" }, MinWidth = 360, Children = { list, hint, browse } },
            },
            CloseButtonText = Strings.Get("Dialog_OK"),
        }.ShowAsync(this);
    }

    /// <summary>Asks for a program as a file, and names it the way a running one is named.</summary>
    private async Task<(string Process, string Display)?> PickProgram()
    {
        if (TopLevel.GetTopLevel(this) is not { } top) return null;
        var options = new FilePickerOpenOptions { AllowMultiple = false };
        // Only Windows programs have a name that ends in a type; elsewhere any file may be one.
        if (OperatingSystem.IsWindows()) options.FileTypeFilter = [new FilePickerFileType(Strings.Get("Profile_ProgramsFileType")) { Patterns = ["*.exe"] }];
        var picked = await top.StorageProvider.OpenFilePickerAsync(options);
        if (picked.Count == 0 || picked[0].TryGetLocalPath() is not { } path) return null;

        string process = ProgramName.Of(path);
        string display = process;
        try
        {
            string? described = System.Diagnostics.FileVersionInfo.GetVersionInfo(path).FileDescription;
            if (!string.IsNullOrWhiteSpace(described)) display = described;
        }
        catch (Exception ex) { AppLog.Write($"profiles: could not read the description of {Path.GetFileName(path)}: {ex.Message}"); }
        return (process, display);
    }

    private string Named(string process) => _names.GetValueOrDefault(process) ?? AppServices.Profiles.DisplayName(process);

    /// <summary>An assigned app as its profile's row lists it, saying so when it switches only on the microphone.</summary>
    private string Described(Profile profile, string app) =>
        profile.MicrophoneApps.Contains(app, StringComparer.OrdinalIgnoreCase)
            ? Strings.Format("Profile_AppOnMicrophone", Named(app))
            : Named(app);

    /// <summary>
    /// The in-place confirmation that replaces a profile's row while its
    /// deletion is pending: Delete and Cancel, with no dialog, matching the
    /// equaliser's own preset delete.
    /// </summary>
    private Control ConfirmRow(Profile profile)
    {
        var delete = Kept(new Button { Content = Strings.Get("Profile_Delete") }, profile, Role.Confirm);
        var cancel = Kept(new Button { Content = Strings.Get("Dialog_Cancel") }, profile, Role.Keep);
        cancel.AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => cancel.Focus());
        AutomationProperties.SetName(delete, Strings.Format("Profile_DeleteNamed", profile.Name));
        AutomationProperties.SetName(cancel, Strings.Format("Profile_KeepNamed", profile.Name));
        delete.Click += (_, _) =>
        {
            _confirmingDeleteId = null;
            AppServices.Profiles.Delete(profile.Id);
        };
        cancel.Click += (_, _) =>
        {
            _confirmingDeleteId = null;
            Paint();
        };

        return new SettingsCard
        {
            Header = profile.Name,
            Content = new StackPanel { Classes = { "gaprow" }, Children = { delete, cancel } },
        };
    }
}
