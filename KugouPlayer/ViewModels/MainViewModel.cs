using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KugouPlayer.Models;
using KugouPlayer.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Data;
using System.Windows.Threading;

namespace KugouPlayer.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly AudioPlayerService _audioPlayer = new();
    private readonly LibraryStateService _libraryState = new();
    private readonly AudioMetadataService _metadataService = new();
    private readonly DownloadService _downloadService = new();
    private readonly UpdateService _updateService = new();
    private readonly DispatcherTimer _progressTimer;
    private readonly DispatcherTimer _statusTimer;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private PageKind _currentPage = PageKind.Home;
    [ObservableProperty] private SongModel? _currentSong;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private bool _isQueueOpen;
    [ObservableProperty] private bool _isPlayerDetailOpen;
    [ObservableProperty] private bool _isStatusVisible;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private double _positionSeconds;
    [ObservableProperty] private double _durationSeconds;
    [ObservableProperty] private double _volume = 68;
    [ObservableProperty] private PlaybackMode _playbackMode;
    [ObservableProperty] private LyricLine? _currentLyricLine;
    [ObservableProperty] private bool _isMuted;
    [ObservableProperty] private PlaylistModel? _selectedPlaylist;
    [ObservableProperty] private bool _isPlaylistEditorOpen;
    [ObservableProperty] private string _playlistEditorTitle = string.Empty;
    [ObservableProperty] private PlaylistModel? _editingPlaylist;
    [ObservableProperty] private string _localFilterText = string.Empty;
    [ObservableProperty] private string _localSortMode = "添加顺序";
    [ObservableProperty] private bool _isDownloadEditorOpen;
    [ObservableProperty] private string _downloadUrl = string.Empty;
    [ObservableProperty] private string _downloadFileName = string.Empty;
    [ObservableProperty] private string _selectedExploreCategory = "精选";
    [ObservableProperty] private string _themeMode = "浅色";
    [ObservableProperty] private bool _autoPlayOnStartup;
    [ObservableProperty] private bool _resumeLastPosition = true;
    [ObservableProperty] private bool _desktopLyricsTopmost = true;
    [ObservableProperty] private bool _minimizeToTray = true;
    [ObservableProperty] private bool _autoCheckUpdates = true;
    [ObservableProperty] private string _equalizerPreset = "关闭";
    [ObservableProperty] private double _channelBalance;
    [ObservableProperty] private string _audioOutputDevice = string.Empty;
    [ObservableProperty] private bool _isCheckingForUpdates;
    [ObservableProperty] private bool _isUpdateAvailable;
    [ObservableProperty] private string _updateStatus = "尚未检查更新";
    [ObservableProperty] private string? _latestReleaseUrl;
    private double _volumeBeforeMute = 68;
    private double _pendingResumeSeconds;
    private bool _isRestoringStartupSong;
    private bool _isLoadingState = true;

    public MainViewModel()
    {
        ExploreNavItems =
        [
            new() { MenuName = "为你推荐", IconGlyph = "\uE80F", Page = PageKind.Home, IsSelected = true },
            new() { MenuName = "乐库", IconGlyph = "\uE8F1", Page = PageKind.MusicLibrary },
            new() { MenuName = "歌单", IconGlyph = "\uE8FD", Page = PageKind.Playlists },
            new() { MenuName = "排行榜", IconGlyph = "\uE9D2", Page = PageKind.Charts },
            new() { MenuName = "频道", IconGlyph = "\uE789", Page = PageKind.Radio },
            new() { MenuName = "视频", IconGlyph = "\uE714", Page = PageKind.Videos },
            new() { MenuName = "有声书", IconGlyph = "\uE82D", Page = PageKind.Audiobooks }
        ];

        MyMusicNavItems =
        [
            new() { MenuName = "我喜欢", IconGlyph = "\uEB51", Page = PageKind.Favorites },
            new() { MenuName = "本地与下载", IconGlyph = "\uE896", Page = PageKind.LocalMusic },
            new() { MenuName = "最近播放", IconGlyph = "\uE823", Page = PageKind.Recent },
            new() { MenuName = "下载管理", IconGlyph = "\uE896", Page = PageKind.Downloads },
            new() { MenuName = "音乐云盘", IconGlyph = "\uE753", Page = PageKind.CloudDrive }
        ];

        SeedDemoData();
        ConfigureExplorePage();
        LoadAudioOutputDevices();
        LoadLibraryState();
        LocalSongsView = CollectionViewSource.GetDefaultView(LocalSongs);
        LocalSongsView.Filter = FilterLocalSong;

        _audioPlayer.Volume = Volume / 100;
        _audioPlayer.MediaOpened += OnMediaOpened;
        _audioPlayer.MediaEnded += OnMediaEnded;
        _audioPlayer.MediaFailed += OnMediaFailed;
        if (CurrentSong?.FilePath is not null && File.Exists(CurrentSong.FilePath))
        {
            _isRestoringStartupSong = true;
            if (!_audioPlayer.Open(CurrentSong.FilePath))
            {
                _isRestoringStartupSong = false;
            }
        }

        _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _progressTimer.Tick += (_, _) => RefreshPlaybackProgress();
        _progressTimer.Start();

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _statusTimer.Tick += (_, _) =>
        {
            _statusTimer.Stop();
            IsStatusVisible = false;
        };
        if (AutoCheckUpdates)
        {
            _ = CheckForUpdatesCore(false);
        }
    }

    public ObservableCollection<NavMenuItem> ExploreNavItems { get; }
    public ObservableCollection<NavMenuItem> MyMusicNavItems { get; }
    public ObservableCollection<SongModel> FeaturedSongs { get; } = [];
    public ObservableCollection<SongModel> LocalSongs { get; } = [];
    public ObservableCollection<SongModel> RecentSongs { get; } = [];
    public ObservableCollection<SongModel> FavoriteSongs { get; } = [];
    public ObservableCollection<SongModel> QueueSongs { get; } = [];
    public ObservableCollection<SongModel> SearchResults { get; } = [];
    public ObservableCollection<string> SearchHistory { get; } = [];
    public ObservableCollection<PlaylistModel> RecommendedPlaylists { get; } = [];
    public ObservableCollection<PlaylistModel> UserPlaylists { get; } = [];
    public ObservableCollection<PlaylistModel> MusicLibraryCollections { get; } = [];
    public ObservableCollection<PlaylistModel> ChartCollections { get; } = [];
    public ObservableCollection<PlaylistModel> RadioCollections { get; } = [];
    public ObservableCollection<PlaylistModel> VideoCollections { get; } = [];
    public ObservableCollection<PlaylistModel> AudiobookCollections { get; } = [];
    public ObservableCollection<PlaylistModel> VisibleExplorePlaylists { get; } = [];
    public ObservableCollection<string> ExploreCategories { get; } = [];
    public ObservableCollection<LyricLine> Lyrics { get; } = [];
    public ObservableCollection<DownloadTaskModel> DownloadTasks { get; } = [];
    public ICollectionView LocalSongsView { get; }
    public IReadOnlyList<string> LocalSortOptions { get; } = ["添加顺序", "歌曲名", "歌手", "时长"];
    public IReadOnlyList<string> HotSearchTerms { get; } = ["新歌首发", "华语流行", "轻音乐", "夜晚氛围", "经典老歌", "治愈系"];
    public IReadOnlyList<string> ThemeOptions { get; } = ["浅色", "深色"];
    public IReadOnlyList<string> EqualizerPresets { get; } = EqualizerProfiles.Names;
    public ObservableCollection<AudioOutputDeviceModel> AudioOutputDevices { get; } = [];
    public string DownloadDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "KugouPlayer");
    public bool HasDownloadTasks => DownloadTasks.Count > 0;
    public string CurrentExploreDescription => CurrentPage switch
    {
        PageKind.MusicLibrary => "发现新歌、专辑与歌手，按类型快速浏览",
        PageKind.Playlists => "编辑精选与属于你的私人歌单",
        PageKind.Charts => "实时热度、飙升趋势与经典榜单",
        PageKind.Radio => "按心情和场景收听连续音乐频道",
        PageKind.Videos => "浏览音乐现场，也可播放电脑中的视频",
        PageKind.Audiobooks => "小说、知识、播客与助眠节目",
        _ => "发现更多好内容"
    };
    public bool IsVideoPage => CurrentPage == PageKind.Videos;
    public bool IsPlaylistPage => CurrentPage == PageKind.Playlists;
    public Version CurrentVersion { get; } = typeof(MainViewModel).Assembly.GetName().Version ?? new Version(1, 0, 0);
    public string VersionText => $"版本 {CurrentVersion.ToString(3)}";
    public string RuntimeText => $"{RuntimeInformation.FrameworkDescription} · {RuntimeInformation.ProcessArchitecture}";

    public string CurrentPageTitle => CurrentPage switch
    {
        PageKind.Home => "音乐推荐",
        PageKind.MusicLibrary => "乐库",
        PageKind.Playlists => "精选歌单",
        PageKind.Charts => "排行榜",
        PageKind.Radio => "音乐频道",
        PageKind.Videos => "视频",
        PageKind.Audiobooks => "有声书",
        PageKind.Favorites => "我喜欢",
        PageKind.LocalMusic => "本地音乐",
        PageKind.Recent => "最近播放",
        PageKind.Downloads => "下载管理",
        PageKind.CloudDrive => "音乐云盘",
        PageKind.Search => $"“{SearchText}” 的搜索结果",
        PageKind.Settings => "设置",
        PageKind.About => "关于 KugouPlayer",
        _ => "酷狗音乐"
    };

    public string PositionText => TimeSpan.FromSeconds(Math.Max(0, PositionSeconds)).ToString(@"mm\:ss");
    public string DurationText => TimeSpan.FromSeconds(Math.Max(0, DurationSeconds)).ToString(@"mm\:ss");
    public double ProgressPercent => DurationSeconds <= 0 ? 0 : PositionSeconds / DurationSeconds * 100;
    public bool HasCurrentSong => CurrentSong is not null;
    public bool HasLocalSongs => LocalSongs.Count > 0;
    public bool HasSearchResults => SearchResults.Count > 0;
    public bool HasSearchQuery => !string.IsNullOrWhiteSpace(SearchText);
    public bool HasSearchHistory => SearchHistory.Count > 0;
    public string SearchSummary => $"歌曲 {SearchResults.Count}";
    public bool HasLyrics => Lyrics.Count > 0;
    public string PlaybackModeGlyph => PlaybackMode switch
    {
        PlaybackMode.RepeatOne => "\uE8ED",
        PlaybackMode.Shuffle => "\uE8B1",
        _ => "\uE8EE"
    };
    public string PlaybackModeTitle => PlaybackMode switch
    {
        PlaybackMode.RepeatOne => "单曲循环",
        PlaybackMode.Shuffle => "随机播放",
        _ => "顺序播放"
    };
    public bool IsPlaylistDetailOpen => SelectedPlaylist is not null;
    public string PlaylistEditorHeader => EditingPlaylist is null ? "新建歌单" : "重命名歌单";

    partial void OnCurrentPageChanged(PageKind value)
    {
        foreach (var item in ExploreNavItems.Concat(MyMusicNavItems))
        {
            item.IsSelected = item.Page == value;
        }

        OnPropertyChanged(nameof(CurrentPageTitle));
        OnPropertyChanged(nameof(CurrentExploreDescription));
        OnPropertyChanged(nameof(IsVideoPage));
        OnPropertyChanged(nameof(IsPlaylistPage));
        ConfigureExplorePage();
    }

    partial void OnSearchTextChanged(string value)
    {
        SearchResults.Clear();
        OnPropertyChanged(nameof(HasSearchQuery));
        if (string.IsNullOrWhiteSpace(value))
        {
            OnPropertyChanged(nameof(HasSearchResults));
            OnPropertyChanged(nameof(SearchSummary));
            return;
        }

        var keyword = value.Trim();
        foreach (var song in FeaturedSongs.Concat(LocalSongs).DistinctBy(song => song.Id).Where(song =>
                     song.SongName.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) ||
                     song.Singer.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) ||
                     song.Album.Contains(keyword, StringComparison.CurrentCultureIgnoreCase)))
        {
            SearchResults.Add(song);
        }

        CurrentPage = PageKind.Search;
        OnPropertyChanged(nameof(CurrentPageTitle));
        OnPropertyChanged(nameof(HasSearchResults));
        OnPropertyChanged(nameof(SearchSummary));
    }

    [RelayCommand]
    private void Search()
    {
        var keyword = SearchText.Trim();
        CurrentPage = PageKind.Search;
        if (string.IsNullOrWhiteSpace(keyword))
        {
            OnPropertyChanged(nameof(CurrentPageTitle));
            return;
        }

        var existing = SearchHistory.FirstOrDefault(item => string.Equals(item, keyword, StringComparison.CurrentCultureIgnoreCase));
        if (existing is not null)
        {
            SearchHistory.Remove(existing);
        }
        SearchHistory.Insert(0, keyword);
        while (SearchHistory.Count > 10)
        {
            SearchHistory.RemoveAt(SearchHistory.Count - 1);
        }
        OnPropertyChanged(nameof(HasSearchHistory));
        SaveLibraryState();
    }

    [RelayCommand]
    private void UseSearchTerm(string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return;
        }
        SearchText = term;
        Search();
    }

    [RelayCommand]
    private void RemoveSearchHistory(string? term)
    {
        if (term is null || !SearchHistory.Remove(term))
        {
            return;
        }
        OnPropertyChanged(nameof(HasSearchHistory));
        SaveLibraryState();
    }

    [RelayCommand]
    private void ClearSearchHistory()
    {
        SearchHistory.Clear();
        OnPropertyChanged(nameof(HasSearchHistory));
        SaveLibraryState();
    }

    partial void OnCurrentSongChanged(SongModel? value)
    {
        OnPropertyChanged(nameof(HasCurrentSong));
    }

    partial void OnPositionSecondsChanged(double value)
    {
        OnPropertyChanged(nameof(PositionText));
        OnPropertyChanged(nameof(ProgressPercent));
    }

    partial void OnDurationSecondsChanged(double value)
    {
        OnPropertyChanged(nameof(DurationText));
        OnPropertyChanged(nameof(ProgressPercent));
    }

    partial void OnVolumeChanged(double value) => _audioPlayer.Volume = value / 100;

    partial void OnPlaybackModeChanged(PlaybackMode value)
    {
        OnPropertyChanged(nameof(PlaybackModeGlyph));
        OnPropertyChanged(nameof(PlaybackModeTitle));
    }

    partial void OnSelectedPlaylistChanged(PlaylistModel? value) => OnPropertyChanged(nameof(IsPlaylistDetailOpen));

    partial void OnEditingPlaylistChanged(PlaylistModel? value) => OnPropertyChanged(nameof(PlaylistEditorHeader));

    partial void OnLocalFilterTextChanged(string value) => LocalSongsView.Refresh();

    partial void OnLocalSortModeChanged(string value)
    {
        LocalSongsView.SortDescriptions.Clear();
        var propertyName = value switch
        {
            "歌曲名" => nameof(SongModel.SongName),
            "歌手" => nameof(SongModel.Singer),
            "时长" => nameof(SongModel.Duration),
            _ => null
        };
        if (propertyName is not null)
        {
            LocalSongsView.SortDescriptions.Add(new SortDescription(propertyName, ListSortDirection.Ascending));
        }
    }

    partial void OnSelectedExploreCategoryChanged(string value) => RefreshExplorePlaylists();

    partial void OnThemeModeChanged(string value)
    {
        ThemeService.Apply(value);
        PersistSettings();
    }

    partial void OnAutoPlayOnStartupChanged(bool value) => PersistSettings();
    partial void OnResumeLastPositionChanged(bool value) => PersistSettings();
    partial void OnDesktopLyricsTopmostChanged(bool value) => PersistSettings();
    partial void OnMinimizeToTrayChanged(bool value) => PersistSettings();
    partial void OnAutoCheckUpdatesChanged(bool value)
    {
        PersistSettings();
        if (!_isLoadingState && value)
        {
            _ = CheckForUpdatesCore(false);
        }
    }
    partial void OnEqualizerPresetChanged(string value)
    {
        _audioPlayer.SetEqualizerProfile(value);
        PersistSettings();
    }

    partial void OnAudioOutputDeviceChanged(string value)
    {
        if (!_audioPlayer.SetOutputDevice(string.IsNullOrWhiteSpace(value) ? null : value))
        {
            if (!string.IsNullOrEmpty(value))
            {
                AudioOutputDevice = string.Empty;
            }
            return;
        }
        PersistSettings();
    }

    partial void OnChannelBalanceChanged(double value)
    {
        _audioPlayer.Balance = value;
        PersistSettings();
    }

    [RelayCommand]
    private void SelectExploreCategory(string? category)
    {
        if (!string.IsNullOrWhiteSpace(category))
        {
            SelectedExploreCategory = category;
        }
    }

    [RelayCommand]
    private void Navigate(NavMenuItem? item)
    {
        if (item is null)
        {
            return;
        }

        CurrentPage = item.Page;
        IsQueueOpen = false;
    }

    [RelayCommand]
    private void NavigateToPage(PageKind page)
    {
        CurrentPage = page;
        IsQueueOpen = false;
    }

    [RelayCommand]
    private void ImportLocalMusic()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择要导入的音乐",
            Multiselect = true,
            Filter = "音频文件|*.mp3;*.wav;*.wma;*.aac;*.m4a;*.flac;*.ogg|所有文件|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var added = 0;
        foreach (var filePath in dialog.FileNames)
        {
            if (LocalSongs.Any(song => string.Equals(song.FilePath, filePath, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (AddLocalFile(filePath))
            {
                added++;
            }
        }

        OnPropertyChanged(nameof(HasLocalSongs));
        CurrentPage = PageKind.LocalMusic;
        SaveLibraryState();
        ShowStatus(added == 0 ? "所选歌曲已经在本地音乐中" : $"已导入 {added} 首本地歌曲");
    }

    [RelayCommand]
    private void ImportMusicFolder()
    {
        var dialog = new OpenFolderDialog { Title = "选择音乐文件夹", Multiselect = false };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var extensions = new HashSet<string>([".mp3", ".wav", ".wma", ".aac", ".m4a", ".flac", ".ogg"], StringComparer.OrdinalIgnoreCase);
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        var added = 0;
        foreach (var filePath in Directory.EnumerateFiles(dialog.FolderName, "*.*", options).Where(path => extensions.Contains(Path.GetExtension(path))))
        {
            if (AddLocalFile(filePath))
            {
                added++;
            }
        }

        OnPropertyChanged(nameof(HasLocalSongs));
        SaveLibraryState();
        CurrentPage = PageKind.LocalMusic;
        ShowStatus(added == 0 ? "文件夹中没有发现新的音乐" : $"已扫描并导入 {added} 首歌曲");
    }

    [RelayCommand]
    private void RemoveLocalSong(SongModel? song)
    {
        if (song is null || !LocalSongs.Contains(song))
        {
            return;
        }

        if (ReferenceEquals(CurrentSong, song))
        {
            _audioPlayer.Stop();
            CurrentSong = null;
            IsPlaying = false;
            PositionSeconds = 0;
            DurationSeconds = 0;
        }
        LocalSongs.Remove(song);
        QueueSongs.Remove(song);
        FavoriteSongs.Remove(song);
        RecentSongs.Remove(song);
        foreach (var playlist in UserPlaylists)
        {
            playlist.Songs.Remove(song);
        }
        OnPropertyChanged(nameof(HasLocalSongs));
        SaveLibraryState();
        ShowStatus("已从本地资料库移除");
    }

    [RelayCommand]
    private void RevealLocalSong(SongModel? song)
    {
        if (song?.FilePath is null || !File.Exists(song.FilePath))
        {
            ShowStatus("文件不存在或已被移动");
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{song.FilePath}\"") { UseShellExecute = true });
    }

    [RelayCommand]
    private void PlaySong(SongModel? song)
    {
        if (song is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(song.FilePath) || !File.Exists(song.FilePath))
        {
            CurrentSong = song;
            ShowStatus("这是推荐内容预览；导入本地音频后即可完整播放");
            return;
        }

        foreach (var item in QueueSongs)
        {
            item.IsPlaying = false;
        }

        CurrentSong = song;
        song.IsPlaying = true;
        if (!QueueSongs.Contains(song))
        {
            QueueSongs.Add(song);
        }

        MoveToRecent(song);
        LoadLyrics(song);
        if (!_audioPlayer.Open(song.FilePath))
        {
            song.IsPlaying = false;
            IsPlaying = false;
            return;
        }
        _audioPlayer.Play();
        IsPlaying = true;
    }

    [RelayCommand]
    private void TogglePlay()
    {
        if (CurrentSong is null)
        {
            var firstPlayable = LocalSongs.FirstOrDefault();
            if (firstPlayable is null)
            {
                ImportLocalMusic();
                return;
            }

            PlaySong(firstPlayable);
            return;
        }

        if (string.IsNullOrWhiteSpace(CurrentSong.FilePath) || !File.Exists(CurrentSong.FilePath))
        {
            ShowStatus("当前为推荐内容预览，请先导入本地音频");
            return;
        }

        if (IsPlaying)
        {
            _audioPlayer.Pause();
            IsPlaying = false;
            CurrentSong.IsPlaying = false;
        }
        else
        {
            _audioPlayer.Play();
            IsPlaying = true;
            CurrentSong.IsPlaying = true;
        }
    }

    [RelayCommand]
    private void PreviousSong() => PlayRelativeSong(-1);

    [RelayCommand]
    private void NextSong() => PlayRelativeSong(1);

    [RelayCommand]
    private void ToggleFavorite(SongModel? song)
    {
        song ??= CurrentSong;
        if (song is null)
        {
            return;
        }

        song.IsFavorite = !song.IsFavorite;
        if (song.IsFavorite)
        {
            if (!FavoriteSongs.Contains(song))
            {
                FavoriteSongs.Insert(0, song);
            }
            ShowStatus("已添加到我喜欢");
        }
        else
        {
            FavoriteSongs.Remove(song);
            ShowStatus("已从我喜欢移除");
        }

        SaveLibraryState();
    }

    [RelayCommand]
    private void PlayAllLocal()
    {
        if (LocalSongs.Count == 0)
        {
            ImportLocalMusic();
            return;
        }

        QueueSongs.Clear();
        foreach (var song in LocalSongs)
        {
            QueueSongs.Add(song);
        }
        PlaySong(LocalSongs[0]);
    }

    [RelayCommand]
    private void PlayAllFavorites()
    {
        if (FavoriteSongs.Count == 0)
        {
            ShowStatus("还没有收藏歌曲");
            return;
        }

        QueueSongs.Clear();
        foreach (var song in FavoriteSongs)
        {
            QueueSongs.Add(song);
        }
        PlaySong(FavoriteSongs[0]);
    }

    [RelayCommand]
    private void RemoveFromQueue(SongModel? song)
    {
        if (song is null)
        {
            return;
        }

        QueueSongs.Remove(song);
    }

    [RelayCommand]
    private void ClearQueue()
    {
        QueueSongs.Clear();
        ShowStatus("播放列表已清空");
    }

    [RelayCommand]
    private void ToggleQueue() => IsQueueOpen = !IsQueueOpen;

    [RelayCommand]
    private void TogglePlayerDetail() => IsPlayerDetailOpen = !IsPlayerDetailOpen;

    [RelayCommand]
    private void CyclePlaybackMode()
    {
        PlaybackMode = PlaybackMode switch
        {
            PlaybackMode.Sequence => PlaybackMode.RepeatOne,
            PlaybackMode.RepeatOne => PlaybackMode.Shuffle,
            _ => PlaybackMode.Sequence
        };
        SaveLibraryState();
        ShowStatus(PlaybackModeTitle);
    }

    [RelayCommand]
    private void ToggleMute()
    {
        if (IsMuted)
        {
            Volume = _volumeBeforeMute <= 0 ? 68 : _volumeBeforeMute;
            IsMuted = false;
        }
        else
        {
            _volumeBeforeMute = Volume;
            Volume = 0;
            IsMuted = true;
        }
    }

    [RelayCommand]
    private void ShowSettings() => CurrentPage = PageKind.Settings;

    [RelayCommand]
    private void ShowAbout() => CurrentPage = PageKind.About;

    [RelayCommand]
    private Task CheckForUpdates() => CheckForUpdatesCore(true);

    [RelayCommand]
    private void OpenProjectHome() => OpenWebPage(UpdateService.ProjectHome);

    [RelayCommand]
    private void OpenLatestRelease()
    {
        if (!string.IsNullOrWhiteSpace(LatestReleaseUrl))
        {
            OpenWebPage(LatestReleaseUrl);
        }
    }

    [RelayCommand]
    private void OpenPlaylist(PlaylistModel? playlist)
    {
        if (playlist is null)
        {
            return;
        }

        SelectedPlaylist = playlist;
        CurrentPage = PageKind.Playlists;
    }

    [RelayCommand]
    private void ClosePlaylistDetail() => SelectedPlaylist = null;

    [RelayCommand]
    private void PlayPlaylist(PlaylistModel? playlist)
    {
        playlist ??= SelectedPlaylist;
        if (playlist is null || playlist.Songs.Count == 0)
        {
            return;
        }

        QueueSongs.Clear();
        foreach (var song in playlist.Songs)
        {
            QueueSongs.Add(song);
        }
        PlaySong(playlist.Songs[0]);
    }

    [RelayCommand]
    private void BeginCreatePlaylist()
    {
        EditingPlaylist = null;
        PlaylistEditorTitle = $"新建歌单 {UserPlaylists.Count + 1}";
        IsPlaylistEditorOpen = true;
    }

    [RelayCommand]
    private void BeginRenamePlaylist(PlaylistModel? playlist)
    {
        playlist ??= SelectedPlaylist;
        if (playlist is null || !playlist.IsUserCreated)
        {
            return;
        }

        EditingPlaylist = playlist;
        PlaylistEditorTitle = playlist.Title;
        IsPlaylistEditorOpen = true;
    }

    [RelayCommand]
    private void SavePlaylistEditor()
    {
        var title = PlaylistEditorTitle.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            ShowStatus("歌单名称不能为空");
            return;
        }

        if (EditingPlaylist is null)
        {
            var playlist = new PlaylistModel
            {
                Title = title,
                Subtitle = "我创建的歌单",
                AccentColor = PickAccent(UserPlaylists.Count),
                IsUserCreated = true
            };
            UserPlaylists.Add(playlist);
            RefreshExplorePlaylists();
            SelectedPlaylist = playlist;
            CurrentPage = PageKind.Playlists;
            ShowStatus("歌单已创建");
        }
        else
        {
            EditingPlaylist.Title = title;
            ShowStatus("歌单已重命名");
        }

        IsPlaylistEditorOpen = false;
        SaveLibraryState();
    }

    [RelayCommand]
    private void CancelPlaylistEditor()
    {
        IsPlaylistEditorOpen = false;
        EditingPlaylist = null;
    }

    [RelayCommand]
    private void DeletePlaylist(PlaylistModel? playlist)
    {
        playlist ??= SelectedPlaylist;
        if (playlist is null || !playlist.IsUserCreated)
        {
            return;
        }

        UserPlaylists.Remove(playlist);
        RefreshExplorePlaylists();
        if (ReferenceEquals(SelectedPlaylist, playlist))
        {
            SelectedPlaylist = null;
        }
        SaveLibraryState();
        ShowStatus("歌单已删除");
    }

    [RelayCommand]
    private void AddCurrentSongToSelectedPlaylist()
    {
        if (SelectedPlaylist is null || !SelectedPlaylist.IsUserCreated)
        {
            ShowStatus("请选择一个自己创建的歌单");
            return;
        }
        if (CurrentSong is null)
        {
            ShowStatus("当前没有正在播放的歌曲");
            return;
        }
        if (SelectedPlaylist.Songs.Contains(CurrentSong))
        {
            ShowStatus("歌曲已经在该歌单中");
            return;
        }

        SelectedPlaylist.Songs.Add(CurrentSong);
        SaveLibraryState();
        ShowStatus("已加入当前歌单");
    }

    [RelayCommand]
    private void ShowDownloadEditor()
    {
        DownloadUrl = string.Empty;
        DownloadFileName = string.Empty;
        IsDownloadEditorOpen = true;
    }

    [RelayCommand]
    private void CancelDownloadEditor() => IsDownloadEditorOpen = false;

    [RelayCommand]
    private async Task StartDownload()
    {
        if (!Uri.TryCreate(DownloadUrl.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            ShowStatus("请输入有效的 HTTP 或 HTTPS 地址");
            return;
        }

        var fileName = string.IsNullOrWhiteSpace(DownloadFileName)
            ? Path.GetFileName(Uri.UnescapeDataString(uri.AbsolutePath))
            : DownloadFileName.Trim();
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = $"music-{DateTime.Now:yyyyMMdd-HHmmss}.mp3";
        }
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidCharacter, '_');
        }

        var task = new DownloadTaskModel
        {
            Title = Path.GetFileNameWithoutExtension(fileName),
            SourceUrl = uri.AbsoluteUri,
            DestinationPath = Path.Combine(DownloadDirectory, fileName),
            Status = DownloadTaskStatus.Waiting
        };
        DownloadTasks.Insert(0, task);
        OnPropertyChanged(nameof(HasDownloadTasks));
        IsDownloadEditorOpen = false;
        await RunDownload(task);
    }

    [RelayCommand]
    private void PauseDownload(DownloadTaskModel? task)
    {
        if (task?.Status != DownloadTaskStatus.Downloading)
        {
            return;
        }
        task.Status = DownloadTaskStatus.Paused;
        task.StatusMessage = "已暂停";
        task.Cancellation?.Cancel();
    }

    [RelayCommand]
    private async Task ResumeDownload(DownloadTaskModel? task)
    {
        if (task is null || task.Status is DownloadTaskStatus.Downloading or DownloadTaskStatus.Completed)
        {
            return;
        }
        await RunDownload(task);
    }

    [RelayCommand]
    private void RemoveDownloadTask(DownloadTaskModel? task)
    {
        if (task is null)
        {
            return;
        }
        task.Cancellation?.Cancel();
        DownloadTasks.Remove(task);
        OnPropertyChanged(nameof(HasDownloadTasks));
        var temporaryPath = task.DestinationPath + ".download";
        if (File.Exists(temporaryPath))
        {
            File.Delete(temporaryPath);
        }
    }

    [RelayCommand]
    private void OpenDownloadDirectory()
    {
        Directory.CreateDirectory(DownloadDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", DownloadDirectory) { UseShellExecute = true });
    }

    public void SeekToPercent(double percent)
    {
        if (DurationSeconds <= 0)
        {
            return;
        }

        var clamped = Math.Clamp(percent, 0, 100);
        _audioPlayer.Position = TimeSpan.FromSeconds(DurationSeconds * clamped / 100);
        RefreshPlaybackProgress();
    }

    public void Dispose()
    {
        SaveLibraryState();
        foreach (var task in DownloadTasks)
        {
            task.Cancellation?.Cancel();
        }
        _progressTimer.Stop();
        _statusTimer.Stop();
        _audioPlayer.Dispose();
        _downloadService.Dispose();
        _updateService.Dispose();
    }

    private void OnMediaOpened(object? sender, EventArgs e)
    {
        DurationSeconds = _audioPlayer.Duration.TotalSeconds;
        if (ResumeLastPosition && _pendingResumeSeconds > 0)
        {
            _audioPlayer.Position = TimeSpan.FromSeconds(Math.Min(_pendingResumeSeconds, Math.Max(0, DurationSeconds - 1)));
            PositionSeconds = _audioPlayer.Position.TotalSeconds;
        }
        _pendingResumeSeconds = 0;
        if (_isRestoringStartupSong && AutoPlayOnStartup && CurrentSong is not null)
        {
            _audioPlayer.Play();
            CurrentSong.IsPlaying = true;
            IsPlaying = true;
        }
        _isRestoringStartupSong = false;
        if (CurrentSong is not null && CurrentSong.Duration == TimeSpan.Zero)
        {
            CurrentSong.Duration = _audioPlayer.Duration;
        }
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        if (PlaybackMode == PlaybackMode.RepeatOne)
        {
            _audioPlayer.Position = TimeSpan.Zero;
            _audioPlayer.Play();
            return;
        }

        if (PlaybackMode == PlaybackMode.Shuffle)
        {
            PlayRandomSong();
            return;
        }

        NextSong();
    }

    private void OnMediaFailed(object? sender, Exception exception)
    {
        IsPlaying = false;
        if (CurrentSong is not null)
        {
            CurrentSong.IsPlaying = false;
        }
        _isRestoringStartupSong = false;
        ShowStatus($"无法播放：{exception.Message}");
    }

    private void RefreshPlaybackProgress()
    {
        if (CurrentSong is null)
        {
            return;
        }

        PositionSeconds = _audioPlayer.Position.TotalSeconds;
        if (_audioPlayer.Duration > TimeSpan.Zero)
        {
            DurationSeconds = _audioPlayer.Duration.TotalSeconds;
        }

        UpdateActiveLyric(_audioPlayer.Position);
    }

    private void PlayRelativeSong(int offset)
    {
        var source = QueueSongs.Count > 0 ? QueueSongs : LocalSongs;
        if (source.Count == 0)
        {
            ShowStatus("播放列表为空");
            return;
        }

        var index = CurrentSong is null ? -1 : source.IndexOf(CurrentSong);
        index = (index + offset + source.Count) % source.Count;
        PlaySong(source[index]);
    }

    private void MoveToRecent(SongModel song)
    {
        song.PlayCount++;
        RecentSongs.Remove(song);
        RecentSongs.Insert(0, song);
        SaveLibraryState();
    }

    private void PlayRandomSong()
    {
        var source = QueueSongs.Count > 0 ? QueueSongs : LocalSongs;
        if (source.Count == 0)
        {
            return;
        }

        if (source.Count == 1)
        {
            PlaySong(source[0]);
            return;
        }

        var currentIndex = CurrentSong is null ? -1 : source.IndexOf(CurrentSong);
        var nextIndex = Random.Shared.Next(source.Count - 1);
        if (nextIndex >= currentIndex && currentIndex >= 0)
        {
            nextIndex++;
        }
        PlaySong(source[nextIndex]);
    }

    private void LoadLyrics(SongModel song)
    {
        Lyrics.Clear();
        CurrentLyricLine = null;
        if (string.IsNullOrWhiteSpace(song.FilePath))
        {
            OnPropertyChanged(nameof(HasLyrics));
            return;
        }

        var lyricPath = Path.ChangeExtension(song.FilePath, ".lrc");
        foreach (var lyricLine in LrcParser.ParseFile(lyricPath))
        {
            Lyrics.Add(lyricLine);
        }
        OnPropertyChanged(nameof(HasLyrics));
    }

    private void UpdateActiveLyric(TimeSpan position)
    {
        LyricLine? activeLine = null;
        foreach (var lyricLine in Lyrics)
        {
            if (lyricLine.Timestamp > position)
            {
                break;
            }
            activeLine = lyricLine;
        }

        if (ReferenceEquals(CurrentLyricLine, activeLine))
        {
            return;
        }

        if (CurrentLyricLine is not null)
        {
            CurrentLyricLine.IsActive = false;
        }
        CurrentLyricLine = activeLine;
        if (CurrentLyricLine is not null)
        {
            CurrentLyricLine.IsActive = true;
        }
    }

    private void ShowStatus(string message)
    {
        StatusText = message;
        IsStatusVisible = true;
        _statusTimer.Stop();
        _statusTimer.Start();
    }

    private async Task RunDownload(DownloadTaskModel task)
    {
        task.Cancellation?.Dispose();
        task.Cancellation = new CancellationTokenSource();
        task.Status = DownloadTaskStatus.Downloading;
        task.StatusMessage = "正在下载";
        var progress = new Progress<double>(value => task.Progress = value);
        try
        {
            await _downloadService.DownloadAsync(task, progress, task.Cancellation.Token);
            task.Status = DownloadTaskStatus.Completed;
            task.StatusMessage = "下载完成";
        }
        catch (OperationCanceledException)
        {
            if (task.Status != DownloadTaskStatus.Paused)
            {
                task.Status = DownloadTaskStatus.Paused;
                task.StatusMessage = "已暂停";
            }
        }
        catch (HttpRequestException exception)
        {
            task.Status = DownloadTaskStatus.Failed;
            task.StatusMessage = $"下载失败：{exception.Message}";
        }
        catch (IOException exception)
        {
            task.Status = DownloadTaskStatus.Failed;
            task.StatusMessage = $"写入失败：{exception.Message}";
        }
    }

    private static (string Artist, string Title) ParseFileName(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        var parts = name.Split(" - ", 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 ? (parts[0], parts[1]) : ("未知歌手", name);
    }

    private bool AddLocalFile(string filePath)
    {
        if (LocalSongs.Any(song => string.Equals(song.FilePath, filePath, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var (artist, title) = ParseFileName(filePath);
        var metadata = _metadataService.Read(filePath);
        LocalSongs.Add(new SongModel
        {
            SongName = string.IsNullOrWhiteSpace(metadata?.Title) ? title : metadata.Title,
            Singer = string.IsNullOrWhiteSpace(metadata?.Artist) ? artist : metadata.Artist,
            Album = string.IsNullOrWhiteSpace(metadata?.Album) ? "本地音乐" : metadata.Album,
            FilePath = filePath,
            Duration = metadata?.Duration ?? TimeSpan.Zero,
            CoverData = metadata?.CoverData,
            AccentColor = PickAccent(LocalSongs.Count)
        });
        return true;
    }

    private bool FilterLocalSong(object item)
    {
        if (item is not SongModel song || string.IsNullOrWhiteSpace(LocalFilterText))
        {
            return true;
        }

        var keyword = LocalFilterText.Trim();
        return song.SongName.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) ||
               song.Singer.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) ||
               song.Album.Contains(keyword, StringComparison.CurrentCultureIgnoreCase);
    }

    private static string PickAccent(int index)
    {
        string[] colors = ["#6C7CFF", "#00A8E8", "#FF7B7B", "#9B72F2", "#26B89A", "#F1A94E"];
        return colors[index % colors.Length];
    }

    private void ConfigureExplorePage()
    {
        ExploreCategories.Clear();
        var categories = CurrentPage switch
        {
            PageKind.MusicLibrary => new[] { "精选", "新歌", "华语", "欧美", "纯音乐" },
            PageKind.Playlists => new[] { "精选", "流行", "氛围", "经典", "我的" },
            PageKind.Charts => new[] { "精选", "热歌", "飙升", "新歌", "经典" },
            PageKind.Radio => new[] { "精选", "通勤", "学习", "运动", "睡前" },
            PageKind.Videos => new[] { "精选", "现场", "MV", "翻唱", "舞台" },
            PageKind.Audiobooks => new[] { "精选", "小说", "知识", "播客", "助眠" },
            _ => Array.Empty<string>()
        };
        foreach (var category in categories)
        {
            ExploreCategories.Add(category);
        }
        SelectedExploreCategory = categories.FirstOrDefault() ?? "精选";
        RefreshExplorePlaylists();
    }

    private void RefreshExplorePlaylists()
    {
        VisibleExplorePlaylists.Clear();
        IEnumerable<PlaylistModel> source = CurrentPage switch
        {
            PageKind.MusicLibrary => MusicLibraryCollections,
            PageKind.Playlists => RecommendedPlaylists.Concat(UserPlaylists),
            PageKind.Charts => ChartCollections,
            PageKind.Radio => RadioCollections,
            PageKind.Videos => VideoCollections,
            PageKind.Audiobooks => AudiobookCollections,
            _ => []
        };
        if (SelectedExploreCategory != "精选")
        {
            source = source.Where(item => item.Category == SelectedExploreCategory ||
                                          SelectedExploreCategory == "我的" && item.IsUserCreated);
        }
        foreach (var playlist in source)
        {
            VisibleExplorePlaylists.Add(playlist);
        }
    }

    private void LoadLibraryState()
    {
        var snapshot = _libraryState.Load();
        foreach (var stored in snapshot.LocalSongs.Where(song => File.Exists(song.FilePath)))
        {
            var metadata = _metadataService.Read(stored.FilePath);
            LocalSongs.Add(new SongModel
            {
                Id = stored.Id,
                SongName = stored.SongName,
                Singer = stored.Singer,
                Album = stored.Album,
                FilePath = stored.FilePath,
                AccentColor = stored.AccentColor,
                Duration = metadata?.Duration > TimeSpan.Zero ? metadata.Duration : TimeSpan.FromSeconds(stored.DurationSeconds),
                CoverData = metadata?.CoverData,
                PlayCount = stored.PlayCount
            });
        }

        var songsByKey = FeaturedSongs.Concat(LocalSongs).ToDictionary(GetSongKey, StringComparer.OrdinalIgnoreCase);
        foreach (var key in snapshot.FavoriteKeys)
        {
            if (songsByKey.TryGetValue(key, out var song) && !FavoriteSongs.Contains(song))
            {
                song.IsFavorite = true;
                FavoriteSongs.Add(song);
            }
        }
        foreach (var key in snapshot.RecentKeys)
        {
            if (songsByKey.TryGetValue(key, out var song) && !RecentSongs.Contains(song))
            {
                RecentSongs.Add(song);
            }
        }

        foreach (var storedPlaylist in snapshot.UserPlaylists)
        {
            var playlist = new PlaylistModel
            {
                Id = storedPlaylist.Id,
                Title = storedPlaylist.Title,
                Subtitle = "我创建的歌单",
                AccentColor = PickAccent(UserPlaylists.Count),
                IsUserCreated = true
            };
            foreach (var key in storedPlaylist.SongKeys)
            {
                if (songsByKey.TryGetValue(key, out var song) && !playlist.Songs.Contains(song))
                {
                    playlist.Songs.Add(song);
                }
            }
            UserPlaylists.Add(playlist);
        }

        foreach (var term in snapshot.SearchHistory.Where(term => !string.IsNullOrWhiteSpace(term)).Take(10))
        {
            SearchHistory.Add(term);
        }

        if (snapshot.LastSongKey is not null && songsByKey.TryGetValue(snapshot.LastSongKey, out var lastSong) &&
            lastSong.FilePath is not null && File.Exists(lastSong.FilePath))
        {
            CurrentSong = lastSong;
            _pendingResumeSeconds = Math.Max(0, snapshot.LastPositionSeconds);
            PositionSeconds = _pendingResumeSeconds;
            DurationSeconds = lastSong.Duration.TotalSeconds;
            LoadLyrics(lastSong);
        }

        Volume = Math.Clamp(snapshot.Volume, 0, 100);
        PlaybackMode = snapshot.PlaybackMode;
        ThemeMode = snapshot.ThemeMode is "浅色" or "深色" ? snapshot.ThemeMode : "浅色";
        AutoPlayOnStartup = snapshot.AutoPlayOnStartup;
        ResumeLastPosition = snapshot.ResumeLastPosition;
        DesktopLyricsTopmost = snapshot.DesktopLyricsTopmost;
        MinimizeToTray = snapshot.MinimizeToTray;
        AutoCheckUpdates = snapshot.AutoCheckUpdates;
        EqualizerPreset = EqualizerPresets.Contains(snapshot.EqualizerPreset) ? snapshot.EqualizerPreset : "关闭";
        ChannelBalance = Math.Clamp(snapshot.ChannelBalance, -1, 1);
        AudioOutputDevice = AudioOutputDevices.Any(device => device.Id == snapshot.AudioOutputDevice)
            ? snapshot.AudioOutputDevice
            : string.Empty;
        _isLoadingState = false;
        OnPropertyChanged(nameof(HasLocalSongs));
        OnPropertyChanged(nameof(HasSearchHistory));
    }

    private void SaveLibraryState()
    {
        _libraryState.Save(new LibrarySnapshot
        {
            LocalSongs = LocalSongs.Select(song => new StoredSong
            {
                Id = song.Id,
                SongName = song.SongName,
                Singer = song.Singer,
                Album = song.Album,
                FilePath = song.FilePath!,
                AccentColor = song.AccentColor,
                DurationSeconds = song.Duration.TotalSeconds,
                PlayCount = song.PlayCount
            }).ToList(),
            FavoriteKeys = FavoriteSongs.Select(GetSongKey).ToList(),
            RecentKeys = RecentSongs.Take(100).Select(GetSongKey).ToList(),
            UserPlaylists = UserPlaylists.Select(playlist => new StoredPlaylist
            {
                Id = playlist.Id,
                Title = playlist.Title,
                SongKeys = playlist.Songs.Select(GetSongKey).ToList()
            }).ToList(),
            SearchHistory = SearchHistory.ToList(),
            Volume = Volume,
            PlaybackMode = PlaybackMode,
            ThemeMode = ThemeMode,
            AutoPlayOnStartup = AutoPlayOnStartup,
            ResumeLastPosition = ResumeLastPosition,
            DesktopLyricsTopmost = DesktopLyricsTopmost,
            MinimizeToTray = MinimizeToTray,
            AutoCheckUpdates = AutoCheckUpdates,
            EqualizerPreset = EqualizerPreset,
            ChannelBalance = ChannelBalance,
            AudioOutputDevice = AudioOutputDevice,
            LastSongKey = CurrentSong is null ? null : GetSongKey(CurrentSong),
            LastPositionSeconds = PositionSeconds
        });
    }

    private void PersistSettings()
    {
        if (!_isLoadingState)
        {
            SaveLibraryState();
        }
    }

    private void LoadAudioOutputDevices()
    {
        AudioOutputDevices.Add(new AudioOutputDeviceModel(string.Empty, "系统默认", true));
        foreach (var device in _audioPlayer.GetOutputDevices())
        {
            AudioOutputDevices.Add(device with
            {
                DisplayName = device.IsDefault ? $"{device.DisplayName}（当前默认）" : device.DisplayName
            });
        }
    }

    private async Task CheckForUpdatesCore(bool showToast)
    {
        if (IsCheckingForUpdates)
        {
            return;
        }

        IsCheckingForUpdates = true;
        UpdateStatus = "正在检查更新…";
        try
        {
            var result = await _updateService.CheckAsync(CurrentVersion);
            IsUpdateAvailable = result.IsUpdateAvailable;
            LatestReleaseUrl = result.ReleasePage?.AbsoluteUri;
            UpdateStatus = result.Message;
        }
        catch (TaskCanceledException)
        {
            UpdateStatus = "更新检查超时，请稍后重试";
        }
        catch (HttpRequestException exception)
        {
            UpdateStatus = $"无法连接更新服务：{exception.Message}";
        }
        catch (JsonException)
        {
            UpdateStatus = "更新服务返回了无法识别的数据";
        }
        finally
        {
            IsCheckingForUpdates = false;
            if (showToast)
            {
                ShowStatus(UpdateStatus);
            }
        }
    }

    private static void OpenWebPage(string address) =>
        Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });

    private static string GetSongKey(SongModel song) => song.FilePath ?? $"{song.SongName}|{song.Singer}";

    private void SeedDemoData()
    {
        SongModel[] songs =
        [
            new() { Id = "demo-wanfeng", SongName = "晚风心里吹", Singer = "阿梨粤", Album = "晚风心里吹", Duration = TimeSpan.FromSeconds(189), AccentColor = "#637BFF", CoverImage = "/Assets/cover-neon-rain.png" },
            new() { Id = "demo-xiangyunduan", SongName = "向云端", Singer = "小霞 / 海洋Bo", Album = "向云端", Duration = TimeSpan.FromSeconds(251), AccentColor = "#4DB6AC", CoverImage = "/Assets/cover-summer-hills.png" },
            new() { Id = "demo-zheshi", SongName = "这世界那么多人", Singer = "莫文蔚", Album = "我要我们在一起", Duration = TimeSpan.FromSeconds(285), AccentColor = "#ED7B84", CoverImage = "/Assets/cover-silk-ripples.png" },
            new() { Id = "demo-ruyuan", SongName = "如愿", Singer = "王菲", Album = "我和我的父辈", Duration = TimeSpan.FromSeconds(265), AccentColor = "#8674D9", CoverImage = "/Assets/cover-neon-rain.png" },
            new() { Id = "demo-yilushenghua", SongName = "一路生花", Singer = "温奕心", Album = "一路生花", Duration = TimeSpan.FromSeconds(256), AccentColor = "#E8A74B", CoverImage = "/Assets/cover-summer-hills.png" },
            new() { Id = "demo-daoxiang", SongName = "稻香", Singer = "周杰伦", Album = "魔杰座", Duration = TimeSpan.FromSeconds(223), AccentColor = "#3D9F86", CoverImage = "/Assets/cover-silk-ripples.png" },
            new() { Id = "demo-pianzhang", SongName = "篇章", Singer = "张韶涵 / 王赫野", Album = "天赐的声音", Duration = TimeSpan.FromSeconds(211), AccentColor = "#E8759C", CoverImage = "/Assets/cover-silk-ripples.png" },
            new() { Id = "demo-guangnian", SongName = "光年之外", Singer = "G.E.M. 邓紫棋", Album = "光年之外", Duration = TimeSpan.FromSeconds(235), AccentColor = "#5969D8", CoverImage = "/Assets/cover-neon-rain.png" }
        ];

        foreach (var song in songs)
        {
            FeaturedSongs.Add(song);
        }

        RecommendedPlaylists.Add(new PlaylistModel { Title = "今日私享 · 只为你推荐", Subtitle = "熟悉旋律里的新鲜感", Category = "流行", PlayCount = "128.6万", AccentColor = "#5A67E8", CoverImage = "/Assets/cover-neon-rain.png" });
        RecommendedPlaylists.Add(new PlaylistModel { Title = "华语流行热歌精选", Subtitle = "最近大家都在听", Category = "流行", PlayCount = "96.2万", AccentColor = "#F17C8A", CoverImage = "/Assets/cover-silk-ripples.png" });
        RecommendedPlaylists.Add(new PlaylistModel { Title = "轻松通勤能量站", Subtitle = "清晨唤醒好心情", Category = "氛围", PlayCount = "76.8万", AccentColor = "#20A486", CoverImage = "/Assets/cover-summer-hills.png" });
        RecommendedPlaylists.Add(new PlaylistModel { Title = "夜晚独处氛围感", Subtitle = "安静听完这一首", Category = "氛围", PlayCount = "63.5万", AccentColor = "#7459C8", CoverImage = "/Assets/cover-neon-rain.png" });
        RecommendedPlaylists.Add(new PlaylistModel { Title = "经典老歌收藏夹", Subtitle = "总有一首让你想起从前", Category = "经典", PlayCount = "206.4万", AccentColor = "#D9913B", CoverImage = "/Assets/cover-silk-ripples.png" });

        for (var playlistIndex = 0; playlistIndex < RecommendedPlaylists.Count; playlistIndex++)
        {
            var playlist = RecommendedPlaylists[playlistIndex];
            for (var songIndex = 0; songIndex < 6; songIndex++)
            {
                playlist.Songs.Add(songs[(playlistIndex + songIndex) % songs.Length]);
            }
        }

        AddExploreCollection(MusicLibraryCollections, songs, "本周新歌速递", "新歌", 0, "#566BE8", "/Assets/cover-neon-rain.png");
        AddExploreCollection(MusicLibraryCollections, songs, "华语流行精选", "华语", 1, "#F17C8A", "/Assets/cover-silk-ripples.png");
        AddExploreCollection(MusicLibraryCollections, songs, "欧美旋律空间", "欧美", 2, "#2AA78D", "/Assets/cover-summer-hills.png");
        AddExploreCollection(MusicLibraryCollections, songs, "无词也动人的旋律", "纯音乐", 3, "#8068D8", "/Assets/cover-neon-rain.png");

        AddExploreCollection(ChartCollections, songs, "酷狗热歌榜", "热歌", 0, "#4F68F1", "/Assets/cover-neon-rain.png");
        AddExploreCollection(ChartCollections, songs, "酷狗飙升榜", "飙升", 2, "#F26E7E", "/Assets/cover-silk-ripples.png");
        AddExploreCollection(ChartCollections, songs, "华语新歌榜", "新歌", 4, "#21A387", "/Assets/cover-summer-hills.png");
        AddExploreCollection(ChartCollections, songs, "经典金曲榜", "经典", 6, "#C88A35", "/Assets/cover-silk-ripples.png");

        AddExploreCollection(RadioCollections, songs, "清晨通勤电台", "通勤", 1, "#27A9D8", "/Assets/cover-summer-hills.png");
        AddExploreCollection(RadioCollections, songs, "专注学习白噪声", "学习", 3, "#6B73E8", "/Assets/cover-neon-rain.png");
        AddExploreCollection(RadioCollections, songs, "运动能量补给站", "运动", 5, "#F07869", "/Assets/cover-silk-ripples.png");
        AddExploreCollection(RadioCollections, songs, "深夜安心陪伴", "睡前", 7, "#7C63C7", "/Assets/cover-neon-rain.png");

        AddExploreCollection(VideoCollections, songs, "热门音乐现场", "现场", 0, "#405BD1", "/Assets/cover-neon-rain.png");
        AddExploreCollection(VideoCollections, songs, "高画质 MV 精选", "MV", 2, "#D85D78", "/Assets/cover-silk-ripples.png");
        AddExploreCollection(VideoCollections, songs, "宝藏翻唱合辑", "翻唱", 4, "#1C9C85", "/Assets/cover-summer-hills.png");
        AddExploreCollection(VideoCollections, songs, "经典舞台回放", "舞台", 6, "#7960BE", "/Assets/cover-neon-rain.png");

        AddExploreCollection(AudiobookCollections, songs, "长篇小说剧场", "小说", 0, "#6B77D6", "/Assets/cover-neon-rain.png");
        AddExploreCollection(AudiobookCollections, songs, "每天学点新知识", "知识", 2, "#D97955", "/Assets/cover-silk-ripples.png");
        AddExploreCollection(AudiobookCollections, songs, "热门播客对谈", "播客", 4, "#2B9D88", "/Assets/cover-summer-hills.png");
        AddExploreCollection(AudiobookCollections, songs, "晚安助眠故事", "助眠", 6, "#7765C8", "/Assets/cover-neon-rain.png");
    }

    private static void AddExploreCollection(ObservableCollection<PlaylistModel> target, IReadOnlyList<SongModel> songs,
        string title, string category, int offset, string accentColor, string coverImage)
    {
        var playlist = new PlaylistModel
        {
            Title = title,
            Subtitle = $"{category} · 编辑精选",
            Category = category,
            PlayCount = $"{48 + offset * 7}.6万",
            AccentColor = accentColor,
            CoverImage = coverImage
        };
        for (var index = 0; index < Math.Min(6, songs.Count); index++)
        {
            playlist.Songs.Add(songs[(offset + index) % songs.Count]);
        }
        target.Add(playlist);
    }
}
