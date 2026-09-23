using System.Globalization;

namespace StealthPro.Core.Settings;

public enum SettingKind { Range, Toggle, Enum, Text }

/// <summary>One confirmed setting.</summary>
/// <remarks>
/// <see cref="Writable"/> is false for values the headset reports but Swarm
/// never sets: either they are read-only, or what they do is not established
/// and a guess is not worth risking on somebody's hardware.
/// </remarks>
public sealed record SettingKey(
    int Key,
    string Name,
    string Category,
    SettingKind Kind,
    int? Minimum = null,
    int? Maximum = null,
    IReadOnlyDictionary<int, string>? Options = null,
    bool Writable = true,
    string Note = "")
{
    public string Hex => $"0x{Key:x}";

    /// <summary>Checks a value against this key and returns it in wire form.</summary>
    public string Validate(object value)
    {
        if (Kind == SettingKind.Text) return value.ToString() ?? "";

        if (!int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
                NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
            throw new ArgumentException($"{Name} takes a number; got '{value}'");

        if (Kind == SettingKind.Toggle && number is not (0 or 1))
            throw new ArgumentException($"{Name} is a toggle; got {number}");
        if (Kind == SettingKind.Enum && Options is { Count: > 0 } && !Options.ContainsKey(number))
            throw new ArgumentException(
                $"{Name} accepts {string.Join(", ", Options.Keys.Order())}; got {number}");
        if (Minimum is { } low && number < low)
            throw new ArgumentException($"{Name} minimum is {low}");
        if (Maximum is { } high && number > high)
            throw new ArgumentException($"{Name} maximum is {high}");

        return number.ToString(CultureInfo.InvariantCulture);
    }
}
