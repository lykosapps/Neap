using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Neap.Core.Settings;

namespace Neap.Desktop.Controls;

/// <summary>
/// One on-or-off headset setting as a tile, the same tile Home uses, with the
/// settings that go with it laid out under it while it is on.
/// </summary>
/// <remarks>
/// <para>
/// The tile fills with the accent while the setting is on. What goes with it,
/// such as how strong it is, matters only then, so it is shown only then,
/// in a drawer under the tile.
/// </para>
/// <para>
/// Disabled, with a dash for its state, until the headset reports the
/// setting, because an unlit tile would read as off. Its automation id is the
/// setting's registry name, so a script can find it by the setting it writes.
/// </para>
/// </remarks>
public partial class SettingTile : UserControl
{
    public static readonly StyledProperty<string> SettingProperty =
        AvaloniaProperty.Register<SettingTile, string>(nameof(Setting), "");

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<SettingTile, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<SettingTile, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<SettingTile, string?>(nameof(Description));

    public static readonly StyledProperty<Control?> InsideProperty =
        AvaloniaProperty.Register<SettingTile, Control?>(nameof(Inside));

    /// <summary>The tile's own corner radius, so its bottom can be squared to meet the drawer.</summary>
    private static readonly CornerRadius TileRadius = new(16);
    private static readonly CornerRadius TileRadiusOverDrawer = new(16, 16, 0, 0);

    private readonly SettingLink _link;

    public SettingTile()
    {
        InitializeComponent();
        StateWord.Text = Strings.Get("Reading_None");
        InsideHost.CornerRadius = new CornerRadius(0, 0, 16, 16);
        _link = new SettingLink(Face, () => Setting, Build, Paint);
        Face.Click += (_, _) => _link.Write(Face.IsChecked == true ? 1 : 0);
    }

    /// <summary>Gets or sets the setting's registry name, such as "anc".</summary>
    public string Setting
    {
        get => GetValue(SettingProperty);
        set => SetValue(SettingProperty, value);
    }

    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Gets or sets what the setting is called, on the tile and to a screen reader.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Gets or sets one line under the title saying what the setting does.</summary>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>Gets or sets what is shown under the tile while the setting is on.</summary>
    public Control? Inside
    {
        get => GetValue(InsideProperty);
        set => SetValue(InsideProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty) Mark.Data = Icon;
        else if (change.Property == DescriptionProperty) DescriptionText.Text = Description;
        else if (change.Property == InsideProperty) InsideHost.Child = Inside;
        else if (change.Property == TitleProperty)
        {
            TitleText.Text = Title;
            if (Title is not null) AutomationProperties.SetName(Face, Title);
        }
    }

    private void Build(SettingKey key) => AutomationProperties.SetAutomationId(Face, key.Name);

    private void Paint()
    {
        int? value = _link.Value;
        Face.IsEnabled = value is not null && _link.CanWrite;
        Face.IsChecked = value == 1;
        StateWord.Text = QuickSettings.SettingWord(value);
        bool open = value == 1 && Inside is not null;
        InsideHost.IsVisible = open;
        Face.CornerRadius = open ? TileRadiusOverDrawer : TileRadius;
    }
}
