using CommunityToolkit.Mvvm.ComponentModel;

namespace KugouPlayer.Models;

public enum DownloadTaskStatus
{
    Waiting,
    Downloading,
    Paused,
    Completed,
    Failed
}

public partial class DownloadTaskModel : ObservableObject
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public required string Title { get; init; }
    public required string SourceUrl { get; init; }
    public required string DestinationPath { get; init; }

    [ObservableProperty] private double _progress;
    [ObservableProperty] private DownloadTaskStatus _status;
    [ObservableProperty] private string _statusMessage = "等待下载";

    internal CancellationTokenSource? Cancellation { get; set; }
}

