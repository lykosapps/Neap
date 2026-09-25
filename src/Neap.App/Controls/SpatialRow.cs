using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Neap.App.Services;
using Neap.Core.Audio;

namespace Neap.App.Controls;

/// <summary>
/// A settings row for Windows' spatial sound on the headset: off, Windows
/// Sonic, or whichever of Dolby Atmos and DTS this PC has installed.
/// </summary>
/// <remarks>
/// <para>
/// Only what Windows says the headset supports is offered, and the row is
/// re-read after every change rather than trusting it took.
/// </para>
/// <para>
/// Dolby Atmos and DTS each need a licence Windows does not report until the
/// format is chosen. When it is missing, a dialog names the app that
/// activates it and opens that app's Store page, which starts it when it is
/// installed. The Stealth Pro II carries a Dolby Atmos licence, which Dolby
/// Access activates only with the headset's Dolby Atmos driver installed,
/// and Swarm II is what installs it; the row and the dialog say so.
/// </para>
/// </remarks>
public sealed class SpatialRow : SettingsCard
{
    private const string DolbyAccess = "ms-windows-store://pdp/?productid=9N0866FS04W8";
    private const string DtsSoundUnbound = "ms-windows-store://pdp/?productid=9PJ0NKL8MCSJ";

    private readonly ComboBox _picker = new() { MinWidth = 240 };
    private string? _description;
    private bool _painting;

    public SpatialRow()
    {
        HeaderIcon = new FontIcon { Glyph = "" };
        Content = _picker;
        _picker.SelectionChanged += async (_, _) => await Apply();
        Loaded += async (_, _) =>
        {
            _description ??= Description?.ToString();
            AutomationProperties.SetName(_picker, Header?.ToString() ?? "");
            await Load();
        };
    }

    private async Task Load()
    {
        var panel = await SpatialAudio.Read();
        _painting = true;
        try
        {
            var shown = _picker.Items.Cast<ComboBoxItem>().Select(item => (SpatialFormat)item.Tag).ToList();
            if (!shown.SequenceEqual(panel.Offered))
            {
                _picker.Items.Clear();
                foreach (var format in panel.Offered)
                    _picker.Items.Add(new ComboBoxItem { Content = Label(format), Tag = format });
            }
            _picker.SelectedItem = _picker.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => panel.Active is { } active && (SpatialFormat)item.Tag == active);
            _picker.IsEnabled = panel.Offered.Count > 1;
            // Dolby Atmos is offered only once the headset's Dolby driver is
            // installed, which Swarm II does; without it, say what it takes.
            Description = panel.Trouble
                ?? (panel.Offered.Count > 0 && !panel.Offered.Contains(SpatialFormat.DolbyAtmos)
                    ? Strings.Get("Spatial_DolbyNeedsDriver") : _description) ?? "";
        }
        finally { _painting = false; }
    }

    private async Task Apply()
    {
        if (_painting || _picker.SelectedItem is not ComboBoxItem { Tag: SpatialFormat wanted }) return;
        _picker.IsEnabled = false;
        var result = await SpatialAudio.Apply(wanted);
        switch (SpatialSound.NoteFor(result))
        {
            case SpatialNote.NeedsLicence:
                await AskToActivate(wanted);
                break;
            case SpatialNote.Refused:
                AppLog.Write($"spatial sound: Windows refused {wanted}: {result}");
                await Complain(wanted);
                break;
        }
        await Load();
    }

    private async Task AskToActivate(SpatialFormat format)
    {
        bool dolby = format == SpatialFormat.DolbyAtmos;
        string app = Strings.Get(dolby ? "Spatial_DolbyApp" : "Spatial_DtsApp");
        var answer = await new NeapDialog
        {
            XamlRoot = XamlRoot,
            Title = Strings.Format("Spatial_NotActivatedTitle", Label(format)),
            Content = dolby ? Strings.Get("Spatial_DolbyNotActivated") : Strings.Format("Spatial_NotActivated", app),
            PrimaryButtonText = Strings.Format("Spatial_OpenApp", app),
            CloseButtonText = Strings.Get("Dialog_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        }.ShowAsync();
        if (answer == ContentDialogResult.Primary)
            await Windows.System.Launcher.LaunchUriAsync(new Uri(dolby ? DolbyAccess : DtsSoundUnbound));
    }

    private async Task Complain(SpatialFormat format) => await new NeapDialog
    {
        XamlRoot = XamlRoot,
        Title = Strings.Get("Spatial_CouldNotChange"),
        Content = Strings.Format("Spatial_Refused", Label(format)),
        CloseButtonText = Strings.Get("Dialog_OK"),
    }.ShowAsync();

    private static string Label(SpatialFormat format) => format switch
    {
        SpatialFormat.WindowsSonic => Strings.Get("Spatial_WindowsSonic"),
        SpatialFormat.DolbyAtmos => Strings.Get("Spatial_DolbyAtmos"),
        SpatialFormat.DtsHeadphoneX => Strings.Get("Spatial_DtsHeadphoneX"),
        SpatialFormat.DtsXUltra => Strings.Get("Spatial_DtsXUltra"),
        _ => Strings.Get("Spatial_Off"),
    };
}
