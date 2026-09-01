using KugouPlayer.Models;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace KugouPlayer.Services;

public sealed class UpdateService : IDisposable
{
    public const string ProjectHome = "https://github.com/NobiNobita-GC/KugouPlayer";
    private const string LatestReleaseEndpoint = "https://api.github.com/repos/NobiNobita-GC/KugouPlayer/releases/latest";
    private readonly HttpClient _httpClient;

    public UpdateService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("KugouPlayer", "1.0"));
        }
    }

    public async Task<UpdateCheckResult> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(LatestReleaseEndpoint, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new UpdateCheckResult(false, currentVersion.ToString(3), "当前仓库尚未发布正式版本");
        }
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var tag = root.TryGetProperty("tag_name", out var tagProperty) ? tagProperty.GetString() : null;
        var pageText = root.TryGetProperty("html_url", out var pageProperty) ? pageProperty.GetString() : null;
        var latestVersion = ParseVersion(tag) ?? currentVersion;
        var releasePage = Uri.TryCreate(pageText, UriKind.Absolute, out var page) ? page : null;
        var isAvailable = latestVersion > currentVersion;
        return new UpdateCheckResult(
            isAvailable,
            latestVersion.ToString(3),
            isAvailable ? $"发现新版本 {latestVersion.ToString(3)}" : "当前已经是最新版本",
            releasePage);
    }

    public void Dispose() => _httpClient.Dispose();

    private static Version? ParseVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }
        var normalized = tag.Trim().TrimStart('v', 'V').Split('-', '+')[0];
        return Version.TryParse(normalized, out var version) ? version : null;
    }
}
