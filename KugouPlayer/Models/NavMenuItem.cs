using CommunityToolkit.Mvvm.ComponentModel;

namespace KugouPlayer.Models;

public partial class NavMenuItem : ObservableObject
{
    public required string MenuName { get; init; }
    public required string IconGlyph { get; init; }
    public required PageKind Page { get; init; }

    [ObservableProperty]
    private bool _isSelected;
}
