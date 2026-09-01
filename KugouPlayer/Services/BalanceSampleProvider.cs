using NAudio.Wave;

namespace KugouPlayer.Services;

public sealed class BalanceSampleProvider(ISampleProvider source) : ISampleProvider
{
    private double _balance;

    public WaveFormat WaveFormat => source.WaveFormat;

    public double Balance
    {
        get => _balance;
        set => _balance = Math.Clamp(value, -1, 1);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var samplesRead = source.Read(buffer, offset, count);
        if (WaveFormat.Channels < 2 || Math.Abs(Balance) < 0.001)
        {
            return samplesRead;
        }

        var leftGain = Balance > 0 ? 1 - Balance : 1;
        var rightGain = Balance < 0 ? 1 + Balance : 1;
        for (var sampleIndex = 0; sampleIndex < samplesRead; sampleIndex += WaveFormat.Channels)
        {
            buffer[offset + sampleIndex] *= (float)leftGain;
            if (sampleIndex + 1 < samplesRead)
            {
                buffer[offset + sampleIndex + 1] *= (float)rightGain;
            }
        }
        return samplesRead;
    }
}
