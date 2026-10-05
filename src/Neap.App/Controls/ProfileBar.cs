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
/// headset no longer matches it, the way to switch, save or discard, and the
/// profile an app asked for while that switch waits.
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
/// <para>
/// Save and Discard live in the menu, not as buttons sitting permanently in
/// the bar: the "Edited" mark alone is enough to notice while tuning, and a
/// person who is only trying things does not need to be asked on every
/// change whether to keep it. See DECISIONS.md.
/// </para>
/// <para>
/// New profile sits above the list of saved ones, not below: it answers the
/// current settings, not a choice from the list, and its spot shouldn't
/// depend on how many profiles happen to be saved.
/// </para>
/// </remarks>
public sealed class ProfileBar : UserControl
{
    private readonly TextBlock _name = new() { Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"] };
    private readonly TextBlock _editedWord = new() { Style = (Style)Application.Current.Resources["NeapTagTextStyle"] };
    private readonly Border _editedPill;
    private readonly DropDownButton _face;
    private readonly Button _saveButton;
    private readonly Button _discardButton;
    private readonly Grid _editRow;
    private readonly ListView _list = new() { SelectionMode = ListViewSelectionMode.Single };
    private readonly ProgressRing _working = new() { Width = 16, Height = 16, IsActive = false, Visibility = Visibility.Collapsed };
    private readonly TextBlock _notice = new()
    {
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = 260,
        Style = (Style)Application.Current.Resources["SecondaryCaptionTextStyle"],
        Visibility = Visibility.Collapsed,
    };
    private readonly Button _new = new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        HorizontalContentAlignment = HorizontalAlignment.Left,
    };
    private readonly TextBlock _waiting = new()
    {
        MaxWidth = 240,
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
        TextWrapping = TextWrapping.NoWrap,
        Style = (Style)Application.Current.Resources["SecondaryCaptionTextStyle"],
        Visibility = Visibility.Collapsed,
    };
    private readonly Border _actionsSection;
    private readonly Flyout _flyout;
    private bool _building;
    private bool _busy;

