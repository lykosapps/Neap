using Avalonia.Automation.Peers;
using Avalonia.Controls.Primitives;

namespace Neap.Desktop.Controls;

/// <summary>Tells UI Automation that a dial is a slider, with its value and range.</summary>
/// <remarks>
/// The toolkit gives its own slider this role, but not a control built on
/// <see cref="RangeBase"/> as the dials are, which would otherwise be read as
/// a plain control with no value. The range value it reports also changes as
/// the dial turns, so a screen reader says the new value.
/// </remarks>
internal sealed class DialAutomationPeer(RangeBase owner) : RangeBaseAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;
}
