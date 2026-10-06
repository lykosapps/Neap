using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Neap.Core.Updates;
using Neap.Desktop.Localization;

namespace Neap.Desktop.Controls;

/// <summary>
/// The banner under the title bar that says a newer version of Neap is out.
/// </summary>
/// <remarks>
/// <para>
/// Above every page, because an update is about the app, not the page, and
/// someone who has stopped looking at Settings still wants to be told. It
/// leads with the one thing to do, Update and restart, or Download where Neap
/// can't update itself. Remind me tomorrow and Skip this version sit together
/// under one Not now menu, written out rather than hidden behind a close
/// button that wouldn't say which it was. In a narrow window What's new and
/// that menu fold into one more menu, so the bar stays one row.
/// </para>
/// <para>
/// Whether it is on show is <see cref="UpdateReminder"/>'s decision, and what
/// it offers is <see cref="UpdateBarLook"/>'s. While an update runs, a bar
/// shows its progress and Update and restart is off. Where there is no release
/// to update to, the bar is never on show.
/// </para>
/// </remarks>
public sealed class UpdateBar : UserControl
{
    private readonly TextBlock _message = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly ProgressBar _progress = new() { Maximum = 100, IsVisible = false };
    private readonly Button _notes = new();
    private readonly Button _act = new() { Classes = { "accent" } };
    private readonly DropDownButton _notNow = new();
    private readonly Button _more = new();
    private readonly StackPanel _actions;
    private UpdateBarLook _look;
    private bool _compact;

    public UpdateBar()
    {
        IsVisible = false;
        _notes.Content = Strings.Get("Settings_UpdateNotes.Content");
        _notNow.Content = Strings.Get("Banner_UpdateNotNow.Content");
        _more.Content = new PathIcon { Width = 16, Height = 16, Data = (Geometry)Application.Current!.FindResource("IconMore")! };
        Uid.SetValue(_more, "Banner_UpdateMore");

        _notes.Click += OnNotes;
        _act.Click += async (_, _) => await Update();
        _notNow.Flyout = Menu(withNotes: false);
        _more.Flyout = Menu(withNotes: true);

        _actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _notes, _act, _notNow, _more },
        };
        Grid.SetColumn(_actions, 2);

        var icon = new PathIcon
        {
            Width = 16,
            Height = 16,
            VerticalAlignment = VerticalAlignment.Center,
            Data = (Geometry)Application.Current!.FindResource("IconUpdate")!,
            Classes = { "accentmark" },
        };
        AutomationProperties.SetAccessibilityView(icon, AccessibilityView.Raw);

        var status = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Children = { _message, _progress } };
        Grid.SetColumn(status, 1);

        // A flat wash of the accent hue, with an accent edge down its left, so
        // the news reads as temporary and not as part of the profile bar
        // beneath it, whose own ground fades from the top. One row at any
        // width: the message wraps, and the buttons give way to a menu when
        // the window is narrow.
        Content = new Grid
        {
            Children =
            {
                new Border
                {
                    Padding = new Thickness(16, 8),
                    Classes = { "update" },
                    Child = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                        ColumnSpacing = 12,
                        Children = { icon, status, _actions },
                    },
                },
                new Border { Width = 4, HorizontalAlignment = HorizontalAlignment.Left, Classes = { "updateedge" } },
            },
        };

        SizeChanged += (_, e) =>
        {
            bool compact = UpdateBarLook.Compact(e.NewSize.Width);
            if (compact == _compact) return;
            _compact = compact;
            Offer();
        };
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Updates.Changed += Paint;
        Paint();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Updates.Changed -= Paint;
    }

    private async void OnNotes(object? sender, RoutedEventArgs e) => await UpdateDialogs.ShowNotes(this);

    private static void OnRemind(object? sender, RoutedEventArgs e) => AppServices.Updates.RemindLater();

    private static void OnSkip(object? sender, RoutedEventArgs e) => AppServices.Updates.SkipThisVersion();

    /// <summary>A menu of what the banner offers beside its main button.</summary>
    /// <remarks>What's new takes the text of its button, so the two can't differ.</remarks>
    private MenuFlyout Menu(bool withNotes)
    {
        var menu = new MenuFlyout();
        if (withNotes) menu.Items.Add(Item((string)_notes.Content!, OnNotes));
        menu.Items.Add(Item(Strings.Get("Banner_UpdateRemind"), OnRemind));
        menu.Items.Add(Item(Strings.Get("Banner_UpdateSkip"), OnSkip));
        return menu;
    }

    private static MenuItem Item(string text, EventHandler<RoutedEventArgs> click)
    {
        var item = new MenuItem { Header = text };
        item.Click += click;
        return item;
    }

    private static async Task Update()
    {
        var updates = AppServices.Updates;
        if (UpdateLook.Of(updates.Shown).Download && updates.Newer is { } release)
        {
            await UpdateDialogs.OpenPage(release);
            return;
        }
        await updates.Update();
    }

    private void Paint()
    {
        var updates = AppServices.Updates;
        if (!updates.ReminderShown)
        {
            IsVisible = false;
            return;
        }

        _look = UpdateBarLook.Of(updates.Shown);
        _message.Text = UpdateCopy.Of(updates, forBanner: true) ?? "";
        AutomationProperties.SetName(_progress, _message.Text);
        _progress.IsVisible = _look.Progress != UpdateProgress.None;
        _progress.IsIndeterminate = _look.Progress == UpdateProgress.Unknown;
        _progress.Value = updates.Percent;
        _act.Content = Strings.Get(UpdateLook.Of(updates.Shown).Download ? "Settings_UpdateDownload" : "Settings_UpdateInstall");
        IsVisible = true;
        Offer();

        // Said when the banner appears or its stage changes.
        AutomationProperties.SetLiveSetting(_message, AutomationLiveSetting.Polite);
    }

    /// <summary>Shows the buttons or the menu the banner offers at this stage and width.</summary>
    private void Offer()
    {
        _act.IsEnabled = _look.ActEnabled;
        _notes.IsVisible = !_compact && _look.Notes;
        _notNow.IsVisible = !_compact && _look.PutAway;
        _more.IsVisible = _compact && (_look.Notes || _look.PutAway);
    }
}
