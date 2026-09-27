using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Neap.App.Services;
using Neap.Core.Profiles;

namespace Neap.App.Controls;

/// <summary>
/// Every saved profile, for renaming or deleting. Switching is the header
/// bar's job, not this list's; see <see cref="ProfileBar"/>.
/// </summary>
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

    public ProfilesSettings()
    {
        Content = new StackPanel { Spacing = 8, Children = { _rows, _empty } };
        Loaded += (_, _) =>
        {
            AppServices.Profiles.Changed += Paint;
            Paint();
        };
        Unloaded += (_, _) => AppServices.Profiles.Changed -= Paint;
    }

    private void Paint()
    {
        var profiles = AppServices.Profiles.All;
        _empty.Visibility = profiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _rows.Children.Clear();
        foreach (var profile in profiles)
            _rows.Children.Add(_confirmingDeleteId == profile.Id ? ConfirmRow(profile) : Row(profile));
    }

    /// <summary>A profile's row: its name, with a way to rename it and a way to delete it.</summary>
    private UIElement Row(Profile profile)
    {
        var name = new TextBlock
        {
            Text = profile.Name,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

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
        Grid.SetColumn(rename, 1);
        Grid.SetColumn(delete, 2);
        grid.Children.Add(name);
        grid.Children.Add(rename);
        grid.Children.Add(delete);

        return Card(grid);
    }

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
