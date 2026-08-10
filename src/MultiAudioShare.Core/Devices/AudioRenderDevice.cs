namespace MultiAudioShare.Core.Devices;

public sealed record AudioRenderDevice(
    string Id,
    string FriendlyName,
    string State,
    bool IsDefault,
    bool IsLikelyBluetooth,
    bool IsLikelyHandsFree,
    int SampleRate,
    int Channels,
    int BitsPerSample);
