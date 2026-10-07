using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;

namespace Neap.Desktop;

/// <summary>
/// The look for someone who has asked their system for high contrast: the
/// system's own colours in place of Neap's twilight, so text is as readable as
/// they set it to be.
/// </summary>
/// <remarks>
/// <para>
/// Windows says what its colours are; elsewhere the person has asked for
/// contrast and nothing more, so a plain one is used: white on black, with
/// yellow for what a link would be and cyan for what is chosen.
/// </para>
/// <para>
/// Two variants, one over the light theme and one over the dark, since the
/// ordinary controls beneath Neap's own take their colours from whichever
/// they sit on, and a light window under dark controls would be unreadable.
/// </para>
/// </remarks>
internal static class HighContrast
{
    /// <summary>The high-contrast look over the light theme.</summary>
    public static readonly ThemeVariant OverLight = new("HighContrastLight", ThemeVariant.Light);

    /// <summary>The high-contrast look over the dark theme.</summary>
    public static readonly ThemeVariant OverDark = new("HighContrastDark", ThemeVariant.Dark);

    private const int ColorWindow = 5, ColorHighlight = 13, ColorHighlightText = 14, ColorGrayText = 17, ColorWindowText = 8, ColorHotlight = 26;

    /// <summary>The colours high contrast is drawn in.</summary>
    private sealed record Palette(Color Window, Color Text, Color Gray, Color Highlight, Color HighlightText, Color Link);

    /// <summary>Adds the look to the app, to be chosen by <see cref="Variant"/>.</summary>
    public static void Install(Application app)
    {
        var palette = Read();
        app.Resources.ThemeDictionaries[OverLight] = Dictionary(palette);
        app.Resources.ThemeDictionaries[OverDark] = Dictionary(palette);
    }

    /// <summary>The look to use now, or null when the person has not asked for high contrast.</summary>
    public static ThemeVariant? Variant(Application app)
    {
        bool wanted = Pretend.HighContrast
            || app.PlatformSettings?.GetColorValues().ContrastPreference == ColorContrastPreference.High;
        if (!wanted) return null;
        return Luminance(Read().Window) > 0.5 ? OverLight : OverDark;
    }

    private static Palette Read() => OperatingSystem.IsWindows() && !Pretend.HighContrast
        ? new Palette(System(ColorWindow), System(ColorWindowText), System(ColorGrayText),
            System(ColorHighlight), System(ColorHighlightText), System(ColorHotlight))
        : new Palette(Colors.Black, Colors.White, Color.Parse("#3FF23F"), Color.Parse("#1AEBFF"), Colors.Black, Color.Parse("#FFFF00"));

