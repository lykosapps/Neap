using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Neap.App.Services;
using Neap.Core.Profiles;

namespace Neap.App.Controls;

/// <summary>
/// Every saved profile, for renaming, deleting, and choosing which apps it
/// belongs to. Switching is the header bar's job, not this list's; see
/// <see cref="ProfileBar"/>.
/// </summary>
/// <remarks>
/// Assigning an app here only records it; nothing yet switches a profile on
/// its own when that app is running. See DECISIONS.md.
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

    /// <summary>The profile whose delete is pending confirmation, if any.</summary>
    private string? _confirmingDeleteId;

    /// <summary>An app's display name, by process, learned whenever the app picker loads candidates.</summary>
    private readonly Dictionary<string, string> _names = new(StringComparer.OrdinalIgnoreCase);

    public ProfilesSettings()
    {
        Content = new StackPanel { Spacing = 8, Children = { _rows, _empty } };
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
        foreach (var candidate in await AppServices.Mix.Candidates())
            _names[candidate.Process] = candidate.Display;
    }

    private void Paint()
    {
        var profiles = AppServices.Profiles.All;
        _empty.Visibility = profiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _rows.Children.Clear();
        foreach (var profile in profiles)
            _rows.Children.Add(_confirmingDeleteId == profile.Id ? ConfirmRow(profile) : Row(profile));
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
    private async Task OpenAppPicker(Profile profile)
    {
        await LoadNames();
        var assigned = AppServices.Profiles.All.FirstOrDefault(p => p.Id == profile.Id)?.AssignedApps
            ?? Array.Empty<string>();
        var candidates = await AppServices.Mix.Candidates();

        var list = new StackPanel { Spacing = 8 };
        foreach (var candidate in candidates)
        {
            var box = new CheckBox
            {
                Content = candidate.Display,
                IsChecked = assigned.Contains(candidate.Process, StringComparer.OrdinalIgnoreCase),
            };
            string process = candidate.Process;
            box.Checked += (_, _) => AppServices.Profiles.Assign(profile.Id, process);
            box.Unchecked += (_, _) => AppServices.Profiles.Unassign(process);
            list.Children.Add(box);
        }

        var hint = new TextBlock
        {
            Text = Strings.Get("Profile_AppsHint"),
            TextWrapping = TextWrapping.Wrap,
            Style = (Style)Application.Current.Resources["SecondaryCaptionTextStyle"],
            Visibility = candidates.Count == 0 ? Visibility.Visible : Visibility.Collapsed,
        };

        await new NeapDialog
        {
            XamlRoot = XamlRoot,
            Title = Strings.Format("Profile_AppsTitle", profile.Name),
            Content = new StackPanel { MinWidth = 260, Spacing = 8, Children = { list, hint } },
            CloseButtonText = Strings.Get("Dialog_OK"),
        }.ShowAsync();
    }

    private string Named(string process) => _names.GetValueOrDefault(process, process);

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
