using KugouPlayer.ViewModels;
using KugouPlayer.Views;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace KugouPlayer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private DesktopLyricsWindow? _desktopLyricsWindow;
    private readonly System.Windows.Forms.NotifyIcon _trayIcon;
    private HwndSource? _windowSource;
    private bool _trayHintShown;
    private const int HotKeyPlayPause = 4101;
    private const int HotKeyPrevious = 4102;
    private const int HotKeyNext = 4103;
    private const int WindowsMessageHotKey = 0x0312;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "KugouPlayer",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized && _viewModel.MinimizeToTray)
            {
                Hide();
                if (!_trayHintShown)
                {
                    _trayIcon.ShowBalloonTip(1800, "KugouPlayer", "应用仍在后台运行，可双击托盘图标恢复。", System.Windows.Forms.ToolTipIcon.Info);
                    _trayHintShown = true;
                }
            }
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        _windowSource = HwndSource.FromHwnd(handle);
        _windowSource?.AddHook(WindowMessageHook);
        RegisterHotKey(handle, HotKeyPlayPause, 0, 0xB3);
        RegisterHotKey(handle, HotKeyPrevious, 0, 0xB1);
        RegisterHotKey(handle, HotKeyNext, 0, 0xB0);
    }

    protected override void OnClosed(EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        UnregisterHotKey(handle, HotKeyPlayPause);
        UnregisterHotKey(handle, HotKeyPrevious);
        UnregisterHotKey(handle, HotKeyNext);
        _windowSource?.RemoveHook(WindowMessageHook);
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _desktopLyricsWindow?.Close();
        _viewModel.Dispose();
        base.OnClosed(e);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (e.OriginalSource is not System.Windows.Controls.Button && e.OriginalSource is not System.Windows.Controls.TextBox)
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void ProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Slider slider || slider.ActualWidth <= 0)
        {
            return;
        }

        var percent = e.GetPosition(slider).X / slider.ActualWidth * 100;
        _viewModel.SeekToPercent(percent);
    }

    private void OverlayBackdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.IsQueueOpen)
        {
            _viewModel.ToggleQueueCommand.Execute(null);
        }
    }

    private void LyricsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.ListBox listBox && listBox.SelectedItem is not null)
        {
            listBox.ScrollIntoView(listBox.SelectedItem);
        }
    }

    private void DesktopLyricsButton_Click(object sender, RoutedEventArgs e)
    {
        _desktopLyricsWindow ??= new DesktopLyricsWindow { DataContext = _viewModel, Owner = this };
        if (_desktopLyricsWindow.IsVisible)
        {
            _desktopLyricsWindow.Hide();
        }
        else
        {
            _desktopLyricsWindow.Show();
        }
    }

    private void OpenLocalVideoButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择本地视频",
            Filter = "视频文件|*.mp4;*.mkv;*.avi;*.wmv;*.mov;*.m4v|所有文件|*.*"
        };
        if (dialog.ShowDialog(this) == true)
        {
            new VideoPlayerWindow(dialog.FileName) { Owner = this }.ShowDialog();
        }
    }

    private System.Windows.Forms.ContextMenuStrip BuildTrayMenu()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("显示主窗口", null, (_, _) => RestoreFromTray());
        menu.Items.Add("播放 / 暂停", null, (_, _) => Dispatcher.Invoke(() => _viewModel.TogglePlayCommand.Execute(null)));
        menu.Items.Add("上一首", null, (_, _) => Dispatcher.Invoke(() => _viewModel.PreviousSongCommand.Execute(null)));
        menu.Items.Add("下一首", null, (_, _) => Dispatcher.Invoke(() => _viewModel.NextSongCommand.Execute(null)));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(Close));
        return menu;
    }

    private void RestoreFromTray()
    {
        Dispatcher.Invoke(() =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        });
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WindowsMessageHotKey)
        {
            return IntPtr.Zero;
        }
        switch (wParam.ToInt32())
        {
            case HotKeyPlayPause:
                _viewModel.TogglePlayCommand.Execute(null);
                break;
            case HotKeyPrevious:
                _viewModel.PreviousSongCommand.Execute(null);
                break;
            case HotKeyNext:
                _viewModel.NextSongCommand.Execute(null);
                break;
        }
        handled = true;
        return IntPtr.Zero;
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr windowHandle, int identifier, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr windowHandle, int identifier);

}
