using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KugouPlayer.Models
{
    [ObservableObject]
    public partial class SongModel
    {
        [ObservableProperty]
        private string? _songName;

        [ObservableProperty]
        private string? _singer;

        [ObservableProperty]
        private string? _coverImage;

        [ObservableProperty]
        private double _currentSecond;

        [ObservableProperty]
        private double _totalSecond;

        [ObservableProperty]
        private bool _isPlaying;
    }
}
