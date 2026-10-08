using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Neap.Core.Updates;

namespace Neap.Desktop.Controls;

/// <summary>The dialog that says what is new, in the version on offer or the one running.</summary>
public static class UpdateDialogs
{
    /// <summary>What's new in the version that is running, from the changelog built into it.</summary>
    private static readonly IReadOnlyList<NoteBlock> Installed = ReadInstalled();

    /// <summary>Whether there are notes to show: a newer version's, or the running one's.</summary>
    public static bool HasNotes => UpdateLook.Of(AppServices.Updates.Shown).Notes || Installed.Count > 0;

    /// <summary>Shows what's new: in the newer version while one is on offer, otherwise in the one running.</summary>
    public static async Task ShowWhatsNew(Control over)
    {
        if (UpdateLook.Of(AppServices.Updates.Shown).Notes) await ShowNotes(over);
        else if (Installed.Count > 0 && AppInfo.Version is { } version) await Notes(version, Installed).ShowAsync(over);
    }

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
    public static async Task ShowNotes(Control over)
    {
        var updates = AppServices.Updates;
        if (updates.Newer is not { } release) return;

        var blocks = ReleaseNotes.Of(release.Notes);
        if (blocks.Count == 0)
        {
            await OpenPage(release);
            return;
        }

        var look = UpdateLook.Of(updates.Shown);
        var dialog = Notes(release.Version, blocks);
        if (!look.Busy)
            dialog.PrimaryButtonText = Strings.Get(look.Download ? "Settings_UpdateDownload" : "Settings_UpdateInstall");

        if (!await dialog.ShowAsync(over)) return;
        if (look.Download) await OpenPage(release);
        else await updates.Update();
    }

    /// <summary>The notes for a version in a dialog with only Close; the caller adds the update's button.</summary>
    /// <remarks>
    /// The notes can be longer than the dialog, and a scroll area is not a
    /// stop for the Tab key unless it is made one, so without that the
    /// keyboard could reach only the buttons. It is made one, named for the
    /// dialog, and focus starts on it, so the arrow keys and Page Down read the
    /// notes at once. Enter does not start the update from there: the primary
    /// button is not the default, so an update begins only when its own button
    /// is chosen.
    /// </remarks>
    private static NeapDialog Notes(Version version, IReadOnlyList<NoteBlock> blocks)
    {
        var body = new StackPanel { Spacing = 8 };
        foreach (var block in blocks) body.Children.Add(Draw(block));

        string title = Strings.Format("Notes_Title", AppInfo.Name, version.ToString(3));
        var notes = new ScrollViewer
        {
            Content = body,
            MaxHeight = 420,
            Focusable = true,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        AutomationProperties.SetName(notes, title);

        var dialog = new NeapDialog
        {
            Heading = title,
            Body = notes,
            CloseButtonText = Strings.Get("Dialog_Close"),
        };
        dialog.Opened += (_, _) => notes.Focus();
        return dialog;
    }

    private static IReadOnlyList<NoteBlock> ReadInstalled()
    {
        if (AppInfo.Version is not { } version) return [];
        var blocks = ReleaseNotes.ForVersion(ReleaseNotes.BuiltIn, version);
        if (blocks.Count == 0) AppLog.Write($"updates: no notes for version {version.ToString(3)} are built in, so What's new shows only a newer version's");
        return blocks;
    }

    /// <summary>Says the version an update put in place did not open, and offers the download.</summary>
    /// <remarks>
    /// Shown by the version that did the update, which is the only one still
    /// running; see <see cref="RestartWatch"/>. Nothing here can put the old
    /// version back, so it says what can be done: start Neap again, or
    /// download it again.
    /// </remarks>
    public static async Task ShowDidNotStart(Control over, Release release)
    {
        bool download = await new NeapDialog
        {
            Heading = Strings.Format("Update_DidNotStartTitle", AppInfo.Name),
            Body = new TextBlock { Text = Strings.Format("Update_DidNotStart", release.Version.ToString(3)) },
            PrimaryButtonText = Strings.Get("Settings_UpdateDownload"),
            CloseButtonText = Strings.Get("Dialog_Close"),
        }.ShowAsync(over);
        if (download) await OpenPage(release);
    }

    /// <summary>Opens the release's page in the browser.</summary>
    public static async Task OpenPage(Release release)
    {
        try { await Platform.Current.Open(release.Page); }
        catch (Exception ex) { AppLog.Write($"could not open the release page: {ex.Message}"); }
    }

    private static Control Draw(NoteBlock block)
    {
        switch (block.Kind)
        {
            case NoteKind.Heading:
                var heading = Text(block);
                heading.Classes.Add("bodystrong");
                heading.Margin = new Thickness(0, 8, 0, 0);
                return heading;

            case NoteKind.Bullet:
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 8, Margin = new Thickness(12, 0, 0, 0) };
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

    private static SelectableTextBlock Text(NoteBlock block)
    {
        var text = new SelectableTextBlock();
        foreach (var span in block.Spans)
        {
            if (span.Link is { } link)
            {
                var anchor = new HyperlinkButton { Content = span.Text, Padding = new Thickness(0), NavigateUri = null };
                anchor.Click += async (_, _) =>
                {
                    try { await Platform.Current.Open(link); }
                    catch (Exception ex) { AppLog.Write($"could not open a link in the release notes: {ex.Message}"); }
                };
                text.Inlines!.Add(new InlineUIContainer(anchor));
            }
            else
            {
                var run = new Run(span.Text);
                if (span.Bold) run.FontWeight = FontWeight.SemiBold;
                text.Inlines!.Add(run);
            }
        }
        return text;
    }
}
