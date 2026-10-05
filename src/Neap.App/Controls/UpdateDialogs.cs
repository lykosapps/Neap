using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Neap.App.Services;
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

        var look = UpdateLook.Of(updates.Stage);
        var dialog = new NeapDialog
        {
            XamlRoot = root,
            Title = Strings.Format("Notes_Title", AppInfo.Name, release.Version.ToString(3)),
            Content = new ScrollViewer
            {
                Content = body,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            },
            CloseButtonText = Strings.Get("Dialog_Close"),
        };
        if (!look.Busy)
        {
            dialog.PrimaryButtonText = Strings.Get(look.Download ? "Settings_UpdateDownload" : "Settings_UpdateInstall");
            dialog.DefaultButton = ContentDialogButton.Primary;
        }

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        if (look.Download) await OpenPage(release);
        else await updates.Update();
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
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetAccessibilityView(
                    dot, Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Raw);
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