    private static ResourceDictionary Dictionary(Palette p)
    {
        var dictionary = new ResourceDictionary();
        void Brush(string name, Color color) => dictionary[name] = new SolidColorBrush(color);

        Brush("NeapGroundBrush", p.Window);
        Brush("NeapPanelBrush", p.Window);
        Brush("NeapPanelStrokeBrush", p.Text);
        Brush("NeapNoticeBrush", p.Window);
        Brush("NeapRuleBrush", p.Text);
        Brush("NeapGameBrush", p.Highlight);
        Brush("NeapGameTextBrush", p.Text);
        Brush("NeapChatBrush", p.Text);
        Brush("NeapChatTextBrush", p.Text);
        Brush("NeapDialTrackBrush", p.Gray);
        Brush("NeapMeterUnlitBrush", p.Gray);
        Brush("NeapTextBrush", p.Text);
        Brush("NeapTextSecondaryBrush", p.Text);
        Brush("NeapTextTertiaryBrush", p.Text);
        Brush("NeapTextDisabledBrush", p.Gray);
        Brush("NeapAccentTextBrush", p.Link);

        // Said by their words and shapes, never by colour alone, so the one
        // text colour is enough for all four.
        Brush("NeapToneGoodBrush", p.Text);
        Brush("NeapToneCautionBrush", p.Text);
        Brush("NeapToneNeutralBrush", p.Text);
        Brush("NeapToneCriticalBrush", p.Text);
        Brush("NeapQuietBrush", p.Window);
        Brush("NeapQuietHoverBrush", p.Window);
        Brush("NeapPresetHoverBrush", p.Window);
        Brush("NeapPresetSelectedBrush", p.Window);

        // A setting's tile: plain and outlined, and filled with the system's
        // highlight, text and all, while it is on.
        Brush("NeapTileBrush", p.Window);
        Brush("NeapTileHoverBrush", p.Window);
        Brush("NeapTileStrokeBrush", p.Text);
        Brush("NeapTileEdgeBrush", Colors.Transparent);
        Brush("NeapTileTextBrush", p.Text);
        Brush("NeapTileOnBrush", p.Highlight);
        Brush("NeapTileOnHoverBrush", p.Highlight);
        Brush("NeapTileOnStrokeBrush", p.Text);
        Brush("NeapTileOnEdgeBrush", Colors.Transparent);
        Brush("NeapTileOnTextBrush", p.HighlightText);
        Brush("NeapTileDisabledBrush", p.Window);
        Brush("NeapTileDisabledStrokeBrush", p.Gray);
        Brush("NeapTileDisabledTextBrush", p.Gray);
        Brush("NeapTileOnDisabledBrush", p.Gray);
        Brush("NeapTileOnDisabledTextBrush", p.Window);

        // The page and the rail share the window's colour; the chosen place
        // is marked by its bar, not by a fill.
        Brush("NeapBodyBrush", p.Window);
        Brush("NeapNavSelectedBrush", p.Window);
        Brush("NeapNavHoverBrush", p.Window);

        // Buttons, the lists that open from them and the switch: outlined in
        // the text colour, and the highlight where they are on. These are the
        // names the controls look for, set here because a name that points at
        // another would find the dark theme's.
        foreach (string name in new[]
        {
            "NeapButtonBrush", "NeapButtonHoverBrush", "NeapButtonPressedBrush", "NeapButtonDisabledBrush", "NeapFlyoutBrush",
            "ButtonBackground", "ButtonBackgroundPointerOver", "ButtonBackgroundPressed", "ButtonBackgroundDisabled",
            "ComboBoxBackground", "ComboBoxBackgroundPointerOver", "ComboBoxBackgroundPressed", "ComboBoxBackgroundDisabled",
            "ComboBoxDropDownBackground", "FlyoutPresenterBackground", "MenuFlyoutPresenterBackground",
            "ExpanderHeaderBackground", "ExpanderHeaderBackgroundPointerOver",
        })
            Brush(name, p.Window);
        Brush("ExpanderContentBackground", Colors.Transparent);
        foreach (string name in new[]
        {
            "NeapButtonStrokeBrush", "ButtonBorderBrush", "ButtonBorderBrushPointerOver", "ButtonBorderBrushPressed",
            "ComboBoxBorderBrush", "ComboBoxBorderBrushPointerOver", "ComboBoxBorderBrushPressed", "ComboBoxDropDownBorderBrush",
            "ExpanderHeaderBorderBrush", "ExpanderContentBorderBrush",
        })
            Brush(name, p.Text);
        Brush("ButtonBorderBrushDisabled", p.Gray);
        foreach (string name in new[]
        {
            "NeapAccentFillBrush", "NeapAccentFillHoverBrush", "NeapAccentFillPressedBrush", "AccentButtonBackground",
            "AccentButtonBackgroundPointerOver", "AccentButtonBackgroundPressed", "ToggleSwitchFillOn",
            "AccentFillColorDefaultBrush", "AccentFillColorSecondaryBrush", "AccentFillColorTertiaryBrush",
            "SliderTrackValueFill", "SliderTrackValueFillPointerOver", "SliderTrackValueFillPressed",
            "SliderThumbBackground", "SliderThumbBackgroundPointerOver", "SliderThumbBackgroundPressed",
            "RadioButtonOuterEllipseCheckedFill", "RadioButtonOuterEllipseCheckedFillPointerOver",
            "RadioButtonOuterEllipseCheckedFillPressed", "RadioButtonOuterEllipseCheckedStroke",
            "RadioButtonOuterEllipseCheckedStrokePointerOver", "RadioButtonOuterEllipseCheckedStrokePressed",
            "CheckBoxCheckBackgroundFillChecked", "CheckBoxCheckBackgroundFillCheckedPointerOver",
            "CheckBoxCheckBackgroundFillCheckedPressed", "CheckBoxCheckBackgroundStrokeChecked",
            "CheckBoxCheckBackgroundStrokeCheckedPointerOver", "CheckBoxCheckBackgroundStrokeCheckedPressed",
            "ToggleSwitchFillOnPointerOver", "ToggleSwitchFillOnPressed",
        })
            Brush(name, p.Highlight);
        foreach (string name in new[]
        {
            "NeapOnAccentBrush", "AccentButtonForeground", "AccentButtonForegroundPointerOver", "AccentButtonForegroundPressed",
            "ToggleSwitchKnobFillOn", "ToggleSwitchKnobFillOnPointerOver", "ToggleSwitchKnobFillOnPressed",
            "RadioButtonCheckGlyphFill", "RadioButtonCheckGlyphFillPointerOver", "RadioButtonCheckGlyphFillPressed",
            "CheckBoxCheckGlyphForegroundChecked",
        })
            Brush(name, p.HighlightText);

        // The dial's knob.
        Brush("NeapKnobBrush", p.Window);
        Brush("NeapKnobCapStrokeBrush", p.Text);
        Brush("NeapKnobRidgeBrush", p.Text);
        Brush("NeapKnobRimBrush", p.Text);
        Brush("NeapKnobShadowBrush", Colors.Transparent);
        Brush("NeapKnobWellBrush", p.Window);

        foreach (string accent in new[]
        {
            "SystemAccentColor", "SystemAccentColorLight1", "SystemAccentColorLight2", "SystemAccentColorLight3",
            "SystemAccentColorDark1", "SystemAccentColorDark2", "SystemAccentColorDark3",
        })
            dictionary[accent] = p.Highlight;
        return dictionary;
    }

    private static double Luminance(Color c) => (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255;

    [SupportedOSPlatform("windows")]
    private static Color System(int index)
    {
        uint rgb = GetSysColor(index);
        return Color.FromRgb((byte)(rgb & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)((rgb >> 16) & 0xFF));
    }

    [DllImport("user32.dll")]
    private static extern uint GetSysColor(int index);
}
