namespace Neap.Core.Audio;

/// <summary>The device Windows uses for one role.</summary>
/// <param name="Output">An output rather than a microphone.</param>
/// <param name="Role">Windows' name for the role: Multimedia, Console or Communications.</param>
/// <param name="Device">The device's name, or empty when Windows has none for the role.</param>
/// <param name="Product">The Turtle Beach product id behind it, or empty for anything else.</param>
public sealed record SoundDefault(bool Output, string Role, string Device, string Product);

/// <summary>A program with sound open on a device.</summary>
/// <param name="Name">The program's process name, or "system" for Windows' own sounds.</param>
/// <param name="Percent">Its own volume on that device, which the mix moves.</param>
/// <param name="Muted">Whether it is muted on that device.</param>
/// <param name="Playing">Whether it is making sound now, rather than only holding the device open.</param>
public sealed record SoundApp(string Name, int Percent, bool Muted, bool Playing);

/// <summary>One of the headset's devices in Windows.</summary>
/// <param name="Output">An output rather than a microphone.</param>
/// <param name="Name">The device's name.</param>
/// <param name="Product">The Turtle Beach product id behind it.</param>
/// <param name="Percent">The device's volume in Windows.</param>
/// <param name="Muted">Whether Windows has it muted.</param>
/// <param name="Peak">The loudest level heard while the survey listened, from 0 to 1.</param>
/// <param name="Apps">The programs with sound open on it.</param>
public sealed record SoundDevice(
    bool Output, string Name, string Product, int Percent, bool Muted, float Peak,
    IReadOnlyList<SoundApp> Apps);

/// <summary>Where Windows sends sound and takes the microphone from, and how loud, at one moment.</summary>
/// <param name="Defaults">The device for each role, outputs then microphones.</param>
/// <param name="Devices">Every one of the headset's devices Windows has active.</param>
/// <param name="OutputFormat">The headset output's format, or why it could not be read.</param>
/// <param name="InputFormat">The headset microphone's format, or why it could not be read.</param>
public sealed record SoundSurvey(
    IReadOnlyList<SoundDefault> Defaults, IReadOnlyList<SoundDevice> Devices,
    string OutputFormat, string InputFormat);
