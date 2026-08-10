using MultiAudioShare.Core.Audio;

namespace MultiAudioShare.Tests;

public sealed class AudioMathTests
{
    [Theory]
    [InlineData(-1f, 0f)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(2f, 1f)]
    public void ClampGain_ConstrainsToSoftwareVolumeRange(float input, float expected)
    {
        Assert.Equal(expected, AudioMath.ClampGain(input));
    }

    [Fact]
    public void EffectiveGain_MultipliesMasterAndDeviceGain()
    {
        Assert.Equal(0.24f, AudioMath.EffectiveGain(0.8f, 0.3f, muted: false), precision: 5);
    }

    [Fact]
    public void EffectiveGain_ReturnsZeroWhenMuted()
    {
        Assert.Equal(0f, AudioMath.EffectiveGain(1f, 1f, muted: true));
    }
}
