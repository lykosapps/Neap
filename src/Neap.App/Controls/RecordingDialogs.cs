using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Neap.Core.Diagnostics;

namespace Neap.App.Controls;

/// <summary>The dialogs around recording headset activity: how to do it, and what it found.</summary>
public static class RecordingDialogs
{
    /// <summary>Shows how to record, for a problem and for another headset.</summary>
    /// <remarks>
    /// The people who need this most are the least likely to read a guide on
    /// a website, so the steps are where the Start button is. The list for
    /// another headset matches docs/MAPPING.md.
    /// </remarks>
    public static async Task ShowHowTo(XamlRoot root)
    {
        var body = new StackPanel { Spacing = 8 };
        Heading(body, "Record_ProblemHeading");
        Line(body, Strings.Get("Record_ProblemSteps"));
        Heading(body, "Record_HeadsetHeading");
        Line(body, Strings.Get("Record_HeadsetStart"));
        string[] steps = ["Record_StepVolume", "Record_StepBalance", "Record_StepMute", "Record_StepMode", "Record_StepButton", "Record_StepPower"];
        for (int i = 0; i < steps.Length; i++)
            Line(body, string.Create(CultureInfo.CurrentCulture, $"{i + 1}. {Strings.Get(steps[i])}"), indent: true);
        Line(body, Strings.Get("Record_HeadsetEnd"));
        Line(body, Strings.Get("Record_Private"), secondary: true);

        await new NeapDialog
        {
            XamlRoot = root,
            Title = Strings.Get("Record_HowTitle"),
            Content = Scrolling(body),
            CloseButtonText = Strings.Get("Dialog_OK"),
        }.ShowAsync();
    }

    /// <summary>Shows what a recording found on another headset, and asks whether to report it.</summary>
    /// <returns>Whether the person chose to report it.</returns>
    public static async Task<bool> ConfirmFindings(XamlRoot root, HeadsetFindings findings)
    {
        var body = new StackPanel { Spacing = 8 };
        if (!findings.Answered)
        {
            Line(body, Strings.Get("Found_NoAnswer"));
        }
        else
        {
            Line(body, Strings.Get("Found_Note"), secondary: true);
            Group(body, "Found_FoundHeading", findings.Found);
            Group(body, "Found_MissingHeading", findings.Missing);
            if (findings.Moved.Count > 0) Group(body, "Found_MovedHeading", findings.Moved);
        }

        var answer = await new NeapDialog
        {
            XamlRoot = root,
            Title = Strings.Get("Found_Title"),
            Content = Scrolling(body),
            PrimaryButtonText = Strings.Get("Found_Send"),
            CloseButtonText = Strings.Get("Dialog_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        }.ShowAsync();
        return answer == ContentDialogResult.Primary;
    }

    private static void Group(StackPanel body, string heading, IReadOnlyList<Feature> features)
    {
        Heading(body, heading);
        Line(body, features.Count == 0 ? Strings.Get("Found_None") : Strings.List(features.Select(Name).ToList()));
    }

    private static string Name(Feature feature) => Strings.Get(feature switch
    {
        Feature.Battery => "Feature_Battery",
        Feature.MasterVolume => "Feature_MasterVolume",
        Feature.VoicePrompts => "Feature_VoicePrompts",
        Feature.NoiseControl => "Feature_NoiseControl",
        Feature.SuperhumanHearing => "Feature_SuperhumanHearing",
        Feature.GameEqualiser => "Feature_GameEqualiser",
        Feature.Microphone => "Feature_Microphone",
        Feature.MicMonitoring => "Feature_MicMonitoring",
        Feature.AiNoiseReduction => "Feature_AiNoiseReduction",
        Feature.NoiseGate => "Feature_NoiseGate",
        Feature.MicrophoneEqualiser => "Feature_MicrophoneEqualiser",
        Feature.ModeButton => "Feature_ModeButton",
        Feature.LowerDial => "Feature_LowerDial",
        Feature.ChatWheel => "Feature_ChatWheel",
        Feature.Lights => "Feature_Lights",
        Feature.AutoShutOff => "Feature_AutoShutOff",
        Feature.WakeOnMotion => "Feature_WakeOnMotion",
        _ => throw new ArgumentOutOfRangeException(nameof(feature), feature, null),
    });

    private static void Heading(StackPanel body, string key) => body.Children.Add(new TextBlock
    {
        Text = Strings.Get(key),
        Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
        Margin = new Thickness(0, 8, 0, 0),
    });

    private static void Line(StackPanel body, string text, bool indent = false, bool secondary = false)
    {
        var line = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(indent ? 12 : 0, 0, 0, 0),
        };
        if (secondary) line.Style = (Style)Application.Current.Resources["SecondaryBodyTextStyle"];
        body.Children.Add(line);
    }

    /// <summary>Lets the steps scroll when text is enlarged past what the dialog can hold.</summary>
    private static ScrollViewer Scrolling(UIElement body) => new()
    {
        Content = body,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
    };
}
