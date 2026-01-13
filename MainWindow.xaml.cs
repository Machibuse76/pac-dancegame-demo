using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PacDancegameDemo.Models;

namespace PacDancegameDemo;

public partial class MainWindow : Window
{
    private readonly MediaPlayer _player = new();
    private readonly DispatcherTimer _beatTimer = new();
    private readonly Random _random = new();
    private readonly Brush _accentBrush;
    private SongItem? _currentSong;

    public MainWindow()
    {
        InitializeComponent();

        _accentBrush = (Brush)Application.Current.Resources["AccentBrush"];

        SongList.SelectionChanged += SongListOnSelectionChanged;
        PlayButton.Click += PlayButtonOnClick;
        StopButton.Click += StopButtonOnClick;
        BpmSlider.ValueChanged += BpmSliderOnValueChanged;

        _beatTimer.Tick += BeatTimerOnTick;
        LoadSongs();
        UpdateBpm();
    }

    private void LoadSongs()
    {
        var basePath = AppContext.BaseDirectory;
        var songPath = Path.Combine(basePath, "Assets", "Songs");
        var imagePath = Path.Combine(basePath, "Assets", "Images");

        Directory.CreateDirectory(songPath);
        Directory.CreateDirectory(imagePath);

        var songs = Directory.GetFiles(songPath, "*.mp3")
            .Select(file => new SongItem
            {
                Name = Path.GetFileNameWithoutExtension(file),
                FilePath = file,
                ImagePath = FindMatchingImage(imagePath, file)
            })
            .OrderBy(song => song.Name)
            .ToList();

        SongList.ItemsSource = songs;
        SongList.DisplayMemberPath = nameof(SongItem.Name);

        if (songs.Count > 0)
        {
            SongList.SelectedIndex = 0;
        }
        else
        {
            NowPlaying.Text = "No songs found";
        }
    }

    private static string? FindMatchingImage(string imageRoot, string songFilePath)
    {
        var baseName = Path.GetFileNameWithoutExtension(songFilePath);
        var possibleExtensions = new[] { ".png", ".jpg", ".jpeg" };

        foreach (var extension in possibleExtensions)
        {
            var candidate = Path.Combine(imageRoot, baseName + extension);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Directory.GetFiles(imageRoot)
            .FirstOrDefault(file => possibleExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase));
    }

    private void SongListOnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SongList.SelectedItem is not SongItem song)
        {
            return;
        }

        _currentSong = song;
        NowPlaying.Text = song.Name;

        if (!string.IsNullOrWhiteSpace(song.ImagePath) && File.Exists(song.ImagePath))
        {
            BackgroundImage.Source = new BitmapImage(new Uri(song.ImagePath));
        }
        else
        {
            BackgroundImage.Source = null;
        }
    }

    private void PlayButtonOnClick(object sender, RoutedEventArgs e)
    {
        if (_currentSong is null)
        {
            return;
        }

        _player.Open(new Uri(_currentSong.FilePath));
        _player.Play();
        StartBeatTimer();
    }

    private void StopButtonOnClick(object sender, RoutedEventArgs e)
    {
        _player.Stop();
        _beatTimer.Stop();
        ArrowCanvas.Children.Clear();
    }

    private void StartBeatTimer()
    {
        _beatTimer.Stop();
        var bpm = BpmSlider.Value;
        var interval = TimeSpan.FromSeconds(60d / bpm);
        _beatTimer.Interval = interval;
        _beatTimer.Start();
    }

    private void BeatTimerOnTick(object? sender, EventArgs e)
    {
        SpawnArrow();
    }

    private void BpmSliderOnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateBpm();
        if (_beatTimer.IsEnabled)
        {
            StartBeatTimer();
        }
    }

    private void UpdateBpm()
    {
        BpmValue.Text = $"{BpmSlider.Value:0} BPM";
    }

    private void SpawnArrow()
    {
        if (ArrowCanvas.ActualWidth <= 0 || ArrowCanvas.ActualHeight <= 0)
        {
            return;
        }

        var arrowGlyphs = new[] { "◀", "▼", "▲", "▶" };
        var laneCount = arrowGlyphs.Length;
        var lane = _random.Next(laneCount);
        var laneWidth = ArrowCanvas.ActualWidth / laneCount;
        var arrowSize = Math.Min(56, laneWidth - 8);
        var xOffset = laneWidth * lane + (laneWidth - arrowSize) / 2;

        var arrow = new TextBlock
        {
            Text = arrowGlyphs[lane],
            FontSize = arrowSize,
            Foreground = _accentBrush,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Center,
            Width = arrowSize,
            Height = arrowSize
        };

        Canvas.SetLeft(arrow, xOffset);
        Canvas.SetTop(arrow, ArrowCanvas.ActualHeight + arrowSize);
        ArrowCanvas.Children.Add(arrow);

        var bpm = Math.Max(60, BpmSlider.Value);
        var travelSeconds = Math.Max(0.9, 120d / bpm);
        var animation = new DoubleAnimation
        {
            From = ArrowCanvas.ActualHeight + arrowSize,
            To = -arrowSize,
            Duration = TimeSpan.FromSeconds(travelSeconds),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };

        animation.Completed += (_, _) => ArrowCanvas.Children.Remove(arrow);
        arrow.BeginAnimation(Canvas.TopProperty, animation);
    }
}
