using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Neap.Core.Updates;

namespace Neap.App.Controls;

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
/// shows its progress and Update and restart is off; keyboard focus moves to
/// the message rather than being thrown on to whatever control is next.
/// </para>
/// </remarks>
public sealed partial class UpdateBar : UserControl
{
    private UpdateStage? _painted;
    private UpdateBarLook _look;
    private bool _compact;

    public UpdateBar()
    {
        InitializeComponent();

        Notes.Click += OnNotes;
        Act.Click += async (_, _) => await Update();
        NotNow.Flyout = Menu(withNotes: false);
        More.Flyout = Menu(withNotes: true);

        SizeChanged += (_, e) =>
        {
            bool compact = UpdateBarLook.Compact(e.NewSize.Width);
            if (compact == _compact) return;
            _compact = compact;
            Offer();
        };

        Loaded += (_, _) =>
        {
            AppServices.Updates.Changed += Paint;
            _painted = null;
            Paint();
        };
        Unloaded += (_, _) => AppServices.Updates.Changed -= Paint;
    }

    private async void OnNotes(object sender, RoutedEventArgs e) => await UpdateDialogs.ShowNotes(XamlRoot);

    private static void OnRemind(object sender, RoutedEventArgs e) => AppServices.Updates.RemindLater();

    private static void OnSkip(object sender, RoutedEventArgs e) => AppServices.Updates.SkipThisVersion();

    /// <summary>A menu of what the banner offers beside its main button.</summary>
    /// <remarks>
    /// What's new takes the text of its button, so the two can't differ.
    /// </remarks>
    private MenuFlyout Menu(bool withNotes)
    {
        var menu = new MenuFlyout();
        if (withNotes) menu.Items.Add(Item((string)Notes.Content, OnNotes));
        menu.Items.Add(Item(Strings.Get("Banner_UpdateRemind"), OnRemind));
        menu.Items.Add(Item(Strings.Get("Banner_UpdateSkip"), OnSkip));
        return menu;
    }

    private static MenuFlyoutItem Item(string text, RoutedEventHandler click)
    {
        var item = new MenuFlyoutItem { Text = text };
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
            Visibility = Visibility.Collapsed;
            _painted = null;
            return;
        }

        _look = UpdateBarLook.Of(updates.Shown);
        Message.Text = UpdateCopy.Of(updates, forBanner: true) ?? "";
        AutomationProperties.SetName(Status, Message.Text);
        AutomationProperties.SetName(Progress, Message.Text);
        Progress.Visibility = _look.Progress == UpdateProgress.None ? Visibility.Collapsed : Visibility.Visible;
        Progress.IsIndeterminate = _look.Progress == UpdateProgress.Unknown;
        Progress.Value = updates.Percent;
        Act.Content = Strings.Get(UpdateLook.Of(updates.Shown).Download ? "Settings_UpdateDownload" : "Settings_UpdateInstall");
        Visibility = Visibility.Visible;
        Offer();

        // Said when the banner appears or its stage changes, not as the
        // download's percentage ticks over.
        bool moved = _painted != updates.Shown;
        _painted = updates.Shown;
        if (moved)
            FrameworkElementAutomationPeer.CreatePeerForElement(Message)?
                .RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    /// <summary>Shows the buttons or the menu the banner offers at this stage and width.</summary>
    /// <remarks>
    /// Focus on a button that goes away or goes off moves to Update and restart
    /// while that is on, and to the message while it is not, rather than falling
    /// to whatever control is next.
    /// </remarks>
    private void Offer()
    {
        var focused = FocusManager.GetFocusedElement(XamlRoot);

        Act.IsEnabled = _look.ActEnabled;
        Notes.Visibility = Shown(!_compact && _look.Notes);
        NotNow.Visibility = Shown(!_compact && _look.PutAway);
        More.Visibility = Shown(_compact && (_look.Notes || _look.PutAway));
        Status.IsTabStop = _look.Progress != UpdateProgress.None;

        bool lost = focused is ButtonBase button && Actions.Children.Contains(button)
            && (!button.IsEnabled || button.Visibility == Visibility.Collapsed);
        bool returns = ReferenceEquals(focused, Status) && _look.ActEnabled;
        if (lost || returns)
            (_look.ActEnabled ? Act : Status).Focus(FocusState.Programmatic);
    }

    private static Visibility Shown(bool shown) => shown ? Visibility.Visible : Visibility.Collapsed;
}
