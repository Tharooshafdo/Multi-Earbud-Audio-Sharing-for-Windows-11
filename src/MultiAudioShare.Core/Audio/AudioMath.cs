namespace MultiAudioShare.Core.Audio;

public static class AudioMath
{
    public static float ClampGain(float gain) => Math.Clamp(gain, 0f, 1f);

    public static float EffectiveGain(float masterGain, float deviceGain, bool muted)
    {
        if (muted)
        {
            return 0f;
        }

        return ClampGain(masterGain) * ClampGain(deviceGain);
    }
}
