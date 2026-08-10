# Bluetooth Limitations

MultiAudio Share cannot bypass limits imposed by Windows, the Bluetooth adapter, device drivers, or headset firmware.

Bluetooth stereo playback normally uses A2DP. Many headsets also expose a hands-free/headset endpoint for calls. The hands-free profile is lower quality and may change microphone and speaker routing, so the app prefers stereo endpoints where Windows exposes enough information to identify them.

The number of simultaneous Bluetooth audio render endpoints is not guaranteed. Some adapters and drivers may handle two A2DP playback streams, while others may fail when opening the second or third stream. When that happens, the correct behavior is to show the endpoint error and keep already running outputs alive.

Latency is also device dependent. Each headset may use different buffering, codec settings, firmware behavior, and radio scheduling. Two nearby listeners can hear echo even if both streams are technically playing correctly. MultiAudio Share provides manual sync delay so faster devices can be delayed to better match slower devices.

What the app controls:

- Which Windows render endpoints it opens.
- Per-listener software volume and mute.
- Per-listener buffering and additional sync delay.
- Error reporting when an endpoint cannot be opened.

What Windows or hardware controls:

- Whether multiple A2DP streams are allowed.
- Bluetooth codec selection and radio scheduling.
- Driver stability and endpoint availability.
- Baseline headset latency.
