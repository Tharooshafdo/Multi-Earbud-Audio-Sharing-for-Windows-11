using MultiAudioShare.Audio.Playback;
using MultiAudioShare.Devices;

Console.WriteLine("MultiAudio Share - audio routing proof of concept");
Console.WriteLine("Captures the current default playback endpoint with WASAPI loopback.");
Console.WriteLine();

using var deviceManager = new AudioDeviceManager();
var devices = deviceManager.EnumerateActiveRenderDevices();

if (devices.Count == 0)
{
    Console.WriteLine("No active render endpoints were found.");
    return 1;
}

Console.WriteLine("Active render endpoints:");
for (var i = 0; i < devices.Count; i++)
{
    var device = devices[i];
    var tags = new List<string>();
    if (device.IsDefault)
    {
        tags.Add("default");
    }

    if (device.IsLikelyBluetooth)
    {
        tags.Add("likely Bluetooth");
    }

    if (device.IsLikelyHandsFree)
    {
        tags.Add("hands-free/headset");
    }

    var tagText = tags.Count > 0 ? $" [{string.Join(", ", tags)}]" : string.Empty;
    Console.WriteLine($"{i}: {device.FriendlyName}{tagText}");
    Console.WriteLine($"   {device.SampleRate} Hz, {device.Channels} ch, {device.BitsPerSample} bit");
}

Console.WriteLine();
Console.Write("Select listener endpoint indexes separated by commas, for example 1,2: ");
var selectionText = Console.ReadLine();
var selectedIndexes = ParseIndexes(selectionText, devices.Count);
if (selectedIndexes.Count == 0)
{
    Console.WriteLine("No valid endpoints selected.");
    return 1;
}

var defaultSelections = selectedIndexes.Where(index => devices[index].IsDefault).ToArray();
if (defaultSelections.Length > 0)
{
    Console.WriteLine("The default playback endpoint is the loopback capture source and cannot be selected as an output in this proof of concept.");
    Console.WriteLine("Set Windows audio to play through one device, then select one or more different listener endpoints.");
    return 1;
}

using var engine = new AudioDistributionEngine();
var sessions = new List<AudioOutputSession>();
var outputs = new List<(AudioOutputSession Session, NAudio.CoreAudioApi.MMDevice Device)>();

foreach (var index in selectedIndexes)
{
    var device = devices[index];
    var session = new AudioOutputSession(device.Id, device.FriendlyName, volume: 1f, syncDelay: TimeSpan.Zero);
    sessions.Add(session);
    outputs.Add((session, deviceManager.GetDevice(device.Id)));
}

try
{
    await engine.StartAsync(outputs);
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine("Unable to start audio sharing.");
    Console.WriteLine(ex.Message);
    Console.WriteLine("If this happened on a second or third Bluetooth headset, Windows or the Bluetooth adapter may not allow another simultaneous A2DP stream.");
    return 2;
}

Console.WriteLine();
Console.WriteLine("Sharing started. Play audio in another app.");
Console.WriteLine("Commands: v <n> <0-100>, m <n>, s <n> <0-1000>, stats, q");
Console.WriteLine("Example: v 1 35");

var quit = false;
while (!quit)
{
    Console.Write("> ");
    var line = Console.ReadLine();
    if (line is null)
    {
        break;
    }

    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (parts.Length == 0)
    {
        continue;
    }

    switch (parts[0].ToLowerInvariant())
    {
        case "q":
        case "quit":
        case "exit":
            quit = true;
            break;

        case "v" when parts.Length == 3 && TrySession(parts[1], sessions, out var volumeSession) && int.TryParse(parts[2], out var percent):
            volumeSession.SetVolume(Math.Clamp(percent, 0, 100) / 100f);
            Console.WriteLine($"{volumeSession.DeviceName}: volume {Math.Clamp(percent, 0, 100)}%");
            break;

        case "m" when parts.Length == 2 && TrySession(parts[1], sessions, out var muteSession):
            muteSession.SetMuted(!muteSession.IsMuted);
            Console.WriteLine($"{muteSession.DeviceName}: {(muteSession.IsMuted ? "muted" : "unmuted")}");
            break;

        case "s" when parts.Length == 3 && TrySession(parts[1], sessions, out var syncSession) && int.TryParse(parts[2], out var delayMs):
            syncSession.SetSyncDelay(TimeSpan.FromMilliseconds(Math.Clamp(delayMs, 0, 1000)));
            Console.WriteLine($"{syncSession.DeviceName}: sync delay {syncSession.SyncDelay.TotalMilliseconds:0} ms");
            break;

        case "stats":
            PrintStats(sessions);
            break;

        default:
            Console.WriteLine("Unknown command. Use: v <n> <0-100>, m <n>, s <n> <0-1000>, stats, q");
            break;
    }
}

await engine.StopAsync();
Console.WriteLine("Sharing stopped.");
return 0;

static IReadOnlyList<int> ParseIndexes(string? text, int deviceCount)
{
    if (string.IsNullOrWhiteSpace(text))
    {
        return [];
    }

    return text
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(part => int.TryParse(part, out var index) ? index : -1)
        .Where(index => index >= 0 && index < deviceCount)
        .Distinct()
        .ToArray();
}

static bool TrySession(string text, IReadOnlyList<AudioOutputSession> sessions, out AudioOutputSession session)
{
    session = null!;
    if (!int.TryParse(text, out var oneBasedIndex))
    {
        return false;
    }

    var index = oneBasedIndex - 1;
    if (index < 0 || index >= sessions.Count)
    {
        return false;
    }

    session = sessions[index];
    return true;
}

static void PrintStats(IReadOnlyList<AudioOutputSession> sessions)
{
    for (var i = 0; i < sessions.Count; i++)
    {
        var session = sessions[i];
        var stats = session.GetStatistics();
        Console.WriteLine($"{i + 1}: {session.DeviceName}");
        Console.WriteLine($"   State: {session.State}, Volume: {session.Volume:P0}, Muted: {session.IsMuted}, Sync: {session.SyncDelay.TotalMilliseconds:0} ms");
        Console.WriteLine($"   Queued: {stats.Queued.TotalMilliseconds:0} ms, Written: {stats.BytesWritten:N0} bytes, Dropped: {stats.DroppedBytes:N0} bytes");
        if (!string.IsNullOrWhiteSpace(session.LastError))
        {
            Console.WriteLine($"   Error: {session.LastError}");
        }
    }
}
