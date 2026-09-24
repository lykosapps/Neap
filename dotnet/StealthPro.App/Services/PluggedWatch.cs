using System.Globalization;
using Microsoft.UI.Dispatching;
using StealthPro.Core.Hid;

namespace StealthPro.App.Services;

/// <summary>
/// Watches which of the headset's USB devices are plugged in, for a page
/// that shows it.
/// </summary>
/// <remarks>
/// <para>
/// Enumerating devices opens nothing for I/O, so it cannot disturb the
/// connection; it only has to be quick enough that a transmitter shows up
/// while someone is still looking.
/// </para>
/// <para>
/// A page makes one when it loads and stops it when it unloads, so
/// nothing is polled while the window is hidden.
/// </para>
/// </remarks>
public sealed class PluggedWatch
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);

    private readonly DispatcherQueueTimer _timer;
    private readonly Action _changed;
    private bool _failing;

    /// <param name="ui">The page's dispatcher; <paramref name="changed"/> is raised on it.</param>
    /// <param name="changed">Called after the first look, and whenever what is plugged in changes.</param>
    public PluggedWatch(DispatcherQueue ui, Action changed)
    {
        _changed = changed;
        _timer = ui.CreateTimer();
        _timer.Interval = Interval;
        _timer.Tick += async (_, _) => await Look();
        _timer.Start();
        _ = Look();
    }

    /// <summary>Gets whether it has looked yet. Not having looked is not the same as nothing plugged in.</summary>
    public bool Looked { get; private set; }

    /// <summary>Gets the product ids plugged in, as four hex digits.</summary>
    public IReadOnlyList<string> Products { get; private set; } = Array.Empty<string>();

    private async Task Look()
    {
        IReadOnlyList<HidDeviceInfo> now;
        try { now = await Task.Run(() => AppServices.Devices.Candidates()); }
        catch (Exception ex)
        {
            // Once, not every three seconds for as long as it keeps failing.
            if (!_failing) AppLog.Write($"could not list the USB devices: {ex.Message}");
            _failing = true;
            return;
        }
        _failing = false;

        var products = now.Select(d => d.ProductId.ToString("X4", CultureInfo.InvariantCulture))
            .Order(StringComparer.Ordinal).ToArray();
        if (Looked && products.SequenceEqual(Products)) return;

        Looked = true;
        Products = products;
        _changed();
    }

    public void Stop() => _timer.Stop();
}
