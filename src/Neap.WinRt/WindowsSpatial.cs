using Neap.Core.Audio;

namespace Neap.WinRt;

/// <summary>Windows' spatial sound, which <see cref="SpatialAudio"/> reaches.</summary>
public sealed class WindowsSpatial : ISpatialAudio
{
    public bool Supported => true;

    public Task<SpatialPanel> Read() => SpatialAudio.Read();

    public Task<SpatialResult> Apply(SpatialFormat format) => SpatialAudio.Apply(format);

    public IDisposable? Watch(string endpointId, Action changed) => SpatialAudio.Watch(endpointId, changed);
}
