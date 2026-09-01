using KugouPlayer.Models;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.IO;

namespace KugouPlayer.Services;

public sealed class AudioPlayerService : IDisposable
{
    private readonly MMDeviceEnumerator _deviceEnumerator = new();
    private IWavePlayer? _output;
    private MMDevice? _playbackDevice;
    private WaveStream? _reader;
    private EqualizerSampleProvider? _equalizer;
    private BalanceSampleProvider? _balanceProvider;
    private VolumeSampleProvider? _volumeProvider;
    private string? _currentFilePath;
    private string? _outputDeviceId;
    private float[] _equalizerGains = EqualizerProfiles.GetGains("关闭");
    private double _volume = 0.68;
    private double _balance;
    private bool _manualStop;

    public event EventHandler? MediaOpened;
    public event EventHandler? MediaEnded;
    public event EventHandler<Exception>? MediaFailed;

    public TimeSpan Position
    {
        get => _reader?.CurrentTime ?? TimeSpan.Zero;
        set
        {
            if (_reader is not null)
            {
                _reader.CurrentTime = value < TimeSpan.Zero ? TimeSpan.Zero :
                    value > _reader.TotalTime ? _reader.TotalTime : value;
            }
        }
    }

    public TimeSpan Duration => _reader?.TotalTime ?? TimeSpan.Zero;

    public double Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 1);
            if (_volumeProvider is not null)
            {
                _volumeProvider.Volume = (float)_volume;
            }
        }
    }

    public double Balance
    {
        get => _balance;
        set
        {
            _balance = Math.Clamp(value, -1, 1);
            if (_balanceProvider is not null)
            {
                _balanceProvider.Balance = _balance;
            }
        }
    }

    public IReadOnlyList<AudioOutputDeviceModel> GetOutputDevices()
    {
        try
        {
            using var defaultDevice = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var devices = new List<AudioOutputDeviceModel>();
            foreach (var endpoint in _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                using (endpoint)
                {
                    devices.Add(new AudioOutputDeviceModel(endpoint.ID, endpoint.FriendlyName, endpoint.ID == defaultDevice.ID));
                }
            }
            return devices
                .OrderByDescending(device => device.IsDefault)
                .ThenBy(device => device.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    public bool Open(string filePath)
    {
        DisposePlaybackPipeline();
        _currentFilePath = filePath;
        try
        {
            _reader = CreateReader(filePath);
            _equalizer = new EqualizerSampleProvider(_reader.ToSampleProvider());
            _equalizer.SetGains(_equalizerGains);
            _balanceProvider = new BalanceSampleProvider(_equalizer) { Balance = _balance };
            _volumeProvider = new VolumeSampleProvider(_balanceProvider) { Volume = (float)_volume };

            _playbackDevice = string.IsNullOrWhiteSpace(_outputDeviceId)
                ? _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
                : _deviceEnumerator.GetDevice(_outputDeviceId);
            _output = new WasapiOut(_playbackDevice, AudioClientShareMode.Shared, true, 100);
            _output.PlaybackStopped += OnPlaybackStopped;
            _output.Init(_volumeProvider.ToWaveProvider());
            MediaOpened?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception exception)
        {
            DisposePlaybackPipeline();
            MediaFailed?.Invoke(this, exception);
            return false;
        }
    }

    public void Play()
    {
        try
        {
            _output?.Play();
        }
        catch (Exception exception)
        {
            MediaFailed?.Invoke(this, exception);
        }
    }

    public void Pause() => _output?.Pause();

    public void Stop()
    {
        _manualStop = true;
        _output?.Stop();
        Position = TimeSpan.Zero;
        _manualStop = false;
    }

    public void SetEqualizerProfile(string profileName)
    {
        _equalizerGains = EqualizerProfiles.GetGains(profileName);
        _equalizer?.SetGains(_equalizerGains);
    }

    public bool SetOutputDevice(string? deviceId)
    {
        if (string.Equals(_outputDeviceId, deviceId, StringComparison.Ordinal))
        {
            return true;
        }

        var path = _currentFilePath;
        var position = Position;
        var wasPlaying = _output?.PlaybackState == PlaybackState.Playing;
        _outputDeviceId = deviceId;
        if (path is null)
        {
            return true;
        }

        if (!Open(path))
        {
            return false;
        }
        if (_reader is null)
        {
            return false;
        }
        Position = position;
        if (wasPlaying)
        {
            Play();
        }
        return true;
    }

    public void Dispose()
    {
        DisposePlaybackPipeline();
        _deviceEnumerator.Dispose();
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs args)
    {
        if (_manualStop)
        {
            return;
        }
        if (args.Exception is not null)
        {
            Dispatch(() => MediaFailed?.Invoke(this, args.Exception));
            return;
        }
        if (_reader is not null && _reader.Position >= _reader.Length)
        {
            Dispatch(() => MediaEnded?.Invoke(this, EventArgs.Empty));
        }
    }

    private void DisposePlaybackPipeline()
    {
        if (_output is not null)
        {
            _manualStop = true;
            _output.PlaybackStopped -= OnPlaybackStopped;
            _output.Stop();
            _output.Dispose();
            _manualStop = false;
        }
        _output = null;
        _playbackDevice?.Dispose();
        _playbackDevice = null;
        _reader?.Dispose();
        _reader = null;
        _equalizer = null;
        _balanceProvider = null;
        _volumeProvider = null;
    }

    private static WaveStream CreateReader(string filePath) => Path.GetExtension(filePath).ToLowerInvariant() switch
    {
        ".wav" => new WaveFileReader(filePath),
        ".mp3" => new Mp3FileReader(filePath),
        ".aif" or ".aiff" => new AiffFileReader(filePath),
        _ => new MediaFoundationReader(filePath)
    };

    private static void Dispatch(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.BeginInvoke(action);
        }
    }
}