    public ProfileBar()
    {
        _editedWord.Text = Strings.Get("Profile_Edited");
        _name.TextTrimming = TextTrimming.CharacterEllipsis;
        _name.TextWrapping = TextWrapping.NoWrap;
        AutomationProperties.SetAutomationId(_working, "profile_working");
        AutomationProperties.SetName(_working, Strings.Get("Profile_Working"));
        _editedPill = new Border
        {
            Style = (Style)Application.Current.Resources["NeapTagStyle"],
            Child = _editedWord,
            Visibility = Visibility.Collapsed,
        };

        AutomationProperties.SetName(_list, Strings.Get("Profile_Bar"));

        _new.Content = Strings.Get("Profile_New");

        _saveButton = new Button
        {
            Content = Strings.Get("Profile_Save"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            // Save always leads here: Discard is the one time it isn't the
            // likely choice, and this is the only place either button shows.
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
        };
        _discardButton = new Button
        {
            Content = Strings.Get("Profile_Discard"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };

        // Save and Discard share the row evenly, rather than sitting at
        // their own width with empty space beside them.
        _editRow = new Grid { ColumnSpacing = 8, Visibility = Visibility.Collapsed };
        _editRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _editRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(_discardButton, 1);
        _editRow.Children.Add(_saveButton);
        _editRow.Children.Add(_discardButton);

        // Everything that acts on the current settings, above the list of
        // saved profiles to switch to: Save and Discard answer the Edited
        // mark on the face when there's something to answer; New profile is
        // always here, at a stable spot the list below can't push down.
        _actionsSection = new Border
        {
            Padding = new Thickness(0, 0, 0, 8),
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["NeapPanelStrokeBrush"],
            Child = new StackPanel { Spacing = 8, Children = { _editRow, _new } },
        };

        _flyout = new Flyout
        {
            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
            Content = new StackPanel
            {
                MinWidth = 220,
                Spacing = 4,
                Children = { _notice, _actionsSection, _list },
            },
        };
        _flyout.Opened += (_, _) =>
        {
            BuildList();
            Paint();
        };

        _new.Click += async (_, _) =>
        {
            _flyout.Hide();
            await ProfileDialogs.SaveCurrentAsNew(XamlRoot);
        };

        _list.SelectionChanged += async (_, _) =>
        {
            if (_building) return;
            if (_list.SelectedItem is not ListViewItem { Tag: Profile chosen }) return;
            _flyout.Hide();
            await ApplyChosen(chosen);
        };

        // A grid, so the name shortens within whatever width Fit gives it.
        var faceContent = new Grid { ColumnSpacing = 8 };
        faceContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        faceContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        faceContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_editedPill, 1);
        Grid.SetColumn(_working, 2);
        faceContent.Children.Add(_name);
        faceContent.Children.Add(_editedPill);
        faceContent.Children.Add(_working);

        // A DropDownButton, not a plain Button: its built-in chevron is the
        // only hint, in the empty "No profile" state, that this is a menu
        // and not a status label.
        _face = new DropDownButton { Flyout = _flyout, Content = faceContent, HorizontalAlignment = HorizontalAlignment.Left };
        AutomationProperties.SetName(_face, Strings.Get("Profile_Bar"));

        _saveButton.Click += (_, _) =>
        {
            _flyout.Hide();
            AppServices.Profiles.SaveOverActive();
            Paint();
            // The menu is already closing; keep focus on the face rather
            // than let it fall back to whatever is next in tab order.
            _face.Focus(FocusState.Programmatic);
        };

        _discardButton.Click += async (_, _) =>
        {
            if (_busy) return;
            _flyout.Hide();
            _busy = true;
            Paint();
            try { await AppServices.Profiles.Discard(); }
            catch (HeadsetUnavailableException ex) { await ProfileDialogs.Complain(XamlRoot, Strings.Get("Profile_CouldNotSwitchTitle"), ex.Message); }
            finally
            {
                _busy = false;
                Paint();
                _face.Focus(FocusState.Programmatic);
            }
        };

        // What is waiting sits beside the name it belongs to; the name
        // shortens only when the window has no more room (Fit).
        AutomationProperties.SetAutomationId(_waiting, "profile_waiting");
        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Gap,
            Children = { _face, _waiting },
        };
        SizeChanged += (_, _) => Fit();

        Loaded += async (_, _) =>
        {
            AppServices.Profiles.Changed += Paint;
            AppServices.AutoSwitch.Changed += Paint;
            AppServices.Headset.Changed += OnHeadsetChanged;
            AppServices.Headset.StatusChanged += OnStatusChanged;
            Paint();
            await AppServices.Profiles.Refresh();
        };
        Unloaded += (_, _) =>
        {
            AppServices.Profiles.Changed -= Paint;
            AppServices.AutoSwitch.Changed -= Paint;
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
        _editRow.Visibility = offerActions ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetItemStatus(_face, edited ? $"{_name.Text}, {_editedWord.Text}" : _name.Text);
        // Only when it changes: setting a tooltip on the button its list hangs from closes the list.
        if (ToolTipService.GetToolTip(_face) as string != _name.Text) ToolTipService.SetToolTip(_face, _name.Text);

        var ready = AppServices.Profiles.Ready;
        _working.IsActive = _busy || ready == ProfileReadiness.Reading;
        _working.Visibility = _working.IsActive ? Visibility.Visible : Visibility.Collapsed;
        _list.IsEnabled = _new.IsEnabled = ready == ProfileReadiness.Ready && !_busy;
        _notice.Text = ready switch
        {
            ProfileReadiness.HeadsetNotAnswering => Strings.Get("Profile_NeedsHeadset"),
            ProfileReadiness.Reading => Strings.Get("Profile_Reading"),
            _ => "",
        };
        _notice.Visibility = ready == ProfileReadiness.Ready ? Visibility.Collapsed : Visibility.Visible;

        var waiting = AppServices.AutoSwitch.Waiting;
        _waiting.Text = waiting is null ? "" : Strings.Format("Profile_Waiting", waiting.Name);
        _waiting.Visibility = waiting is null ? Visibility.Collapsed : Visibility.Visible;
        Fit();
    }

    private const double Gap = 8;

    /// <summary>Gives the name whatever width the waiting line leaves.</summary>
    private void Fit()
    {
        double others = 0;
        if (_waiting.Visibility == Visibility.Visible)
        {
            _waiting.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            others = _waiting.DesiredSize.Width + Gap;
        }
        if (ActualWidth > 0) _face.MaxWidth = Math.Max(96, ActualWidth - others);
    }

    private void BuildList()
    {
        _building = true;
        try
        {
            _list.Items.Clear();
            string? activeId = AppServices.Profiles.ActiveId;
            string? defaultId = AppServices.Profiles.DefaultId;
            foreach (var profile in AppServices.Profiles.All)
            {
                var item = new ListViewItem { Content = profile.Name, Tag = profile };
                AutomationProperties.SetName(item, profile.Name);
                if (profile.Id == defaultId)
                {
                    string tag = Strings.Get("Profile_DefaultTag");
                    item.Content = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock { Text = profile.Name, VerticalAlignment = VerticalAlignment.Center },
                            new Border
                            {
                                Style = (Style)Application.Current.Resources["NeapTagStyle"],
                                Child = new TextBlock { Text = tag, Style = (Style)Application.Current.Resources["NeapTagTextStyle"] },
                            },
                        },
                    };
                    AutomationProperties.SetItemStatus(item, tag);
                }
                _list.Items.Add(item);
                if (profile.Id == activeId) _list.SelectedItem = item;
            }
        }
        finally { _building = false; }
    }

    private async Task ApplyChosen(Profile chosen)
    {
        if (_busy) return;

        // Unsaved changes are the one thing switching loses for good.
        if (AppServices.Profiles.IsEdited && AppServices.Profiles.Active is { } leaving)
        {
            var answer = await new NeapDialog
            {
                XamlRoot = XamlRoot,
                Title = Strings.Get("Profile_SwitchTitle"),
                Content = Strings.Format("Profile_SwitchUnsaved", leaving.Name),
                PrimaryButtonText = Strings.Get("Profile_SaveAndSwitch"),
                SecondaryButtonText = Strings.Get("Profile_SwitchAnyway"),
                CloseButtonText = Strings.Get("Dialog_Cancel"),
                DefaultButton = ContentDialogButton.Primary,
            }.ShowAsync();
            if (answer == ContentDialogResult.None) return;
            if (answer == ContentDialogResult.Primary) AppServices.Profiles.SaveOverActive();
        }

        _busy = true;
        Paint();
        IReadOnlyList<string> missing = [];
        try { missing = await AppServices.Profiles.Apply(chosen); }
        catch (HeadsetUnavailableException ex)
        {
            await ProfileDialogs.Complain(XamlRoot, Strings.Get("Profile_CouldNotSwitchTitle"), ex.Message);
        }
        finally
        {
            _busy = false;
            Paint();
        }
        if (missing.Count > 0)
            await ProfileDialogs.Complain(XamlRoot, Strings.Get("Profile_PartiallyAppliedTitle"),
                Strings.Format("Profile_MissingPresets", Strings.List(missing)));
    }
}
