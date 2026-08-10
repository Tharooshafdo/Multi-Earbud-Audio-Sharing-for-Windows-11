using MultiAudioShare.Core.Audio;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MultiAudioShare.Audio.Playback;

public sealed class AudioOutputSession : IDisposable
{
    private readonly object _gate = new();
    private readonly TimeSpan _targetBuffer;
    private readonly TimeSpan _maxBuffer;
    private BufferedWaveProvider? _bufferedWaveProvider;
    private MediaFoundationResampler? _resampler;
    private VolumeSampleProvider? _volumeProvider;
    private WasapiOut? _output;
    private WaveFormat? _sourceFormat;
    private long _bytesWritten;
    private long _droppedBytes;
    private long _bufferUnderruns;
    private bool _isDisposed;

    public AudioOutputSession(string deviceId, string deviceName, float volume, TimeSpan syncDelay, TimeSpan? targetBuffer = null, TimeSpan? maxBuffer = null)
    {
        DeviceId = deviceId;
        DeviceName = deviceName;
        Volume = AudioMath.ClampGain(volume);
        SyncDelay = ClampSyncDelay(syncDelay);
        _targetBuffer = targetBuffer ?? TimeSpan.FromMilliseconds(180);
        _maxBuffer = maxBuffer ?? TimeSpan.FromSeconds(1);
    }

    public string DeviceId { get; }
    public string DeviceName { get; }
    public AudioOutputState State { get; private set; } = AudioOutputState.Ready;
    public float Volume { get; private set; }
    public bool IsMuted { get; private set; }
    public float MasterVolume { get; private set; } = 1f;
    public TimeSpan SyncDelay { get; private set; }
    public string? LastError { get; private set; }

    public Task StartAsync(MMDevice device, WaveFormat sourceFormat, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            ThrowIfDisposed();
            StopLocked();
            State = AudioOutputState.Starting;
            LastError = null;
            _sourceFormat = sourceFormat;
            _bytesWritten = 0;
            _droppedBytes = 0;
            _bufferUnderruns = 0;

            var bufferLength = Math.Max(sourceFormat.AverageBytesPerSecond / 5, (int)(sourceFormat.AverageBytesPerSecond * _maxBuffer.TotalSeconds));
            _bufferedWaveProvider = new BufferedWaveProvider(sourceFormat)
            {
                BufferLength = bufferLength,
                DiscardOnBufferOverflow = true,
                ReadFully = true
            };

            WriteSilenceLocked(_targetBuffer + SyncDelay);

            IWaveProvider waveProvider = _bufferedWaveProvider;
            var outputFormat = device.AudioClient.MixFormat;
            if (!WaveFormatsEquivalent(sourceFormat, outputFormat))
            {
                _resampler = new MediaFoundationResampler(waveProvider, outputFormat)
                {
                    ResamplerQuality = 60
                };
                waveProvider = _resampler;
            }

            _volumeProvider = new VolumeSampleProvider(waveProvider.ToSampleProvider())
            {
                Volume = EffectiveVolume
            };

            _output = new WasapiOut(device, AudioClientShareMode.Shared, false, (int)_targetBuffer.TotalMilliseconds);
            _output.PlaybackStopped += OnPlaybackStopped;
            _output.Init(_volumeProvider);
            _output.Play();
            State = IsMuted ? AudioOutputState.Muted : AudioOutputState.Playing;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        lock (_gate)
        {
            StopLocked();
            State = AudioOutputState.Stopped;
        }

        return Task.CompletedTask;
    }

    public void Enqueue(byte[] buffer, int byteCount)
    {
        lock (_gate)
        {
            if (_bufferedWaveProvider is null || State is AudioOutputState.Stopping or AudioOutputState.Stopped or AudioOutputState.Error)
            {
                return;
            }

            var before = _bufferedWaveProvider.BufferedBytes;
            _bufferedWaveProvider.AddSamples(buffer, 0, byteCount);
            var after = _bufferedWaveProvider.BufferedBytes;
            _bytesWritten += byteCount;

            if (after - before < byteCount)
            {
                _droppedBytes += byteCount - Math.Max(0, after - before);
            }
        }
    }

    public void SetVolume(float volume)
    {
        lock (_gate)
        {
            Volume = AudioMath.ClampGain(volume);
            ApplyVolumeLocked();
        }
    }

    public void SetMasterVolume(float volume)
    {
        lock (_gate)
        {
            MasterVolume = AudioMath.ClampGain(volume);
            ApplyVolumeLocked();
        }
    }

    public void SetMuted(bool muted)
    {
        lock (_gate)
        {
            IsMuted = muted;
            ApplyVolumeLocked();
            if (State is AudioOutputState.Playing or AudioOutputState.Muted)
            {
                State = muted ? AudioOutputState.Muted : AudioOutputState.Playing;
            }
        }
    }

    public void SetSyncDelay(TimeSpan delay)
    {
        lock (_gate)
        {
            SyncDelay = ClampSyncDelay(delay);
            if (_bufferedWaveProvider is not null && _sourceFormat is not null)
            {
                _bufferedWaveProvider.ClearBuffer();
                WriteSilenceLocked(_targetBuffer + SyncDelay);
            }
        }
    }

    public AudioOutputStatistics GetStatistics()
    {
        lock (_gate)
        {
            var queued = TimeSpan.Zero;
            if (_bufferedWaveProvider is not null)
            {
                queued = TimeSpan.FromSeconds((double)_bufferedWaveProvider.BufferedBytes / _bufferedWaveProvider.WaveFormat.AverageBytesPerSecond);
            }

            return new AudioOutputStatistics(queued, _bytesWritten, _bufferUnderruns, _droppedBytes);
        }
    }

    private float EffectiveVolume => AudioMath.EffectiveGain(MasterVolume, Volume, IsMuted);

    private void ApplyVolumeLocked()
    {
        if (_volumeProvider is not null)
        {
            _volumeProvider.Volume = EffectiveVolume;
        }
    }

    private void WriteSilenceLocked(TimeSpan duration)
    {
        if (_bufferedWaveProvider is null || _sourceFormat is null || duration <= TimeSpan.Zero)
        {
            return;
        }

        var bytes = Math.Min(_bufferedWaveProvider.BufferLength, (int)(_sourceFormat.AverageBytesPerSecond * duration.TotalSeconds));
        if (bytes <= 0)
        {
            return;
        }

        var silence = new byte[bytes];
        _bufferedWaveProvider.AddSamples(silence, 0, silence.Length);
    }

    private void StopLocked()
    {
        State = AudioOutputState.Stopping;

        if (_output is not null)
        {
            _output.PlaybackStopped -= OnPlaybackStopped;
            _output.Stop();
            _output.Dispose();
            _output = null;
        }

        _resampler?.Dispose();
        _resampler = null;
        _bufferedWaveProvider = null;
        _volumeProvider = null;
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        lock (_gate)
        {
            if (e.Exception is not null)
            {
                State = AudioOutputState.Error;
                LastError = e.Exception.Message;
            }
        }
    }

    private static bool WaveFormatsEquivalent(WaveFormat left, WaveFormat right)
    {
        return left.SampleRate == right.SampleRate &&
               left.Channels == right.Channels &&
               left.BitsPerSample == right.BitsPerSample &&
               left.Encoding == right.Encoding;
    }

    private static TimeSpan ClampSyncDelay(TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return delay > TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : delay;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            StopLocked();
            _isDisposed = true;
        }
    }
}
