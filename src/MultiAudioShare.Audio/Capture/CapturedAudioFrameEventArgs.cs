namespace MultiAudioShare.Audio.Capture;

public sealed class CapturedAudioFrameEventArgs(byte[] buffer, int byteCount) : EventArgs
{
    public byte[] Buffer { get; } = buffer;
    public int ByteCount { get; } = byteCount;
}
