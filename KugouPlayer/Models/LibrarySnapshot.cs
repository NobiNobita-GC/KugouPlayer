namespace KugouPlayer.Models;

public sealed class LibrarySnapshot
{
    public List<StoredSong> LocalSongs { get; set; } = [];
    public List<string> FavoriteKeys { get; set; } = [];
    public List<string> RecentKeys { get; set; } = [];
    public List<StoredPlaylist> UserPlaylists { get; set; } = [];
    public List<string> SearchHistory { get; set; } = [];
    public double Volume { get; set; } = 68;
    public PlaybackMode PlaybackMode { get; set; }
    public string ThemeMode { get; set; } = "浅色";
    public bool AutoPlayOnStartup { get; set; }
    public bool ResumeLastPosition { get; set; } = true;
    public bool DesktopLyricsTopmost { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public bool AutoCheckUpdates { get; set; } = true;
    public string EqualizerPreset { get; set; } = "关闭";
    public double ChannelBalance { get; set; }
    public string AudioOutputDevice { get; set; } = string.Empty;
    public string? LastSongKey { get; set; }
    public double LastPositionSeconds { get; set; }
}

public sealed class StoredPlaylist
{
    public required string Id { get; set; }
    public required string Title { get; set; }
    public List<string> SongKeys { get; set; } = [];
}

public sealed class StoredSong
{
    public required string Id { get; set; }
    public required string SongName { get; set; }
    public required string Singer { get; set; }
    public required string Album { get; set; }
    public required string FilePath { get; set; }
    public required string AccentColor { get; set; }
    public double DurationSeconds { get; set; }
    public int PlayCount { get; set; }
}
