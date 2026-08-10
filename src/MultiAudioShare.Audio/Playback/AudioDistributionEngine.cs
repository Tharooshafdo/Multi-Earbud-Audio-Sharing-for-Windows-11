using MultiAudioShare.Audio.Capture;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace MultiAudioShare.Audio.Playback;

public sealed class AudioDistributionEngine : IDisposable
{
    private readonly object _gate = new();
    private readonly DefaultEndpointLoopbackCapture _capture = new();
    private readonly List<AudioOutputSession> _sessions = [];
    private WaveFormat? _captureFormat;
    private bool _isStarted;

    public AudioDistributionEngine()
    {
        _capture.AudioAvailable += OnAudioAvailable;
    }

    public IReadOnlyList<AudioOutputSession> Sessions
    {
        get
        {
            lock (_gate)
            {
                return _sessions.ToArray();
            }
        }
    }

    public async Task StartAsync(IEnumerable<(AudioOutputSession Session, MMDevice Device)> outputs, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_isStarted)
            {
                throw new InvalidOperationException("Audio sharing is already running.");
            }
        }

        await _capture.StartAsync(cancellationToken).ConfigureAwait(false);
        _captureFormat = _capture.WaveFormat ?? throw new InvalidOperationException("Loopback capture did not expose an audio format.");

        foreach (var (session, device) in outputs)
        {
            try
            {
                await session.StartAsync(device, _captureFormat, cancellationToken).ConfigureAwait(false);
                lock (_gate)
                {
                    _sessions.Add(session);
                }
            }
            catch
            {
                await session.StopAsync().ConfigureAwait(false);
                throw;
            }
        }

        lock (_gate)
        {
            _isStarted = true;
        }
    }

    public async Task StopAsync()
    {
        AudioOutputSession[] sessions;
        lock (_gate)
        {
            sessions = _sessions.ToArray();
            _sessions.Clear();
            _isStarted = false;
        }

        await _capture.StopAsync().ConfigureAwait(false);
        foreach (var session in sessions)
        {
            await session.StopAsync().ConfigureAwait(false);
        }
    }

    private void OnAudioAvailable(object? sender, CapturedAudioFrameEventArgs e)
    {
        AudioOutputSession[] sessions;
        lock (_gate)
        {
            sessions = _sessions.ToArray();
        }

        foreach (var session in sessions)
        {
            try
            {
                session.Enqueue(e.Buffer, e.ByteCount);
            }
            catch
            {
                _ = session.StopAsync();
            }
        }
    }

    public void Dispose()
    {
        _capture.AudioAvailable -= OnAudioAvailable;
        _capture.Dispose();
        foreach (var session in Sessions)
        {
            session.Dispose();
        }
    }
}
