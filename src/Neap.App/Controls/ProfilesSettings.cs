using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Neap.App.Services;
using Neap.Core.Profiles;
using Windows.Storage.Pickers;

namespace Neap.App.Controls;

/// <summary>
/// Every saved profile, for renaming, deleting, and choosing which apps it
/// belongs to, and which one is the default. Switching by hand is the header
/// bar's job, not this list's; see <see cref="ProfileBar"/>. Switching as apps
/// start and close is <see cref="AutoSwitchService"/>'s.
/// </summary>
/// <remarks>
/// The rows are rebuilt only when something they show has changed. The
/// profiles change on every read of the headset, and rebuilding each time
/// would take the keyboard's place out from under whoever is on a row.
/// </remarks>
public sealed class ProfilesSettings : UserControl
{
    private readonly StackPanel _rows = new() { Spacing = 8 };

    private readonly TextBlock _empty = new()
    {
        Text = Strings.Get("Profiles_Empty"),
        TextWrapping = TextWrapping.Wrap,
        Style = (Style)Application.Current.Resources["SecondaryBodyTextStyle"],
    };

    private readonly ComboBox _default = new() { MinWidth = 180 };
    private readonly CommunityToolkit.WinUI.Controls.SettingsCard _defaultCard;

    /// <summary>The profile whose delete is pending confirmation, if any.</summary>
    private string? _confirmingDeleteId;

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
        _defaultCard = new CommunityToolkit.WinUI.Controls.SettingsCard { Header = header, Content = _default };
        // After the list has finished choosing, not during: setting the default
        // repaints, and a list changed while it is still choosing brings the
        // whole app down.
        _default.SelectionChanged += (_, _) =>
        {
            if (_painting || _default.SelectedItem is not ComboBoxItem chosen) return;
            string? id = chosen.Tag as string;
            DispatcherQueue.TryEnqueue(() => AppServices.Profiles.SetDefault(id));
        };

        Content = new StackPanel { Spacing = 8, Children = { _defaultCard, _rows, _empty } };
        Loaded += async (_, _) =>
        {
            AppServices.Profiles.Changed += Paint;
            Paint();
            await LoadNames();
            Paint();
        };
        Unloaded += (_, _) => AppServices.Profiles.Changed -= Paint;
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
        string shown = string.Join("\n", profiles.Select(p => $"{p.Id}|{p.Name}|{string.Join(",", p.AssignedApps.Select(Named))}"))
            + $"\n{_confirmingDeleteId}\n{defaultId}";
        if (shown == _shown) return;
        _shown = shown;

        _empty.Visibility = profiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _defaultCard.Visibility = profiles.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        _rows.Children.Clear();
        foreach (var profile in profiles)
            _rows.Children.Add(_confirmingDeleteId == profile.Id ? ConfirmRow(profile) : Row(profile));

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

