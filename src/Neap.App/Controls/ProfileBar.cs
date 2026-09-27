using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Neap.App.Services;
using Neap.Core.Connection;
using Neap.Core.Profiles;

namespace Neap.App.Controls;

/// <summary>
/// The active profile, on every page: its name, an "Edited" mark while the
/// headset no longer matches it, and the way to switch, save or discard.
/// </summary>
/// <remarks>
/// <para>
/// Sits in its own row under the title bar rather than inside it: the title
/// bar has room for a dot and a word, and a profile needs more than that. See
/// DECISIONS.md.
/// </para>
/// <para>
/// Naming and deleting a profile are Settings' job, not this bar's; the one
/// exception is saving the current settings as a brand new profile, which
/// belongs wherever the settings just got made, not a page away.
/// </para>
/// </remarks>
public sealed class ProfileBar : UserControl
{
    private readonly TextBlock _name = new() { Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"] };
    private readonly TextBlock _editedWord = new() { Style = (Style)Application.Current.Resources["NeapTagTextStyle"] };
    private readonly Border _editedPill;
    private readonly Button _face;
    private readonly Button _saveButton;
    private readonly Button _discardButton;
    private readonly ListView _list = new() { SelectionMode = ListViewSelectionMode.Single };
    private readonly Flyout _flyout;
    private bool _building;
    private bool _busy;

    public ProfileBar()
    {
        _editedWord.Text = Strings.Get("Profile_Edited");
        _editedPill = new Border
        {
            Style = (Style)Application.Current.Resources["NeapTagStyle"],
            Child = _editedWord,
            Visibility = Visibility.Collapsed,
        };

        AutomationProperties.SetName(_list, Strings.Get("Profile_Bar"));

        var newButton = new Button
        {
            Content = Strings.Get("Profile_New"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };

        _flyout = new Flyout
        {
            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
            Content = new StackPanel
            {
                MinWidth = 220,
                Spacing = 4,
                Children =
                {
                    _list,
                    new Border
                    {
                        Padding = new Thickness(0, 4, 0, 0),
                        BorderThickness = new Thickness(0, 1, 0, 0),
                        BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["NeapPanelStrokeBrush"],
                        Child = newButton,
                    },
                },
            },
        };
        _flyout.Opened += (_, _) => BuildList();

        newButton.Click += async (_, _) =>
        {
            _flyout.Hide();
            await SaveCurrentAsNew();
        };

        _list.SelectionChanged += async (_, _) =>
        {
            if (_building) return;
            if (_list.SelectedItem is not ListViewItem { Tag: Profile chosen }) return;
            _flyout.Hide();
            await ApplyChosen(chosen);
        };

        _face = new Button
        {
            Flyout = _flyout,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children = { _name, _editedPill },
            },
        };
        AutomationProperties.SetName(_face, Strings.Get("Profile_Bar"));

        _saveButton = new Button { Content = Strings.Get("Profile_Save"), Visibility = Visibility.Collapsed };
        _saveButton.Click += (_, _) =>
        {
            AppServices.Profiles.SaveOverActive();
            Paint();
        };

        _discardButton = new Button { Content = Strings.Get("Profile_Discard"), Visibility = Visibility.Collapsed };
        _discardButton.Click += async (_, _) =>
        {
            if (_busy) return;
            _busy = true;
            try { await AppServices.Profiles.Discard(); }
            finally { _busy = false; }
            Paint();
        };

        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { _face, _saveButton, _discardButton },
        };

        Loaded += async (_, _) =>
        {
            AppServices.Profiles.Changed += Paint;
            AppServices.Headset.Changed += OnHeadsetChanged;
            AppServices.Headset.StatusChanged += OnStatusChanged;
            Paint();
            await AppServices.Profiles.Refresh();
        };
        Unloaded += (_, _) =>
        {
            AppServices.Profiles.Changed -= Paint;
            AppServices.Headset.Changed -= OnHeadsetChanged;
            AppServices.Headset.StatusChanged -= OnStatusChanged;
        };
    }

    private async void OnHeadsetChanged() => await AppServices.Profiles.Refresh();

    private async void OnStatusChanged(HeadsetStatus status) => await AppServices.Profiles.Refresh();

    private void Paint()
    {
        var active = AppServices.Profiles.Active;
        bool edited = AppServices.Profiles.IsEdited;
        _name.Text = active?.Name ?? Strings.Get("Profile_None");
        _editedPill.Visibility = edited ? Visibility.Visible : Visibility.Collapsed;
        bool offerActions = edited && active is not null;
        _saveButton.Visibility = offerActions ? Visibility.Visible : Visibility.Collapsed;
        _discardButton.Visibility = offerActions ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetItemStatus(_face, edited ? $"{_name.Text}, {_editedWord.Text}" : _name.Text);
    }

    private void BuildList()
    {
        _building = true;
        try
        {
            _list.Items.Clear();
            string? activeId = AppServices.Profiles.ActiveId;
            foreach (var profile in AppServices.Profiles.All)
            {
                var item = new ListViewItem { Content = profile.Name, Tag = profile };
                _list.Items.Add(item);
                if (profile.Id == activeId) _list.SelectedItem = item;
            }
        }
        finally { _building = false; }
    }

    private async Task ApplyChosen(Profile chosen)
    {
        if (_busy) return;
        _busy = true;
        IReadOnlyList<string> missing;
        try { missing = await AppServices.Profiles.Apply(chosen); }
        finally { _busy = false; }
        if (missing.Count > 0)
            await Complain(Strings.Get("Profile_PartiallyAppliedTitle"),
                Strings.Format("Profile_MissingPresets", Strings.List(missing)));
    }

    private async Task SaveCurrentAsNew()
    {
        string name = await ProfileNaming.Ask(XamlRoot, Strings.Get("Profile_NewTitle"), "", excludingId: null);
        if (name.Length == 0) return;
        if (AppServices.Profiles.SaveNew(name) is { } trouble)
            await Complain(Strings.Get("Profile_CouldNotSaveTitle"), trouble);
    }

    private async Task Complain(string title, string message) => await new NeapDialog
    {
        XamlRoot = XamlRoot,
        Title = title,
        Content = message,
        CloseButtonText = Strings.Get("Dialog_OK"),
    }.ShowAsync();
}
