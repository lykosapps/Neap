namespace StealthPro.Core.Settings;

public enum SettingKind { Range, Toggle, Enum, Text }

/// <summary>
/// One confirmed setting.
///
/// <see cref="Writable"/> is false for values the headset reports but Swarm
/// never sets — either because they are read-only, or because we have not
/// established what they do and will not guess with somebody's hardware.
/// </summary>
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

    /// <summary>Check a value against this key and return it as the wire form.</summary>
    public string Validate(object value)
    {
        if (Kind == SettingKind.Text) return value.ToString() ?? "";

        if (!int.TryParse(value.ToString(), out int number))
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

        return number.ToString();
    }
}
