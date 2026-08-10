namespace MultiAudioShare.Core.Configuration;

public sealed record DeviceSelection(
    string DeviceId,
    string DeviceName,
    float Volume,
    bool IsMuted,
    TimeSpan SyncDelay);
