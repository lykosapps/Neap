using System.Text;

namespace Neap.Core.Diagnostics;

/// <summary>
/// The table of which headset has which of Neap's functions, built from the
/// headset models so it cannot drift from what the app does.
/// </summary>
/// <remarks>
/// <c>docs/SUPPORT-MATRIX.md</c> is this text, kept in step by a test. A new
/// function added to <see cref="Feature"/> needs a label here, or the matrix
/// fails to build.
/// </remarks>
public static class SupportMatrix
{
    /// <summary>The support matrix as a Markdown document.</summary>
    public static string Markdown()
    {
        var models = HeadsetModels.All;
        var sb = new StringBuilder();
        sb.AppendLine("# Headset support");
        sb.AppendLine();
        sb.AppendLine("What each headset Neap knows has of the functions Neap offers. This file is");
        sb.AppendLine("generated from the headset models in code and kept in step by a test, so change");
        sb.AppendLine("the models, not this file.");
        sb.AppendLine();
        sb.AppendLine("- **Yes** — Neap reads it and offers it.");
        sb.AppendLine("- **Offered** — the headset has it, but Neap cannot yet read it, so it is shown to be tried.");
        sb.AppendLine("- **—** — not offered on this headset: either it does not have it, or what it does is");
        sb.AppendLine("  not yet known. Each headset's file in `docs/headsets` says which.");
        sb.AppendLine();

        sb.Append("| Function |");
        foreach (var model in models) sb.Append(' ').Append(model.Name).Append(" |");
        sb.AppendLine();
        sb.Append("|---|");
        foreach (var _ in models) sb.Append("---|");
        sb.AppendLine();

        foreach (var feature in Enum.GetValues<Feature>())
        {
            sb.Append("| ").Append(Label(feature)).Append(" |");
            foreach (var model in models) sb.Append(' ').Append(Cell(model, feature)).Append(" |");
            sb.AppendLine();
        }

        sb.Append("| Max loudness boost |");
        foreach (var model in models) sb.Append(' ').Append(model.HasLoudnessBoost ? "Yes" : "—").Append(" |");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string Cell(HeadsetModel model, Feature feature) =>
        !model.Features.Contains(feature) ? "—"
        : model.Unread.Contains(feature) ? "Offered"
        : "Yes";

    /// <summary>A function's name in plain words.</summary>
    private static string Label(Feature feature) => feature switch
    {
        Feature.Battery => "Battery",
        Feature.MasterVolume => "Master volume",
        Feature.VoicePrompts => "Voice prompts",
        Feature.NoiseControl => "Noise cancellation",
        Feature.SuperhumanHearing => "Superhuman Hearing",
        Feature.GameEqualiser => "Game equaliser",
        Feature.Microphone => "Microphone",
        Feature.MicMonitoring => "Mic monitoring",
        Feature.AiNoiseReduction => "AI noise reduction",
        Feature.NoiseGate => "Noise gate",
        Feature.MicrophoneEqualiser => "Microphone equaliser",
        Feature.ModeButton => "Mode button",
        Feature.LowerDial => "Lower dial",
        Feature.ChatWheel => "Chat wheel",
        Feature.Lights => "Lights",
        Feature.AutoShutOff => "Auto shut-off",
        Feature.WakeOnMotion => "Wake on motion",
        _ => throw new ArgumentOutOfRangeException(nameof(feature), feature, "no label for this function"),
    };
}
