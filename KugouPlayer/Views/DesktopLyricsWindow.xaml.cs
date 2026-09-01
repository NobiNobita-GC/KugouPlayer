using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace KugouPlayer.Views;

public partial class DesktopLyricsWindow : Window
{
    private const int ExtendedStyleIndex = -20;
    private const int TransparentStyle = 0x20;
    private bool _isLocked;

    public DesktopLyricsWindow()
    {
        InitializeComponent();
    }

    private void LyricSurface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isLocked && e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void LockButton_Click(object sender, RoutedEventArgs e)
    {
        _isLocked = !_isLocked;
        LockButton.Content = _isLocked ? "\uE785" : "\uE72E";
        LockButton.ToolTip = _isLocked ? "解锁歌词" : "锁定歌词";

        var handle = new WindowInteropHelper(this).Handle;
        var styles = GetWindowLong(handle, ExtendedStyleIndex);
        SetWindowLong(handle, ExtendedStyleIndex, _isLocked ? styles | TransparentStyle : styles & ~TransparentStyle);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr windowHandle, int index, int newStyle);
}
