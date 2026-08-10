namespace MultiAudioShare.Core.Audio;

public enum AudioOutputState
{
    Ready,
    Starting,
    Playing,
    Muted,
    Buffering,
    Stopping,
    Stopped,
    Disconnected,
    Unsupported,
    Error
}
