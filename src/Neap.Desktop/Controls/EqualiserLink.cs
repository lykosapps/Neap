using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Neap.Core.Connection;
using Neap.Core.Presets;
using Neap.Desktop.Views;

namespace Neap.Desktop.Controls;

/// <summary>
/// A row that names an equaliser and the preset it is on, and opens the
/// Equaliser page.
/// </summary>
/// <remarks>
/// <para>
/// The Audio page has no equaliser of its own; this is the way to it from
/// where a person is already adjusting what they hear or what others hear.
/// The preset is read from the headset the first time it is needed, which
/// the Equaliser page and Home share; until then the row names the equaliser
/// alone.
/// </para>
/// <para>
/// The Game link lands on the page's Game tab. The page offers no way yet to
/// ask for its Microphone tab, so the Microphone link opens the page as it
/// was left.
/// </para>
/// </remarks>
public sealed class EqualiserLink : Button
{
    public static readonly StyledProperty<Bank> BankProperty =
        AvaloniaProperty.Register<EqualiserLink, Bank>(nameof(Bank), Core.Presets.Bank.Game);

    private readonly TextBlock _name = new() { Classes = { "secondary" }, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };

    public EqualiserLink()
    {
        Classes.Add("rowlink");
        // Worded and drawn as every other link to a page: the accent, with the links' arrow after it.
        var open = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = Space.XS,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = Strings.Get("Equaliser_Open"), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center },
                new PathIcon
                {
                    Width = (double)Application.Current!.FindResource("NeapIconRow")!,
                    Height = (double)Application.Current!.FindResource("NeapIconRow")!,
                    Data = (Avalonia.Media.Geometry)Application.Current!.FindResource("IconArrowUp")!,
                    RenderTransform = new Avalonia.Media.RotateTransform(90),
                },
            },
        };
        open.Bind(TextElement.ForegroundProperty, open.GetResourceObservable("NeapAccentTextBrush"));
        var line = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = Space.M };
        Grid.SetColumn(open, 1);
        line.Children.Add(_name);
        line.Children.Add(open);
        Content = line;
        Click += (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is not MainWindow window) return;
            window.Open("equaliser");
            if (window.CurrentPage is EqualiserPage page)
            {
                if (Bank == Bank.Game) page.ShowGame();
                else page.ShowMicrophone();
            }
        };
    }

    protected override Type StyleKeyOverride => typeof(Button);

    /// <summary>Gets or sets which equaliser it names: the game's or the microphone's.</summary>
    public Bank Bank
    {
        get => GetValue(BankProperty);
        set => SetValue(BankProperty, value);
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Headset.Changed += Paint;
        Paint();
        if (AppServices.Presets.State(Bank) is not null || AppServices.Headset.Status.Link != Link.Connected) return;
        try { await AppServices.Presets.Load(Bank); }
        catch (Exception ex)
        {
            AppLog.Write($"could not read the {Bank} equaliser's presets for the Audio page: {ex.Message}");
            return;
        }
        Paint();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Headset.Changed -= Paint;
    }

    private void Paint()
    {
        string label = Strings.Get(Bank == Bank.Game ? "Feature_GameEqualiser" : "Feature_MicrophoneEqualiser");
        string name = AppServices.Presets.State(Bank) is null ? "" : AppServices.Presets.CurrentName(Bank);
        string shown = name.Length == 0 ? label : Strings.Format("Equaliser_LinkNamed", label, name);
        _name.Text = shown;
        AutomationProperties.SetName(this, Strings.Format("Equaliser_LinkSpoken", shown));
    }
}
