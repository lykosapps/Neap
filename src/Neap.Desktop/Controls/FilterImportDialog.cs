using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Neap.Core.Presets;

namespace Neap.Desktop.Controls;

/// <summary>
/// The dialog that takes filters in the text format of Equalizer APO and
/// AutoEq, pasted or opened from a file, for the ten parametric bands.
/// </summary>
/// <remarks>
/// It reads the text as it changes and shows, before anything is applied, how
/// many bands it sets, each line it does not use and why, any gain held to
/// the limit, and that a preamp is not used. What it says is
/// <see cref="FilterImportLook"/>'s to decide.
/// </remarks>
internal static class FilterImportDialog
{
    /// <summary>The ten bands an import sets, and the name of the file they came from, if one was opened.</summary>
    /// <param name="Bands">The ten bands.</param>
    /// <param name="Source">The file's name without its extension, or null for pasted text.</param>
    public sealed record Imported(IReadOnlyList<ParametricBand> Bands, string? Source);

    /// <summary>Asks for filters, and gives what to set, or null when the dialog was cancelled.</summary>
    /// <param name="over">The control asking, whose window the dialog opens over.</param>
    public static async Task<Imported?> Ask(Control over)
    {
        var field = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            Height = 160,
            MaxLength = FilterImport.MostCharacters,
            PlaceholderText = Strings.Get("FilterImport_Example"),
        };
        AutomationProperties.SetName(field, Strings.Get("FilterImport_Field"));
        var open = new Button { Content = Strings.Get("FilterImport_OpenFile") };
        var summary = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var notes = new StackPanel { Classes = { "dialogflow" } };
        var skipped = new StackPanel { Classes = { "dialogflow" } };
        var skippedArea = new ScrollViewer { MaxHeight = 140, Content = skipped };

        var dialog = new NeapDialog
        {
            Heading = Strings.Get("FilterImport_Title"),
            Body = new StackPanel { Classes = { "dialogflow" }, Children = { field, open, summary, notes, skippedArea } },
            PrimaryButtonText = Strings.Get("FilterImport_Apply"),
            CloseButtonText = Strings.Get("Dialog_Cancel"),
        };

        var result = FilterImport.Parse(string.Empty);
        string? source = null;

        void Judge()
        {
            result = FilterImport.Parse(field.Text ?? string.Empty);
            var saying = FilterImportLook.Say(result);
            summary.Text = Summary(saying, result.Bands.Count);
            summary.IsVisible = summary.Text.Length > 0;
            dialog.IsPrimaryButtonEnabled = FilterImportLook.CanApply(saying);

            notes.Children.Clear();
            foreach (var held in result.HeldGains)
                notes.Children.Add(Caption(Strings.Format("FilterImport_GainHeld", Db(held.From), Db(held.To))));
            if (result.PreampDb is { } preamp)
                notes.Children.Add(Caption(Strings.Format("FilterImport_PreampNotUsed", Db(preamp))));
            notes.IsVisible = notes.Children.Count > 0;

            skipped.Children.Clear();
            if (result.NotImported.Count > 0) skipped.Children.Add(Caption(Strings.Get("FilterImport_NotUsed")));
            foreach (var line in result.NotImported) skipped.Children.Add(Caption(Reason(line)));
            skippedArea.IsVisible = skipped.Children.Count > 0;
        }

        field.TextChanged += (_, _) => Judge();
        open.Click += async (_, _) =>
        {
            if (await ReadFile(dialog) is not { } file) return;
            field.Text = file.Text;
            source = file.Name;
        };
        Judge();
        dialog.Opened += (_, _) => field.Focus();

        if (!await dialog.ShowAsync(over)) return null;
        AppLog.Write($"parametric: imported {result.Bands.Count} filters; {result.NotImported.Count} lines not used");
        return new Imported(result.ToTen(), source);
    }

    /// <summary>Asks for a text file and reads it, saying on screen when it cannot.</summary>
    /// <returns>The file's text and its name without the extension, or null when none was chosen or it could not be read.</returns>
    private static async Task<(string Text, string Name)?> ReadFile(NeapDialog dialog)
    {
        var picked = await dialog.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(Strings.Get("FilterImport_FileType")) { Patterns = ["*.txt"] }, FilePickerFileTypes.All],
        });
        if (picked.Count == 0) return null;

        try
        {
            await using var stream = await picked[0].OpenReadAsync();
            using var reader = new StreamReader(stream);
            var buffer = new char[FilterImport.MostCharacters + 1];
            int read = await reader.ReadBlockAsync(buffer);
            if (read <= FilterImport.MostCharacters) return (new string(buffer, 0, read), Path.GetFileNameWithoutExtension(picked[0].Name));
            AppLog.Write($"parametric: {picked[0].Name} is too long to be a filter file");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Write($"parametric: could not read {picked[0].Name}: {ex.Message}");
        }

        await ProfileDialogs.Complain(dialog, Strings.Get("FilterImport_Title"), Strings.Format("FilterImport_CannotRead", picked[0].Name));
        return null;
    }

    private static string Summary(ImportSaying saying, int bands) => saying switch
    {
        ImportSaying.NothingFits => Strings.Get("FilterImport_NothingFits"),
        ImportSaying.SetsOne => Strings.Get("FilterImport_SetsOne"),
        ImportSaying.SetsSome => Strings.Format("FilterImport_SetsSome", bands),
        ImportSaying.SetsAll => Strings.Get("FilterImport_SetsAll"),
        _ => string.Empty,
    };

    private static string Reason(SkippedLine line) => line.Reason switch
    {
        SkipReason.SwitchedOff => Strings.Format("FilterImport_SwitchedOff", line.Line),
        SkipReason.TypeNotOnHeadset => Strings.Format("FilterImport_TypeNotOnHeadset", line.Line, line.Type),
        SkipReason.PastTenth => Strings.Format("FilterImport_PastTenth", line.Line),
        _ => Strings.Format("FilterImport_NotUnderstood", line.Line),
    };

    private static TextBlock Caption(string text) =>
        new() { Text = text, TextWrapping = TextWrapping.Wrap, Classes = { "caption", "secondary" } };

    private static string Db(double value) => value.ToString("+0.#;-0.#;0", CultureInfo.CurrentCulture);
}
