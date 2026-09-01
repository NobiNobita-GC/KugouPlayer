using NAudio.Dsp;
using NAudio.Wave;

namespace KugouPlayer.Services;

public sealed class EqualizerSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly BiQuadFilter[][] _filters;
    private float[] _gains = EqualizerProfiles.GetGains("关闭");

    public EqualizerSampleProvider(ISampleProvider source)
    {
        _source = source;
        WaveFormat = source.WaveFormat;
        _filters = new BiQuadFilter[EqualizerProfiles.Frequencies.Length][];
        RebuildFilters();
    }

    public WaveFormat WaveFormat { get; }

    public void SetGains(float[] gains)
    {
        if (gains.Length != EqualizerProfiles.Frequencies.Length)
        {
            throw new ArgumentException("均衡器增益数量必须与频段数量一致。", nameof(gains));
        }
        _gains = gains.ToArray();
        RebuildFilters();
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var samplesRead = _source.Read(buffer, offset, count);
        var channels = WaveFormat.Channels;
        for (var sampleIndex = 0; sampleIndex < samplesRead; sampleIndex++)
        {
            var channel = (offset + sampleIndex) % channels;
            var value = buffer[offset + sampleIndex];
            for (var band = 0; band < _filters.Length; band++)
            {
                value = _filters[band][channel].Transform(value);
            }
            buffer[offset + sampleIndex] = Math.Clamp(value, -1f, 1f);
        }
        return samplesRead;
    }

    private void RebuildFilters()
    {
        for (var band = 0; band < _filters.Length; band++)
        {
            _filters[band] = new BiQuadFilter[WaveFormat.Channels];
            for (var channel = 0; channel < WaveFormat.Channels; channel++)
            {
                var safeFrequency = Math.Min(EqualizerProfiles.Frequencies[band], WaveFormat.SampleRate * 0.45f);
                _filters[band][channel] = BiQuadFilter.PeakingEQ(
                    WaveFormat.SampleRate,
                    safeFrequency,
                    0.8f,
                    _gains[band]);
            }
        }
    }
}
