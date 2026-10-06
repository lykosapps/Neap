using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Neap.Core.Updates;

namespace Neap.App.Controls;

/// <summary>The dialog that says what is new in the version on offer.</summary>
public static class UpdateDialogs
{
    /// <summary>Shows the newer version's notes, and offers to install it.</summary>
    /// <remarks>
    /// <para>
    /// The notes are the release page's own, which came with the check for a
    /// newer version, so reading them asks GitHub for nothing more. A release
    /// with notes Neap can't read is sent to its page instead.
    /// </para>
    /// <para>
    /// The dialog is a snapshot: it offers what the stage was when it opened,
    /// Update and restart, or Download where Neap can't update itself, and
    /// nothing while an update is already running.
    /// </para>
    /// <para>
    /// The notes can be longer than the dialog, and a scroll area is not a
    /// stop for the Tab key unless it is made one, so without that the
    /// keyboard could reach only the buttons. It is made one, named for the
    /// dialog, and focus starts on it, so the arrow keys and Page Down read the
    /// notes at once. Enter does not start the update from there: there is no
    /// default button, so an update begins only when its own button is chosen.
    /// </para>
    /// </remarks>
    public static async Task ShowNotes(XamlRoot root)
    {
        var updates = AppServices.Updates;
        if (updates.Newer is not { } release) return;

        var blocks = ReleaseNotes.Of(release.Notes);
        if (blocks.Count == 0)
        {
            await OpenPage(release);
            return;
        }

        var body = new StackPanel { Spacing = 8 };
        foreach (var block in blocks) body.Children.Add(Draw(block));

        var look = UpdateLook.Of(updates.Shown);
        string title = Strings.Format("Notes_Title", AppInfo.Name, release.Version.ToString(3));
        var notes = new ScrollViewer
        {
            Content = body,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            IsTabStop = true,
            UseSystemFocusVisuals = true,
        };
        AutomationProperties.SetName(notes, title);

        var dialog = new NeapDialog
        {
            XamlRoot = root,
            Title = title,
            Content = notes,
            CloseButtonText = Strings.Get("Dialog_Close"),
        };
        if (!look.Busy)
            dialog.PrimaryButtonText = Strings.Get(look.Download ? "Settings_UpdateDownload" : "Settings_UpdateInstall");
        dialog.Opened += (_, _) => notes.Focus(FocusState.Programmatic);

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        if (look.Download) await OpenPage(release);
        else await updates.Update();
    }

    /// <summary>Says the version an update put in place did not open, and offers the download.</summary>
    /// <remarks>
    /// Shown by the version that did the update, which is the only one still
    /// running; see <see cref="RestartWatch"/>. Nothing here can put the old
    /// version back, so it says what can be done: start Neap again, or
    /// download it again.
    /// </remarks>
    public static async Task ShowDidNotStart(XamlRoot root, Release release)
    {
        var answer = await new NeapDialog
        {
            XamlRoot = root,
            Title = Strings.Format("Update_DidNotStartTitle", AppInfo.Name),
            Content = Strings.Format("Update_DidNotStart", release.Version.ToString(3)),
            PrimaryButtonText = Strings.Get("Settings_UpdateDownload"),
            CloseButtonText = Strings.Get("Dialog_Close"),
            DefaultButton = ContentDialogButton.Primary,
        }.ShowAsync();
        if (answer == ContentDialogResult.Primary) await OpenPage(release);
    }

    /// <summary>Opens the release's page in the browser.</summary>
    public static async Task OpenPage(Release release)
    {
        try { await Windows.System.Launcher.LaunchUriAsync(release.Page); }
        catch (Exception ex) { AppLog.Write($"could not open the release page: {ex.Message}"); }
    }

    private static UIElement Draw(NoteBlock block)
    {
        switch (block.Kind)
        {
            case NoteKind.Heading:
                var heading = Text(block);
                heading.Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"];
                heading.Margin = new Thickness(0, 8, 0, 0);
                return heading;

            case NoteKind.Bullet:
                var row = new Grid { ColumnSpacing = 8, Margin = new Thickness(12, 0, 0, 0) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var dot = new TextBlock { Text = "•" };
                AutomationProperties.SetAccessibilityView(dot, AccessibilityView.Raw);
                row.Children.Add(dot);
                var item = Text(block);
                Grid.SetColumn(item, 1);
                row.Children.Add(item);
                return row;

            default:
                return Text(block);
        }
    }

    private static TextBlock Text(NoteBlock block)
    {
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
        foreach (var span in block.Spans)
        {
            if (span.Link is { } link)
            {
                var anchor = new Hyperlink { NavigateUri = null };
                anchor.Inlines.Add(new Run { Text = span.Text });
                anchor.Click += async (_, _) =>
                {
                    try { await Windows.System.Launcher.LaunchUriAsync(link); }
                    catch (Exception ex) { AppLog.Write($"could not open a link in the release notes: {ex.Message}"); }
                };
                text.Inlines.Add(anchor);
            }
            else
            {
                var run = new Run { Text = span.Text };
                if (span.Bold) run.FontWeight = FontWeights.SemiBold;
                text.Inlines.Add(run);
            }
        }
        return text;
    }
}
