using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace KugouPlayer.Views;

public partial class VideoPlayerWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private bool _isPlaying = true;

    public VideoPlayerWindow(string filePath)
    {
        InitializeComponent();
        Title = Path.GetFileName(filePath);
        Player.Source = new Uri(filePath, UriKind.Absolute);
        Player.Volume = 0.7;
        Player.Play();
        _timer.Tick += (_, _) =>
        {
            if (!ProgressSlider.IsMouseCaptureWithin)
            {
                ProgressSlider.Value = Player.Position.TotalSeconds;
            }
        };
        _timer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        Player.Stop();
        Player.Close();
        base.OnClosed(e);
    }

    private void Player_MediaOpened(object sender, RoutedEventArgs e)
    {
        if (Player.NaturalDuration.HasTimeSpan)
        {
            ProgressSlider.Maximum = Math.Max(1, Player.NaturalDuration.TimeSpan.TotalSeconds);
        }
    }

    private void Player_MediaEnded(object sender, RoutedEventArgs e)
    {
        Player.Position = TimeSpan.Zero;
        Player.Pause();
        _isPlaying = false;
        PlayPauseButton.Content = "播放";
    }

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isPlaying)
        {
            Player.Pause();
            PlayPauseButton.Content = "播放";
        }
        else
        {
            Player.Play();
            PlayPauseButton.Content = "暂停";
        }
        _isPlaying = !_isPlaying;
    }

    private void ProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Slider slider)
        {
            Player.Position = TimeSpan.FromSeconds(slider.Value);
        }
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Player is not null)
        {
            Player.Volume = e.NewValue;
        }
    }
}
