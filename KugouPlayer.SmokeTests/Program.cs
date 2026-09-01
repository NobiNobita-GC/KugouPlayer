using KugouPlayer.Models;
using KugouPlayer.Services;
using NAudio.Wave.SampleProviders;
using System.Net;
using System.Net.Http;
using System.Text.Json;

var checks = new List<(string Name, Action Run)>
{
    ("LRC parses fractions and sorts timestamps", () =>
    {
        var result = LrcParser.ParseText("[01:02.5]后一句\n[00:03.25][00:05.250]同一句\n[ar:歌手]\n");
        Ensure(result.Count == 3, "expected three timestamped lines");
        Ensure(result[0].Timestamp == TimeSpan.FromMilliseconds(3250), "two-digit fraction should mean 250ms");
        Ensure(result[1].Timestamp == TimeSpan.FromMilliseconds(5250), "three-digit fraction should mean 250ms");
        Ensure(result[2].Timestamp == TimeSpan.FromMilliseconds(62500), "one-digit fraction should mean 500ms");
        Ensure(result[0].Text == "同一句" && result[1].Text == "同一句", "multiple timestamps should share lyric text");
    }),
    ("LRC missing file is safe", () =>
    {
        Ensure(LrcParser.ParseFile(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".lrc")).Count == 0,
            "missing lyric file should return an empty list");
    }),
    ("Library settings survive JSON round-trip", () =>
    {
        var source = new LibrarySnapshot
        {
            ThemeMode = "深色",
            Volume = 42,
            PlaybackMode = PlaybackMode.Shuffle,
            SearchHistory = ["晚风", "纯音乐"],
            ChannelBalance = -0.25,
            MinimizeToTray = true
        };
        var restored = JsonSerializer.Deserialize<LibrarySnapshot>(JsonSerializer.Serialize(source));
        Ensure(restored is not null, "snapshot should deserialize");
        Ensure(restored!.ThemeMode == "深色" && restored.Volume == 42, "theme and volume should persist");
        Ensure(restored.PlaybackMode == PlaybackMode.Shuffle && restored.SearchHistory.SequenceEqual(source.SearchHistory),
            "playback mode and search history should persist");
        Ensure(restored.ChannelBalance == -0.25 && restored.MinimizeToTray, "system settings should persist");
    }),
    ("Equalizer changes samples without clipping", () =>
    {
        var drySource = new SignalGenerator(44100, 2) { Frequency = 125, Gain = 0.1, Type = SignalGeneratorType.Sin };
        var wetSource = new SignalGenerator(44100, 2) { Frequency = 125, Gain = 0.1, Type = SignalGeneratorType.Sin };
        var equalizer = new EqualizerSampleProvider(wetSource);
        equalizer.SetGains(EqualizerProfiles.GetGains("低音增强"));
        var dry = new float[4096];
        var wet = new float[4096];
        drySource.Read(dry, 0, dry.Length);
        equalizer.Read(wet, 0, wet.Length);
        Ensure(dry.Zip(wet).Any(pair => Math.Abs(pair.First - pair.Second) > 0.0001f), "enabled EQ should alter samples");
        Ensure(wet.All(sample => float.IsFinite(sample) && Math.Abs(sample) <= 1), "EQ output should remain finite and clipped safely");
    }),
    ("Balance attenuates the opposite stereo channel", () =>
    {
        var source = new SignalGenerator(44100, 2) { Frequency = 440, Gain = 0.2, Type = SignalGeneratorType.Sin };
        var balance = new BalanceSampleProvider(source) { Balance = -1 };
        var samples = new float[2048];
        balance.Read(samples, 0, samples.Length);
        var leftPeak = samples.Where((_, index) => index % 2 == 0).Max(Math.Abs);
        var rightPeak = samples.Where((_, index) => index % 2 == 1).Max(Math.Abs);
        Ensure(leftPeak > 0.1f, "left channel should remain audible");
        Ensure(rightPeak < 0.0001f, "full-left balance should mute the right channel");
    }),
    ("Update service compares semantic release versions", () =>
    {
        const string responseJson = """{"tag_name":"v1.2.0","html_url":"https://github.com/NobiNobita-GC/KugouPlayer/releases/tag/v1.2.0"}""";
        using var client = new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, responseJson));
        using var service = new UpdateService(client);
        var result = service.CheckAsync(new Version(1, 0, 0)).GetAwaiter().GetResult();
        Ensure(result.IsUpdateAvailable, "newer release should be reported");
        Ensure(result.LatestVersion == "1.2.0", "release tag should be normalized");
        Ensure(result.ReleasePage?.Scheme == Uri.UriSchemeHttps, "release link should remain HTTPS");
    }),
    ("Windows exposes an active WASAPI output device", () =>
    {
        using var player = new AudioPlayerService();
        var devices = player.GetOutputDevices();
        Ensure(devices.Count > 0, "at least one active render endpoint is required");
        Ensure(devices.Select(device => device.Id).Distinct(StringComparer.Ordinal).Count() == devices.Count,
            "audio endpoint IDs should be unique");
    })
};

var failures = 0;
foreach (var check in checks)
{
    try
    {
        check.Run();
        Console.WriteLine($"PASS  {check.Name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.WriteLine($"FAIL  {check.Name}: {exception.Message}");
    }
}

Console.WriteLine($"{checks.Count - failures}/{checks.Count} checks passed");
return failures == 0 ? 0 : 1;

static void Ensure(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed class StubHttpMessageHandler(HttpStatusCode statusCode, string content) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(statusCode) { Content = new StringContent(content) });
}
