namespace MultiAudioShare.Core.Audio;

public sealed record AudioOutputStatistics(
    TimeSpan Queued,
    long BytesWritten,
    long BufferUnderruns,
    long DroppedBytes);
