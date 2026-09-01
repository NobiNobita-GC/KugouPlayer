using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KugouPlayer.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace KugouPlayer.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private string _searchText = string.Empty;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value)
                    return;

                _searchText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSearchEmpty));
            }
        }

        public bool IsSearchEmpty =>
            string.IsNullOrEmpty(SearchText);
    }
}