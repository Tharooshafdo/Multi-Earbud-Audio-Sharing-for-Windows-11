using MultiAudioShare.Core.Devices;
using NAudio.CoreAudioApi;

namespace MultiAudioShare.Devices;

public sealed class AudioDeviceManager : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();

    public IReadOnlyList<AudioRenderDevice> EnumerateActiveRenderDevices()
    {
        var defaultId = TryGetDefaultDeviceId();
        return _enumerator
            .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .Select(device => ToModel(device, defaultId))
            .OrderByDescending(device => device.IsDefault)
            .ThenByDescending(device => device.IsLikelyBluetooth && !device.IsLikelyHandsFree)
            .ThenBy(device => device.FriendlyName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public MMDevice GetDevice(string id) => _enumerator.GetDevice(id);

    private string? TryGetDefaultDeviceId()
    {
        try
        {
            return _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID;
        }
        catch
        {
            return null;
        }
    }

    private static AudioRenderDevice ToModel(MMDevice device, string? defaultId)
    {
        var mixFormat = device.AudioClient.MixFormat;
        var name = device.FriendlyName;
        var lowerName = name.ToLowerInvariant();

        return new AudioRenderDevice(
            device.ID,
            name,
            device.State.ToString(),
            string.Equals(device.ID, defaultId, StringComparison.Ordinal),
            lowerName.Contains("bluetooth", StringComparison.Ordinal) ||
            lowerName.Contains("buds", StringComparison.Ordinal) ||
            lowerName.Contains("airpods", StringComparison.Ordinal) ||
            lowerName.Contains("headphones", StringComparison.Ordinal),
            lowerName.Contains("hands-free", StringComparison.Ordinal) ||
            lowerName.Contains("hands free", StringComparison.Ordinal) ||
            lowerName.Contains("headset", StringComparison.Ordinal),
            mixFormat.SampleRate,
            mixFormat.Channels,
            mixFormat.BitsPerSample);
    }

    public void Dispose() => _enumerator.Dispose();
}
