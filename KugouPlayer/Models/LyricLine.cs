using CommunityToolkit.Mvvm.ComponentModel;

namespace KugouPlayer.Models;

public partial class LyricLine : ObservableObject
{
    public required TimeSpan Timestamp { get; init; }
    public required string Text { get; init; }

    [ObservableProperty]
    private bool _isActive;
}

