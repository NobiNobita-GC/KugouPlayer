using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace KugouPlayer.Models;

public partial class PlaylistModel : ObservableObject
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _subtitle = string.Empty;

    public required string AccentColor { get; init; }
    public string? CoverImage { get; init; }
    public string PlayCount { get; init; } = "0";
    public string Category { get; init; } = "精选";
    public bool IsUserCreated { get; init; }
    public ObservableCollection<SongModel> Songs { get; init; } = [];
}
