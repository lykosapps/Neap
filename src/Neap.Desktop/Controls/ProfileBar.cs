using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Neap.Core.Connection;
using Neap.Core.Profiles;

namespace Neap.Desktop.Controls;

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
    private const double Gap = 8;

    private readonly TextBlock _name = new()
    {
        Classes = { "caption" },
        TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
        TextWrapping = Avalonia.Media.TextWrapping.NoWrap,
    };

    private readonly Border _editedPill;
    private readonly DropDownButton _face;
    private readonly Button _saveButton;
    private readonly Button _discardButton;
    private readonly Grid _editRow;
    private readonly ListBox _list = new();
    private readonly ProgressBar _working = new() { Width = 40, MinWidth = 0, MinHeight = 0, Height = 4, IsIndeterminate = true, IsVisible = false, VerticalAlignment = VerticalAlignment.Center };

    private readonly TextBlock _notice = new()
    {
        MaxWidth = 260,
        Classes = { "caption", "secondary" },
        IsVisible = false,
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
        TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
        TextWrapping = Avalonia.Media.TextWrapping.NoWrap,
        Classes = { "caption", "secondary" },
        IsVisible = false,
    };

    private readonly Flyout _flyout;
    private bool _building;
    private bool _busy;

    public ProfileBar()
    {
        var editedWord = new TextBlock { Text = Strings.Get("Profile_Edited") };
        AutomationProperties.SetAutomationId(_working, "profile_working");
        AutomationProperties.SetName(_working, Strings.Get("Profile_Working"));
        _editedPill = new Border { Classes = { "tag" }, Child = editedWord, IsVisible = false };

        AutomationProperties.SetName(_list, Strings.Get("Profile_Bar"));
        _new.Content = Strings.Get("Profile_New");

        // Save always leads here: Discard is the one time it isn't the
        // likely choice, and this is the only place either button shows.
        _saveButton = new Button
        {
            Content = Strings.Get("Profile_Save"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Classes = { "accent" },
        };
        _discardButton = new Button
        {
            Content = Strings.Get("Profile_Discard"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };

        // Save and Discard share the row evenly, rather than sitting at
        // their own width with empty space beside them.
        _editRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 8, IsVisible = false };
        Grid.SetColumn(_discardButton, 1);
        _editRow.Children.Add(_saveButton);
        _editRow.Children.Add(_discardButton);

        // Everything that acts on the current settings, above the list of
        // saved profiles to switch to: Save and Discard answer the Edited
        // mark on the face when there's something to answer; New profile is
        // always here, at a stable spot the list below can't push down.
        var actionsSection = new Border
        {
            Padding = new Thickness(0, 0, 0, 8),
            Classes = { "underlined" },
            Child = new StackPanel { Spacing = 8, Children = { _editRow, _new } },
        };

        _flyout = new Flyout
        {
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            Content = new StackPanel
            {
                MinWidth = 220,
                Spacing = 4,
                Children = { _notice, actionsSection, _list },
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
            await SaveCurrentAsNew();
        };

        _list.SelectionChanged += async (_, _) =>
        {
            if (_building) return;
            if (_list.SelectedItem is not ListBoxItem { Tag: Profile chosen }) return;
            _flyout.Hide();
            await ApplyChosen(chosen);
        };

        // A grid, so the name shortens within whatever width Fit gives it.
        var faceContent = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 8 };
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
            _face.Focus();
        };

        _discardButton.Click += async (_, _) =>
        {
            if (_busy) return;
            _flyout.Hide();
            _busy = true;
            Paint();
            try { await AppServices.Profiles.Discard(); }
            catch (HeadsetUnavailableException ex) { await Complain(Strings.Get("Profile_CouldNotSwitchTitle"), ex.Message); }
            finally
            {
                _busy = false;
                Paint();
                _face.Focus();
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
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Profiles.Changed += Paint;
        AppServices.AutoSwitch.Changed += Paint;
        AppServices.Headset.Changed += OnHeadsetChanged;
        AppServices.Headset.StatusChanged += OnStatusChanged;
        Paint();
        await AppServices.Profiles.Refresh();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Profiles.Changed -= Paint;
        AppServices.AutoSwitch.Changed -= Paint;
        AppServices.Headset.Changed -= OnHeadsetChanged;
        AppServices.Headset.StatusChanged -= OnStatusChanged;
    }

    private async void OnHeadsetChanged() => await AppServices.Profiles.Refresh();

    private async void OnStatusChanged(HeadsetStatus status) => await AppServices.Profiles.Refresh();

    private void Paint()
    {
        var active = AppServices.Profiles.Active;
        bool edited = AppServices.Profiles.IsEdited;
        _name.Text = active?.Name ?? Strings.Get("Profile_None");
        _editedPill.IsVisible = edited;
        _editRow.IsVisible = edited && active is not null;
        AutomationProperties.SetItemStatus(_face, edited ? $"{_name.Text}, {Strings.Get("Profile_Edited")}" : _name.Text);

        var ready = AppServices.Profiles.Ready;
        _working.IsVisible = _busy || ready == ProfileReadiness.Reading;
        _list.IsEnabled = _new.IsEnabled = ready == ProfileReadiness.Ready && !_busy;
        _notice.Text = ready switch
        {
            ProfileReadiness.HeadsetNotAnswering => Strings.Get("Profile_NeedsHeadset"),
            ProfileReadiness.Reading => Strings.Get("Profile_Reading"),
            _ => "",
        };
        _notice.IsVisible = ready != ProfileReadiness.Ready;

        var waiting = AppServices.AutoSwitch.Waiting;
        _waiting.Text = waiting is null ? "" : Strings.Format("Profile_Waiting", waiting.Name);
        _waiting.IsVisible = waiting is not null;
        Fit();
    }

    /// <summary>Gives the name whatever width the waiting line leaves.</summary>
    private void Fit()
    {
        double others = 0;
        if (_waiting.IsVisible)
        {
            _waiting.Measure(Size.Infinity);
            others = _waiting.DesiredSize.Width + Gap;
        }
        if (Bounds.Width > 0) _face.MaxWidth = Math.Max(96, Bounds.Width - others);
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
                var item = new ListBoxItem { Content = profile.Name, Tag = profile };
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
                            new Border { Classes = { "tag" }, Child = new TextBlock { Text = tag } },
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
                Heading = Strings.Get("Profile_SwitchTitle"),
                Body = new TextBlock { Text = Strings.Format("Profile_SwitchUnsaved", leaving.Name) },
                PrimaryButtonText = Strings.Get("Profile_SaveAndSwitch"),
                SecondaryButtonText = Strings.Get("Profile_SwitchAnyway"),
                CloseButtonText = Strings.Get("Dialog_Cancel"),
            }.Ask(this);
            if (answer == DialogAnswer.None) return;
            if (answer == DialogAnswer.Primary) AppServices.Profiles.SaveOverActive();
        }

        _busy = true;
        Paint();
        IReadOnlyList<string> missing = [];
        try { missing = await AppServices.Profiles.Apply(chosen); }
        catch (HeadsetUnavailableException ex)
        {
            await Complain(Strings.Get("Profile_CouldNotSwitchTitle"), ex.Message);
        }
        finally
        {
            _busy = false;
            Paint();
        }
        if (missing.Count > 0)
            await Complain(Strings.Get("Profile_PartiallyAppliedTitle"),
                Strings.Format("Profile_MissingPresets", Strings.List(missing)));
    }

    private async Task SaveCurrentAsNew()
    {
        string name = await ProfileNaming.Ask(this, Strings.Get("Profile_NewTitle"), "", excludingId: null);
        if (name.Length == 0) return;
        if (AppServices.Profiles.SaveNew(name) is { } trouble)
            await Complain(Strings.Get("Profile_CouldNotSaveTitle"), trouble);
    }

    private async Task Complain(string title, string message) => await new NeapDialog
    {
        Heading = title,
        Body = new TextBlock { Text = message },
        CloseButtonText = Strings.Get("Dialog_OK"),
    }.ShowAsync(this);
}
