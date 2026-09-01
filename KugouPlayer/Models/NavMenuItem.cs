using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KugouPlayer.Models
{
    [ObservableObject]
    public partial class NavMenuItem
    {
        [ObservableProperty]
        private string? _menuName;

        [ObservableProperty]
        private string? _iconPath;

        [ObservableProperty]
        private bool _isSelected;
    }
}
