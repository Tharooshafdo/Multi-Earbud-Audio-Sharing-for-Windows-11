using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace MultiAudioShare.Audio.Capture;

public sealed class DefaultEndpointLoopbackCapture : IDisposable
{
    private WasapiLoopbackCapture? _capture;

    public event EventHandler<CapturedAudioFrameEventArgs>? AudioAvailable;
    public event EventHandler<Exception>? CaptureFailed;

    public WaveFormat? WaveFormat => _capture?.WaveFormat;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_capture is not null)
        {
            return Task.CompletedTask;
        }

        using var enumerator = new MMDeviceEnumerator();
        var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

        _capture = new WasapiLoopbackCapture(defaultDevice);
        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += OnRecordingStopped;
        _capture.StartRecording();

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (_capture is null)
        {
            return Task.CompletedTask;
        }

        _capture.StopRecording();
        DisposeCapture();
        return Task.CompletedTask;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0)
        {
            return;
        }

        var buffer = new byte[e.BytesRecorded];
        Buffer.BlockCopy(e.Buffer, 0, buffer, 0, e.BytesRecorded);
        AudioAvailable?.Invoke(this, new CapturedAudioFrameEventArgs(buffer, e.BytesRecorded));
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            CaptureFailed?.Invoke(this, e.Exception);
        }
    }

    private void DisposeCapture()
    {
        if (_capture is null)
        {
            return;
        }

        _capture.DataAvailable -= OnDataAvailable;
        _capture.RecordingStopped -= OnRecordingStopped;
        _capture.Dispose();
        _capture = null;
    }

    public void Dispose() => DisposeCapture();
}
