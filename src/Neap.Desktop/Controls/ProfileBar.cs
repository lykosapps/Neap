using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Neap.Core.Connection;
using Neap.Core.Profiles;

namespace Neap.Desktop.Controls;

/// <summary>
/// The active profile, at the top of the left menu so it is there on every
/// page: its name, an "Edited" mark while the headset no longer matches it,
/// the way to switch, save or discard, and the profile an app asked for while
/// that switch waits.
/// </summary>
/// <remarks>
/// <para>
/// It takes the menu's width. While the menu shows icons only, it is one
/// profile icon (<see cref="Compact"/>) that opens the same menu, with the
/// profile's name as its name and tooltip.
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
/// depend on how many profiles happen to be saved. The face and the list are
/// a <see cref="Chooser"/>, as every list Neap chooses from is.
/// </para>
/// </remarks>
public sealed class ProfileBar : UserControl
{
    private readonly Border _editedPill;
    private readonly Chooser _face = new(Strings.Get("Profile_Bar"))
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        SmallText = true,
    };

    private readonly StackPanel _faceTags;
    private readonly Button _icon;
    private readonly Ellipse _editedDot;
    private readonly Button _saveButton;
    private readonly Button _discardButton;
    private readonly Grid _editRow;
    private readonly ProgressBar _working = new() { Classes = { "busy" }, IsVisible = false, VerticalAlignment = VerticalAlignment.Center };

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
        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        Classes = { "caption", "secondary" },
        IsVisible = false,
    };

    private bool _busy;
    private bool _compact;

    public ProfileBar()
    {
        var editedWord = new TextBlock { Text = Strings.Get("Profile_Edited") };
        AutomationProperties.SetAutomationId(_working, "profile_working");
        AutomationProperties.SetName(_working, Strings.Get("Profile_Working"));
        _editedPill = new Border { Classes = { "tag" }, Child = editedWord, IsVisible = false };

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
        _editRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), Classes = { "profileface" }, IsVisible = false };
        Grid.SetColumn(_discardButton, 1);
        _editRow.Children.Add(_saveButton);
        _editRow.Children.Add(_discardButton);

        // Everything that acts on the current settings, above the list of
        // saved profiles to switch to: Save and Discard answer the Edited
        // mark on the face when there's something to answer; New profile is
        // always here, at a stable spot the list below can't push down.
        _face.Actions(_notice, _editRow, _new);
        _face.Flyout!.Opened += (_, _) =>
        {
            BuildList();
            Paint();
        };

        _new.Click += async (_, _) => await ProfileDialogs.SaveCurrentAsNew(this);
        _face.Chosen += async chosen => await ApplyChosen((Profile)chosen);
        _face.Deleted += deleted =>
        {
            AppServices.Profiles.Delete(((Profile)deleted).Id);
            BuildList();
        };

        // A chooser, a dropdown button: its chevron is the only hint, in the
        // empty "No profile" state, that this is a menu and not a status label.
        _faceTags = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.S, Children = { _editedPill, _working } };

        // The icon shows the Edited mark as a dot on its corner.
        _editedDot = new Ellipse
        {
            Classes = { "statusdot" },
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 4, 4, 0),
            IsHitTestVisible = false,
            IsVisible = false,
        };
        _editedDot[!Shape.FillProperty] = new DynamicResourceExtension("NeapGameTextBrush");
        double target = (double)Application.Current!.FindResource("NeapNavItemHeight")!;
        _icon = new Button
        {
            Flyout = _face.Flyout,
            Content = new Grid
            {
                Children =
                {
                    new PathIcon { Data = (Avalonia.Media.Geometry)Application.Current!.FindResource("IconProfile")!, Width = 20, Height = 20 },
                    _editedDot,
                },
            },
            Height = target,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            IsVisible = false,
        };

        _saveButton.Click += (_, _) =>
        {
            _face.Close();
            AppServices.Profiles.SaveOverActive();
            Paint();
            // The menu is already closing; keep focus on the face rather
            // than let it fall back to whatever is next in tab order.
            Face.Focus();
        };

        _discardButton.Click += async (_, _) => await Discard();

        // What is waiting sits under the name it belongs to.
        AutomationProperties.SetAutomationId(_waiting, "profile_waiting");
        Content = new StackPanel
        {
            Spacing = Space.XS,
            Children = { _face, _icon, _waiting },
        };
    }

    /// <summary>Gets or sets whether the bar is one icon, for a menu that shows icons only, rather than the profile's name in full.</summary>
    public bool Compact
    {
        get => _compact;
        set
        {
            _compact = value;
            Paint();
        }
    }

    private Button Face => _compact ? _icon : _face;

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Profiles.Changed += Paint;
        AppServices.AutoSwitch.Changed += Paint;
        AppServices.Headset.Changed += Reread;
        AppServices.Headset.StatusChanged += OnStatusChanged;
        // A profile holds the mix on a headset with no chat wheel.
        AppServices.Mix.Changed += Reread;
        // And the parametric equaliser's mode and shape.
        AppServices.Parametric.Changed += Reread;
        Paint();
        await AppServices.Profiles.Refresh();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Profiles.Changed -= Paint;
        AppServices.AutoSwitch.Changed -= Paint;
        AppServices.Headset.Changed -= Reread;
        AppServices.Headset.StatusChanged -= OnStatusChanged;
        AppServices.Mix.Changed -= Reread;
        AppServices.Parametric.Changed -= Reread;
    }

    private async void Reread() => await AppServices.Profiles.Refresh();

    private async void OnStatusChanged(HeadsetStatus status) => await AppServices.Profiles.Refresh();

    private void Paint()
    {
        var active = AppServices.Profiles.Active;
        bool edited = AppServices.Profiles.IsEdited;
        string name = active?.Name ?? Strings.Get("Profile_None");
        _face.Show(name, tag: _faceTags);
        _editedPill.IsVisible = edited;
        _editRow.IsVisible = edited && active is not null;
        _face.IsVisible = !_compact;
        _icon.IsVisible = _compact;
        _editedDot.IsVisible = edited;
        string status = edited ? $"{name}, {Strings.Get("Profile_Edited")}" : name;
        AutomationProperties.SetItemStatus(_face, status);
        AutomationProperties.SetItemStatus(_icon, status);
        AutomationProperties.SetName(_icon, name);

        var ready = AppServices.Profiles.Ready;
        _working.IsVisible = _busy || ready == ProfileReadiness.Reading;
        _face.ListEnabled = _new.IsEnabled = ready == ProfileReadiness.Ready && !_busy;
        _notice.Text = ready switch
        {
            ProfileReadiness.HeadsetNotAnswering => Strings.Get("Profile_NeedsHeadset"),
            ProfileReadiness.Reading => Strings.Get("Profile_Reading"),
            _ => "",
        };
        _notice.IsVisible = ready != ProfileReadiness.Ready;

        var waiting = AppServices.AutoSwitch.Waiting;
        _waiting.Text = waiting is null ? "" : Strings.Format("Profile_Waiting", waiting.Name);
        _waiting.IsVisible = waiting is not null && !_compact;

        // The whole name, where the bar has shortened it, and in the icon what is waiting too. Only when it
        // changes: setting a tip on the button its list hangs from closes the list.
        string tip = _compact && waiting is not null ? $"{name}. {_waiting.Text}" : name;
        foreach (var button in new Button[] { _face, _icon })
            if (ToolTip.GetTip(button) as string != tip) ToolTip.SetTip(button, tip);
    }

    /// <summary>Lists the saved profiles, the active one selected and the default one tagged.</summary>
    private void BuildList()
    {
        string? activeId = AppServices.Profiles.ActiveId;
        string? defaultId = AppServices.Profiles.DefaultId;
        string tag = Strings.Get("Profile_DefaultTag");
        _face.List([.. AppServices.Profiles.All.Select(profile => profile.Id == defaultId
            ? new ChooserItem(profile.Name, profile)
            {
                Chosen = profile.Id == activeId,
                Deletable = true,
                Badge = new Border { Classes = { "tag" }, Child = new TextBlock { Text = tag } },
                Status = tag,
            }
            : new ChooserItem(profile.Name, profile) { Chosen = profile.Id == activeId, Deletable = true })]);
    }

    /// <summary>Puts the active profile back on the headset as it was saved.</summary>
    private async Task Discard()
    {
        if (_busy) return;
        _face.Close();
        _busy = true;
        Paint();
        try { await AppServices.Profiles.Discard(); }
        catch (HeadsetUnavailableException ex) { await ProfileDialogs.Complain(this, Strings.Get("Profile_CouldNotSwitchTitle"), ex.Message); }
        finally
        {
            _busy = false;
            Paint();
            Face.Focus();
        }
    }

    private async Task ApplyChosen(Profile chosen)
    {
        if (_busy) return;

        // The active profile chosen again puts it back as it was, as Discard does.
        if (chosen.Id == AppServices.Profiles.ActiveId)
        {
            if (AppServices.Profiles.IsEdited) await Discard();
            return;
        }

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
            await ProfileDialogs.Complain(this, Strings.Get("Profile_CouldNotSwitchTitle"), ex.Message);
        }
        finally
        {
            _busy = false;
            Paint();
        }
        if (missing.Count > 0)
            await ProfileDialogs.Complain(this, Strings.Get("Profile_PartiallyAppliedTitle"),
                Strings.Format("Profile_MissingPresets", Strings.List(missing)));
    }
}
