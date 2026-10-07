using System.Globalization;

namespace Neap.Core.Connection;

/// <summary>What the Charging Dock's battery slot holds.</summary>
public enum SpareState
{
    /// <summary>No battery in the slot.</summary>
    Empty,
    /// <summary>A battery, charged to <see cref="SpareReading.Percent"/>.</summary>
    InSlot,
    /// <summary>A value that did not parse, shown as no reading rather than as empty.</summary>
    Unreadable,
}

/// <summary>The Charging Dock's spare battery, as the app shows it.</summary>
/// <param name="State">What the slot holds.</param>
/// <param name="Percent">Its charge; zero unless <paramref name="State"/> is <see cref="SpareState.InSlot"/>.</param>
public readonly record struct SpareReading(SpareState State, int Percent)
{
    /// <summary>Whether the dock is charging the battery: one in the slot and not yet full.</summary>
    public bool Charging => State == SpareState.InSlot && Percent < 100;
}

/// <summary>Reads the spare battery a Charging Dock reports inside its transmitter slot.</summary>
/// <remarks>
/// <para>
/// The fourth entry of the dock's slot is the spare's charge, 0 to 100, and
/// 255 while the slot is empty. It went to 255 and back each time the spare
/// was taken out and put back, and a drained battery put in the dock rose
/// from 37 to 100 as it charged. The USB Transmitter, which has no slot,
/// reports 0 there.
/// </para>
/// <para>
/// Nothing separate says it is charging. The dock charges whatever is in the
/// slot, so a charge below 100 is a battery charging and the percentage is
/// all there is to show.
/// </para>
/// </remarks>
public static class SpareBattery
{
    /// <summary>What the dock reports while its battery slot is empty.</summary>
    public const string EmptySlot = "255";

    public static SpareReading Parse(string reported) =>
        reported == EmptySlot ? new SpareReading(SpareState.Empty, 0)
        : int.TryParse(reported, NumberStyles.None, CultureInfo.InvariantCulture, out int percent) && percent <= 100
            ? new SpareReading(SpareState.InSlot, percent)
            : new SpareReading(SpareState.Unreadable, 0);
}