    /// <summary>
    /// A profile's row: its name and assigned apps, a way to choose apps, a
    /// way to rename it, and a way to delete it.
    /// </summary>
    private UIElement Row(Profile profile)
    {
        var name = new TextBlock { Text = profile.Name, TextTrimming = TextTrimming.CharacterEllipsis };
        var caption = new TextBlock
        {
            Text = profile.AssignedApps.Count > 0
                ? Strings.List(profile.AssignedApps.Select(Named).ToList())
                : Strings.Get("Profile_NoApps"),
            Style = (Style)Application.Current.Resources["SecondaryCaptionTextStyle"],
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { name, caption } };

        var apps = new Button { Content = new FontIcon { Glyph = "", FontSize = 12 } };
        string appsName = Strings.Format("Profile_AppsTitle", profile.Name);
        AutomationProperties.SetName(apps, appsName);
        // Unlike rename and delete, this icon has no meaning anyone already
        // knows, so it gets the one exception to no help text: a tooltip.
        ToolTipService.SetToolTip(apps, appsName);
        apps.Click += async (_, _) => await OpenAppPicker(profile);

        var rename = new Button { Content = new FontIcon { Glyph = "", FontSize = 12 } };
        AutomationProperties.SetName(rename, Strings.Format("Profile_RenameNamed", profile.Name));
        rename.Click += async (_, _) =>
        {
            string typed = await ProfileNaming.Ask(XamlRoot, Strings.Get("Profile_RenameTitle"), profile.Name, profile.Id);
            if (typed.Length > 0) AppServices.Profiles.Rename(profile.Id, typed);
        };

        var delete = new Button { Content = new FontIcon { Glyph = "", FontSize = 12 } };
        AutomationProperties.SetName(delete, Strings.Format("Profile_DeleteNamed", profile.Name));
        delete.Click += (_, _) =>
        {
            _confirmingDeleteId = profile.Id;
            Paint();
        };

        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(apps, 1);
        Grid.SetColumn(rename, 2);
        Grid.SetColumn(delete, 3);
        grid.Children.Add(names);
        grid.Children.Add(apps);
        grid.Children.Add(rename);
        grid.Children.Add(delete);

        return Card(grid);
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
    /// picked as a file.
    /// </remarks>
    private async Task OpenAppPicker(Profile profile)
    {
        await LoadNames();
        var assigned = AppServices.Profiles.All.FirstOrDefault(p => p.Id == profile.Id)?.AssignedApps
            ?? Array.Empty<string>();
        var candidates = await AppServices.Mix.Candidates("a profile's apps picker");

        var list = new StackPanel { Spacing = 8 };
        var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string process, string display, bool isChecked)
        {
            if (!listed.Add(process)) return;
            var box = new CheckBox { Content = display, Tag = process, IsChecked = isChecked };
            box.Checked += (_, _) => AppServices.Profiles.Assign(profile.Id, process, display);
            box.Unchecked += (_, _) => AppServices.Profiles.Unassign(process);
            list.Children.Add(box);
        }

        foreach (var candidate in candidates)
            Add(candidate.Process, candidate.Display, assigned.Contains(candidate.Process, StringComparer.OrdinalIgnoreCase));
        foreach (string process in assigned)
            Add(process, Named(process), true);

        var hint = new TextBlock
        {
            Text = Strings.Get("Profile_AppsHint"),
            TextWrapping = TextWrapping.Wrap,
            Style = (Style)Application.Current.Resources["SecondaryCaptionTextStyle"],
            Visibility = listed.Count == 0 ? Visibility.Visible : Visibility.Collapsed,
        };

        var browse = new Button { Content = Strings.Get("Profile_Browse") };
        browse.Click += async (_, _) =>
        {
            if (await PickProgram() is not { } picked) return;
            hint.Visibility = Visibility.Collapsed;
            _names[picked.Process] = picked.Display;
            Add(picked.Process, picked.Display, isChecked: false);

            // Picking a program means assigning it, whether or not it was already listed.
            list.Children.OfType<CheckBox>().First(b => (string)b.Tag == picked.Process).IsChecked = true;
        };

        await new NeapDialog
        {
            XamlRoot = XamlRoot,
            Title = Strings.Format("Profile_AppsTitle", profile.Name),
            Content = new StackPanel { MinWidth = 260, Spacing = 12, Children = { list, hint, browse } },
            CloseButtonText = Strings.Get("Dialog_OK"),
        }.ShowAsync();
    }

    /// <summary>Asks for a program as a file, and names it the way a running one is named.</summary>
    private static async Task<(string Process, string Display)?> PickProgram()
    {
        var picker = new FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(MainWindow.Instance!));
        picker.FileTypeFilter.Add(".exe");
        var file = await picker.PickSingleFileAsync();
        if (file is null) return null;

        string process = ProgramName.Of(file.Path);
        string display = process;
        try
        {
            string? described = System.Diagnostics.FileVersionInfo.GetVersionInfo(file.Path).FileDescription;
            if (!string.IsNullOrWhiteSpace(described)) display = described;
        }
        catch (Exception ex) { AppLog.Write($"profiles: could not read the description of {file.Name}: {ex.Message}"); }
        return (process, display);
    }

    private string Named(string process) => _names.GetValueOrDefault(process) ?? AppServices.Profiles.DisplayName(process);

    /// <summary>
    /// The in-place confirmation that replaces a profile's row while its
    /// deletion is pending: Delete and Cancel, with no dialog, matching the
    /// equaliser's own preset delete.
    /// </summary>
    private UIElement ConfirmRow(Profile profile)
    {
        var name = new TextBlock
        {
            Text = profile.Name,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var delete = new Button { Content = Strings.Get("Profile_Delete") };
        var cancel = new Button { Content = Strings.Get("Dialog_Cancel") };
        cancel.Loaded += (_, _) => cancel.Focus(FocusState.Programmatic);
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

        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(delete, 1);
        Grid.SetColumn(cancel, 2);
        row.Children.Add(name);
        row.Children.Add(delete);
        row.Children.Add(cancel);

        return Card(row);
    }

    private static UIElement Card(UIElement content) => new Border
    {
        Style = (Style)Application.Current.Resources["QuickCardStyle"],
        Padding = new Thickness(12),
        Child = content,
    };
}
