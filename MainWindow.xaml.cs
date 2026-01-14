using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PacDancegameDemo.Models;

namespace PacDancegameDemo;

public partial class MainWindow : Window
{
    private const double PerfectWindow = 24;
    private const double GoodWindow = 70;
    private readonly MediaPlayer _player = new();
    private readonly DispatcherTimer _beatTimer = new();
    private readonly Random _random = new();
    private readonly Brush _accentBrush;
    private readonly List<ArrowNote> _activeNotes = new();
    private int _score;
    private int _perfectCount;
    private int _goodCount;
    private int _poorCount;
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
        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
        LoadSongs();
        UpdateBpm();
        UpdateScoreboard();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Focus();
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
        ClearActiveNotes();
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

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        var lane = e.Key switch
        {
            Key.Left => 0,
            Key.Down => 1,
            Key.Up => 2,
            Key.Right => 3,
            _ => -1
        };

        if (lane < 0)
        {
            return;
        }

        HandleHit(lane);
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

        var note = new ArrowNote(arrow, lane);
        _activeNotes.Add(note);

        var bpm = Math.Max(60, BpmSlider.Value);
        var travelSeconds = Math.Max(0.9, 120d / bpm);
        var animation = new DoubleAnimation
        {
            From = ArrowCanvas.ActualHeight + arrowSize,
            To = -arrowSize,
            Duration = TimeSpan.FromSeconds(travelSeconds),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };

        animation.Completed += (_, _) => RegisterMiss(note);
        arrow.BeginAnimation(Canvas.TopProperty, animation);
    }

    private void HandleHit(int lane)
    {
        if (ArrowCanvas.ActualHeight <= 0)
        {
            return;
        }

        var middle = ArrowCanvas.ActualHeight / 2;
        var candidate = _activeNotes
            .Where(note => note.Lane == lane)
            .Select(note => new { note, distance = Math.Abs(GetNoteCenterY(note) - middle) })
            .OrderBy(result => result.distance)
            .FirstOrDefault();

        if (candidate is null || candidate.distance > GoodWindow)
        {
            RegisterPoor();
            return;
        }

        if (candidate.distance <= PerfectWindow)
        {
            _perfectCount += 1;
            _score += 100;
        }
        else
        {
            _goodCount += 1;
            _score += 50;
        }

        RemoveNote(candidate.note);
        UpdateScoreboard();
    }

    private double GetNoteCenterY(ArrowNote note)
    {
        var position = note.Element.TranslatePoint(new Point(0, 0), ArrowCanvas);
        return position.Y + note.Element.ActualHeight / 2;
    }

    private void RegisterMiss(ArrowNote note)
    {
        if (!_activeNotes.Contains(note))
        {
            return;
        }

        RegisterPoor();
        RemoveNote(note);
    }

    private void RegisterPoor()
    {
        _poorCount += 1;
        _score -= 10;
        UpdateScoreboard();
    }

    private void RemoveNote(ArrowNote note)
    {
        _activeNotes.Remove(note);
        ArrowCanvas.Children.Remove(note.Element);
    }

    private void ClearActiveNotes()
    {
        _activeNotes.Clear();
    }

    private void UpdateScoreboard()
    {
        ScoreText.Text = $"Score: {_score}";
        PerfectText.Text = $"Perfect: {_perfectCount}";
        GoodText.Text = $"Good: {_goodCount}";
        PoorText.Text = $"Poor: {_poorCount}";
    }

    private sealed record ArrowNote(TextBlock Element, int Lane);
}
