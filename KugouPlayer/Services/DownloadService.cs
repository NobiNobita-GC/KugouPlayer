using KugouPlayer.Models;
using System.IO;
using System.Net.Http;

namespace KugouPlayer.Services;

public sealed class DownloadService : IDisposable
{
    private readonly HttpClient _httpClient = new() { Timeout = Timeout.InfiniteTimeSpan };

    public async Task DownloadAsync(DownloadTaskModel task, IProgress<double> progress, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(task.SourceUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        var temporaryPath = task.DestinationPath + ".download";
        Directory.CreateDirectory(Path.GetDirectoryName(task.DestinationPath)!);

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
        var buffer = new byte[81920];
        long downloadedBytes = 0;
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            downloadedBytes += bytesRead;
            if (totalBytes > 0)
            {
                progress.Report(downloadedBytes * 100d / totalBytes.Value);
            }
        }

        await destination.FlushAsync(cancellationToken);
        File.Move(temporaryPath, task.DestinationPath, true);
        progress.Report(100);
    }

    public void Dispose() => _httpClient.Dispose();
}
