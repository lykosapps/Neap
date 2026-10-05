using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Neap.App.Services;
using Neap.Core.Updates;

namespace Neap.App.Controls;

/// <summary>
/// The banner under the profile bar that says a newer version of Neap is out.
/// </summary>
/// <remarks>
/// <para>
/// Above every page, because an update is about the app, not the page, and
/// someone who has stopped looking at Settings still wants to be told. It
/// leads with the one thing to do, Update and restart, or Download where Neap
/// can't update itself. Remind me tomorrow and Skip this version are
/// beside it, written out rather than hidden behind a close button that
/// wouldn't say which it was.
/// </para>
/// <para>
/// Whether it is on show is <see cref="UpdateReminder"/>'s decision. While an
/// update runs, its progress is written in the banner, and its buttons stay
/// where they are, pressing them doing nothing, so keyboard focus is not
/// thrown on to the next control.
/// </para>
/// </remarks>
public sealed partial class UpdateBar : UserControl
{
    private UpdateStage? _painted;

    public UpdateBar()
    {
        InitializeComponent();

        Notes.Click += async (_, _) => await UpdateDialogs.ShowNotes(XamlRoot);
        Act.Click += async (_, _) => await Update();
        Later.Click += (_, _) => AppServices.Updates.RemindLater();
        Skip.Click += (_, _) => AppServices.Updates.SkipThisVersion();

        Loaded += (_, _) =>
        {
            AppServices.Updates.Changed += Paint;
            _painted = null;
            Paint();
        };
        Unloaded += (_, _) => AppServices.Updates.Changed -= Paint;
    }

    private static async Task Update()
    {
        var updates = AppServices.Updates;
        var look = UpdateLook.Of(updates.Shown);
        if (look.Busy) return;
        if (look.Download && updates.Newer is { } release)
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

        var look = UpdateLook.Of(updates.Shown);
        Message.Text = UpdateCopy.Of(updates, forBanner: true) ?? "";
        Act.Content = Strings.Get(look.Download ? "Settings_UpdateDownload" : "Settings_UpdateInstall");
        Notes.Visibility = look.Notes ? Visibility.Visible : Visibility.Collapsed;
        Visibility = Visibility.Visible;

        // Said when the banner appears or its stage changes, not as the
        // download's percentage ticks over.
        bool moved = _painted != updates.Shown;
        _painted = updates.Shown;
        if (moved)
            FrameworkElementAutomationPeer.CreatePeerForElement(Message)?
                .RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
}
