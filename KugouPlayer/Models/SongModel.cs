using CommunityToolkit.Mvvm.ComponentModel;

namespace KugouPlayer.Models;

public partial class SongModel : ObservableObject
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    [ObservableProperty] private string _songName = string.Empty;
    [ObservableProperty] private string _singer = string.Empty;
    [ObservableProperty] private string _album = string.Empty;
    [ObservableProperty] private string? _coverImage;
    [ObservableProperty] private byte[]? _coverData;
    [ObservableProperty] private string? _filePath;
    [ObservableProperty] private TimeSpan _duration;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private bool _isFavorite;
    [ObservableProperty] private int _playCount;

    public string AccentColor { get; init; } = "#5B8FF9";
    public string DurationText => Duration == TimeSpan.Zero ? "--:--" : Duration.ToString(@"mm\:ss");
    public string DisplayArtist => string.IsNullOrWhiteSpace(Singer) ? "未知歌手" : Singer;

    partial void OnDurationChanged(TimeSpan value) => OnPropertyChanged(nameof(DurationText));
    partial void OnSingerChanged(string value) => OnPropertyChanged(nameof(DisplayArtist));
}
