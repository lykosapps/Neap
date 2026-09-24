using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using StealthPro.App.Services;
using StealthPro.Core.Settings;

namespace StealthPro.App.Controls;

/// <summary>
/// One on-or-off headset setting as a tile, the same tile Home uses, with the
/// settings that go with it laid out under it while it is on.
/// </summary>
/// <remarks>
/// <para>
/// The tile fills with the accent while the setting is on. What goes with it,
/// such as how strong it is, matters only then, so it is shown only then.
/// </para>
/// <para>
/// Disabled, with a dash for its state, until the headset reports the
/// setting, because an unlit tile would read as off. Its automation id is the
/// setting's registry name, so a script can find it by the setting it writes.
/// </para>
/// <para>
/// Pages say which setting, how to describe it and what goes inside:
/// <code>&lt;c:SettingTile Setting="anc" Glyph="&amp;#xE7F6;"&gt; … &lt;/c:SettingTile&gt;</code>
/// </para>
/// </remarks>
[ContentProperty(Name = nameof(Inside))]
public sealed partial class SettingTile : UserControl
{
    private readonly SettingLink _link;

    public SettingTile()
    {
        InitializeComponent();
        _link = new SettingLink(Face, () => Setting, Build, Paint);
        Face.Click += (_, _) => _link.Write(Face.IsChecked == true ? 1 : 0);
    }

    public static readonly DependencyProperty SettingProperty = DependencyProperty.Register(
        nameof(Setting), typeof(string), typeof(SettingTile), new PropertyMetadata(""));

    /// <summary>Gets or sets the setting's registry name, such as "anc".</summary>
    public string Setting
    {
        get => (string)GetValue(SettingProperty);
        set => SetValue(SettingProperty, value);
    }

    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(string), typeof(SettingTile),
        new PropertyMetadata("", (d, e) => ((SettingTile)d).Icon.Glyph = (string)e.NewValue));

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(SettingTile),
        new PropertyMetadata("", (d, e) => ((SettingTile)d).Named((string)e.NewValue)));

    /// <summary>Gets or sets what the setting is called, on the tile and to a screen reader.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(SettingTile),
        new PropertyMetadata("", (d, e) => ((SettingTile)d).DescriptionText.Text = (string)e.NewValue));

    /// <summary>Gets or sets one line under the title saying what the setting does.</summary>
    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public static readonly DependencyProperty InsideProperty = DependencyProperty.Register(
        nameof(Inside), typeof(object), typeof(SettingTile),
        new PropertyMetadata(null, (d, e) => ((SettingTile)d).InsideHost.Content = e.NewValue));

    /// <summary>Gets or sets what is shown under the tile while the setting is on.</summary>
    public object? Inside
    {
        get => GetValue(InsideProperty);
        set => SetValue(InsideProperty, value);
    }

    private void Named(string title)
    {
        TitleText.Text = title;
        AutomationProperties.SetName(Face, title);
    }

    private void Build(SettingKey key) => AutomationProperties.SetAutomationId(Face, key.Name);

    private void Paint()
    {
        int? value = _link.Value;
        Face.IsEnabled = value is not null && _link.Key is { Writable: true };
        Face.IsChecked = value == 1;
        StateWord.Text = value switch
        {
            null => "—",
            1 => Strings.Get("Switch_On"),
            _ => Strings.Get("Switch_Off"),
        };
        InsideHost.Visibility = value == 1 ? Visibility.Visible : Visibility.Collapsed;
    }
}
