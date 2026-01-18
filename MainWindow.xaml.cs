using System.IO;
using IOPath = System.IO.Path;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using PacDancegameDemo.Models;

namespace PacDancegameDemo;

public partial class MainWindow : Window
{
    private const int LaneCount = 4;
    private const double PerfectWindow = 24;
    private const double GoodWindow = 70;
    private readonly MediaPlayer _player = new();
    private readonly DispatcherTimer _beatTimer = new();
    private readonly DispatcherTimer _feedbackTimer = new();
    private readonly DispatcherTimer _backgroundTimer = new();
    private readonly DispatcherTimer _countdownTimer = new();
    private readonly DispatcherTimer _resultsDisplayTimer = new();
    private readonly DispatcherTimer _previewTimer = new();
    private readonly MediaPlayer _previewPlayer = new();
    private readonly Random _random = new();
    private readonly Brush _accentBrush;
    private readonly Brush _perfectBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0xFF));
    private readonly Brush _goodBrush = new SolidColorBrush(Color.FromRgb(0xB0, 0xFF, 0xB0));
    private readonly Brush _poorBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0xB0));
    private readonly List<ArrowNote> _activeNotes = new();
    private readonly Dictionary<int, ArrowNote> _activeHolds = new();
    private int _score;
    private int _perfectCount;
    private int _goodCount;
    private int _poorCount;
    private int _perfectStreak;
    private int _perfectMultiplier = 1;
    private bool _failed;
    private bool _isPlaying;
    private int _bpm = 80;
    private int _difficultyIndex = 1;
    private readonly List<string> _backgroundImages = new();
    private int _backgroundIndex = -1;
    private int _totalArrowsSpawned;
    private int _countdownValue;
    private readonly List<SongItem> _songs = new();
    private int _songIndex;
    private SongItem? _currentSong;
    private bool _hasVideoBackground;

    public MainWindow()
    {
        InitializeComponent();

        _accentBrush = (Brush)Application.Current.Resources["AccentBrush"];
        _player.MediaEnded += OnMediaEnded;

        SongList.SelectionChanged += SongListOnSelectionChanged;
        DifficultyList.SelectionChanged += DifficultyListOnSelectionChanged;

        _beatTimer.Tick += BeatTimerOnTick;
        _feedbackTimer.Interval = TimeSpan.FromSeconds(1);
        _feedbackTimer.Tick += FeedbackTimerOnTick;
        _backgroundTimer.Interval = TimeSpan.FromSeconds(6);
        _backgroundTimer.Tick += BackgroundTimerOnTick;
        _countdownTimer.Interval = TimeSpan.FromSeconds(1);
        _countdownTimer.Tick += CountdownTimerOnTick;
        _resultsDisplayTimer.Interval = TimeSpan.FromSeconds(15);
        _resultsDisplayTimer.Tick += ResultsDisplayTimerOnTick;
        _previewTimer.Interval = TimeSpan.FromSeconds(15);
        _previewTimer.Tick += PreviewTimerOnTick;
        Loaded += OnLoaded;
        LoadSongs();
        UpdateBpm();
        UpdateScoreboard();
        InitializeDifficultyList();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Focus();
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        Focus();
    }

    private void LoadSongs()
    {
        var songPath = ResolveSongsPath();
        Directory.CreateDirectory(songPath);

        _songs.Clear();
        _songs.AddRange(Directory.GetDirectories(songPath)
            .Select(BuildSongFromFolder)
            .Where(song => song is not null)
            .Select(song => song!)
            .OrderBy(song => song.Name)
            .ToList());

        SongList.ItemsSource = _songs;

        if (_songs.Count > 0)
        {
            _songIndex = 0;
            SetCurrentSong(_songs[_songIndex]);
            SongList.SelectedIndex = 0;
        }
        else
        {
            NowPlaying.Text = "No songs found";
            SongPreviewTitle.Text = "No songs found";
        }
    }

    private static SongItem? BuildSongFromFolder(string folderPath)
    {
        var mp3 = Directory.GetFiles(folderPath, "*.mp3").FirstOrDefault();
        if (string.IsNullOrWhiteSpace(mp3))
        {
            return null;
        }

        var titleImage = IOPath.Combine(folderPath, "title.png");
        var video = Directory.GetFiles(folderPath, "*.mp4").FirstOrDefault();
        var images = Directory.GetFiles(folderPath)
            .Where(file =>
            {
                var extension = IOPath.GetExtension(file);
                return extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
            })
            .Where(file => !file.Equals(titleImage, StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SongItem
        {
            Name = IOPath.GetFileNameWithoutExtension(mp3),
            FilePath = mp3,
            ImagePath = File.Exists(titleImage) ? titleImage : null,
            VideoPath = video,
            BackgroundImages = images
        };
    }

    private static string ResolveSongsPath()
    {
        var basePath = AppContext.BaseDirectory;
        var directPath = IOPath.Combine(basePath, "Assets", "Songs");
        if (Directory.Exists(directPath))
        {
            return directPath;
        }

        var current = new DirectoryInfo(basePath);
        while (current is not null)
        {
            var candidate = IOPath.Combine(current.FullName, "Assets", "Songs");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return directPath;
    }

    private void SetCurrentSong(SongItem song)
    {
        _currentSong = song;
        NowPlaying.Text = song.Name;
        SongPreviewTitle.Text = song.Name;

        if (!string.IsNullOrWhiteSpace(song.ImagePath) && File.Exists(song.ImagePath))
        {
            BackgroundImage.Source = new BitmapImage(new Uri(song.ImagePath));
            SongPreviewImage.Source = new BitmapImage(new Uri(song.ImagePath));
        }
        else
        {
            BackgroundImage.Source = null;
            SongPreviewImage.Source = null;
        }

        _backgroundImages.Clear();
        _backgroundImages.AddRange(song.BackgroundImages);
        _backgroundIndex = 0;
        _hasVideoBackground = !string.IsNullOrWhiteSpace(song.VideoPath) && File.Exists(song.VideoPath);

        StartPreviewIfIdle();
    }

    private void SongListOnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SongList.SelectedItem is not SongItem song)
        {
            return;
        }

        _songIndex = _songs.IndexOf(song);
        SetCurrentSong(song);
    }

    private void StartGame()
    {
        if (_currentSong is null)
        {
            return;
        }

        ResetFailureState();
        SongCarouselPanel.Visibility = Visibility.Collapsed;
        ScorePanel.Visibility = Visibility.Visible;
        NowPlayingPanel.Visibility = Visibility.Visible;
        _isPlaying = true;
        StopPreview();
        _resultsDisplayTimer.Stop();
        _player.Open(new Uri(_currentSong.FilePath));
        _player.Play();
        StartBackgroundCycle();
        ResetSessionStats();
        StartGameCountdown();
    }

    private void StartBeatTimer()
    {
        _beatTimer.Stop();
        var interval = TimeSpan.FromSeconds(60d / _bpm);
        _beatTimer.Interval = interval;
        _beatTimer.Start();
    }

    private void BeatTimerOnTick(object? sender, EventArgs e)
    {
        SpawnBeatNotes();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isPlaying && (e.Key == Key.Enter || e.Key == Key.Space || e.Key == Key.D1 || e.Key == Key.NumPad1))
        {
            StartGame();
            e.Handled = true;
            return;
        }

        if (!_isPlaying)
        {
            if (e.Key == Key.Up)
            {
                SelectPreviousSong();
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                SelectNextSong();
                e.Handled = true;
            }
            else if (e.Key == Key.Left)
            {
                SetDifficultyIndex(_difficultyIndex - 1);
                e.Handled = true;
            }
            else if (e.Key == Key.Right)
            {
                SetDifficultyIndex(_difficultyIndex + 1);
                e.Handled = true;
            }
            return;
        }

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
        e.Handled = true;
    }

    private void OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (!_isPlaying)
        {
            return;
        }

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

        HandleHoldRelease(lane);
        e.Handled = true;
    }

    private void UpdateBpm()
    {
        BpmValue.Text = $"{_bpm} BPM";
    }

    private void SpawnBeatNotes()
    {
        SpawnArrow();
        if (_random.NextDouble() < 0.25)
        {
            SpawnArrow();
        }
    }

    private void SpawnArrow()
    {
        var laneWidth = GetLaneWidth();
        if (laneWidth <= 0 || ArrowCanvas.ActualHeight <= 0)
        {
            return;
        }

        var lane = _random.Next(LaneCount);
        var arrowSize = Math.Min(90, laneWidth - 8);
        var xOffset = laneWidth * lane + (laneWidth - arrowSize) / 2;

        var arrow = CreateArrowShape(lane, arrowSize);

        var isHold = _random.NextDouble() < 0.2;
        Rectangle? trail = null;
        if (isHold)
        {
            var trailHeight = Math.Max(arrowSize * 2.6, ArrowCanvas.ActualHeight * 0.3);
            trail = new Rectangle
            {
                Width = arrowSize * 0.35,
                Height = trailHeight,
                Fill = new SolidColorBrush(Color.FromArgb(160, 0x3D, 0xDC, 0xFF)),
                RadiusX = 6,
                RadiusY = 6
            };
            Canvas.SetLeft(trail, xOffset + (arrowSize - trail.Width) / 2);
            Canvas.SetTop(trail, ArrowCanvas.ActualHeight + arrowSize + (arrowSize / 2));
            ArrowCanvas.Children.Add(trail);
        }

        Canvas.SetLeft(arrow, xOffset);
        Canvas.SetTop(arrow, ArrowCanvas.ActualHeight + arrowSize);
        ArrowCanvas.Children.Add(arrow);

        var note = new ArrowNote(arrow, lane, isHold, trail);
        _activeNotes.Add(note);
        _totalArrowsSpawned += 1;

        var bpm = Math.Max(60, _bpm);
        var travelSeconds = Math.Max(0.9, 120d / bpm);
        var animation = new DoubleAnimation
        {
            From = ArrowCanvas.ActualHeight + arrowSize,
            To = -arrowSize,
            Duration = TimeSpan.FromSeconds(travelSeconds),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };

        animation.Completed += (_, _) => HandleNoteCompletion(note);
        arrow.BeginAnimation(Canvas.TopProperty, animation);

        if (trail is not null)
        {
            trail.BeginAnimation(Canvas.TopProperty, animation);
        }
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

        if (candidate is null)
        {
            RegisterPoor(lane);
            return;
        }

        if (candidate.note.IsHold)
        {
            if (candidate.note.IsHolding)
            {
                return;
            }

            if (candidate.distance > GoodWindow)
            {
                RegisterPoor(lane);
                RemoveNote(candidate.note);
                return;
            }

            _activeHolds[lane] = candidate.note;
            candidate.note.IsHolding = true;
            candidate.note.HoldStartDistance = candidate.distance;
            return;
        }

        if (candidate.distance > GoodWindow)
        {
            RegisterPoor(lane);
            return;
        }

        if (candidate.distance <= PerfectWindow)
        {
            _perfectCount += 1;
            _perfectStreak += 1;
            if (_perfectStreak % 5 == 0)
            {
                _perfectMultiplier += 1;
            }

            _score += 100 * _perfectMultiplier;
            ShowHitResult("Perfect", _perfectBrush, lane);
            ApplyHitEffect(candidate.note, _perfectBrush);
        }
        else
        {
            _goodCount += 1;
            _score += 50;
            ResetPerfectStreak();
            ShowHitResult("Good", _goodBrush, lane);
            ApplyHitEffect(candidate.note, _goodBrush);
        }

        RemoveNote(candidate.note);
        UpdateScoreboard();
    }

    private void HandleHoldRelease(int lane)
    {
        if (!_activeHolds.TryGetValue(lane, out var note))
        {
            return;
        }

        _activeHolds.Remove(lane);
        note.IsHolding = false;

        var middle = ArrowCanvas.ActualHeight / 2;
        var distance = Math.Abs(GetNoteCenterY(note) - middle);
        var startDistance = note.HoldStartDistance;

        if (startDistance <= PerfectWindow && distance <= PerfectWindow)
        {
            _perfectCount += 1;
            _perfectStreak += 1;
            if (_perfectStreak % 5 == 0)
            {
                _perfectMultiplier += 1;
            }

            _score += 100 * _perfectMultiplier;
            ShowHitResult("Perfect", _perfectBrush, lane);
            ApplyHitEffect(note, _perfectBrush);
        }
        else if (startDistance <= GoodWindow && distance <= GoodWindow)
        {
            _goodCount += 1;
            _score += 50;
            ResetPerfectStreak();
            ShowHitResult("Good", _goodBrush, lane);
            ApplyHitEffect(note, _goodBrush);
        }
        else
        {
            RegisterPoor(lane);
        }

        RemoveNote(note);
        UpdateScoreboard();
    }

    private double GetNoteCenterY(ArrowNote note)
    {
        var position = note.Element.TranslatePoint(new Point(0, 0), ArrowCanvas);
        return position.Y + note.Element.ActualHeight / 2;
    }

    private void HandleNoteCompletion(ArrowNote note)
    {
        if (!_activeNotes.Contains(note))
        {
            return;
        }

        if (note.IsHold)
        {
            _activeHolds.Remove(note.Lane);
        }

        RegisterPoor(note.Lane);
        RemoveNote(note);
    }

    private void RegisterPoor(int lane)
    {
        _poorCount += 1;
        ResetPerfectStreak();
        ShowHitResult("Poor", _poorBrush, lane);
        UpdateScoreboard();

        if (_poorCount >= 100)
        {
            TriggerFailure();
        }
    }

    private void RemoveNote(ArrowNote note)
    {
        _activeNotes.Remove(note);
        ArrowCanvas.Children.Remove(note.Element);
        if (note.Trail is not null)
        {
            ArrowCanvas.Children.Remove(note.Trail);
        }
    }

    private void ClearActiveNotes()
    {
        _activeNotes.Clear();
        _activeHolds.Clear();
    }

    private void UpdateScoreboard()
    {
        ScoreText.Text = $"Score: {_score}";
        PerfectText.Text = $"Perfect: {_perfectCount}";
        PerfectMultiplierText.Text = $"x{_perfectMultiplier}";
        GoodText.Text = $"Good: {_goodCount}";
        PoorText.Text = $"Poor: {_poorCount}";
        UpdatePerfectGlow();
    }

    private void ShowHitResult(string result, Brush brush, int lane)
    {
        if (_failed)
        {
            return;
        }

        ClearHitResult();
        HitResultText.Text = result;
        HitResultText.Foreground = brush;
        Grid.SetColumn(HitResultText, lane);
        _feedbackTimer.Stop();
        _feedbackTimer.Start();
    }

    private void ApplyHitEffect(ArrowNote note, Brush brush)
    {
        var color = brush is SolidColorBrush solid ? solid.Color : Colors.White;
        var glow = new DropShadowEffect
        {
            Color = color,
            BlurRadius = 0,
            ShadowDepth = 0,
            Opacity = 0.9
        };

        note.Element.Effect = glow;

        var blurAnimation = new DoubleAnimation
        {
            From = 0,
            To = 24,
            Duration = TimeSpan.FromMilliseconds(160),
            AutoReverse = true
        };
        blurAnimation.Completed += (_, _) => note.Element.Effect = null;
        glow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, blurAnimation);

        var opacityAnimation = new DoubleAnimation
        {
            From = 0.9,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(300)
        };
        glow.BeginAnimation(DropShadowEffect.OpacityProperty, opacityAnimation);
    }

    private void FeedbackTimerOnTick(object? sender, EventArgs e)
    {
        ClearHitResult();
    }

    private void ClearHitResult()
    {
        _feedbackTimer.Stop();
        HitResultText.Text = string.Empty;
        Grid.SetColumn(HitResultText, 0);
    }

    private void ResetPerfectStreak()
    {
        _perfectStreak = 0;
        _perfectMultiplier = 1;
    }

    private void UpdatePerfectGlow()
    {
        if (_perfectStreak >= 5)
        {
            PerfectText.Effect = new DropShadowEffect
            {
                Color = Colors.Cyan,
                BlurRadius = 18,
                ShadowDepth = 0,
                Opacity = 0.9
            };
        }
        else
        {
            PerfectText.Effect = null;
        }
    }

    private void StartBackgroundCycle()
    {
        if (_hasVideoBackground && _currentSong?.VideoPath is { } videoPath)
        {
            StartVideoBackground(videoPath);
            return;
        }

        StopVideoBackground();

        if (_backgroundImages.Count == 0)
        {
            BackgroundImage.Source = _currentSong?.ImagePath is { } titlePath && File.Exists(titlePath)
                ? new BitmapImage(new Uri(titlePath))
                : null;
            return;
        }

        _backgroundTimer.Stop();
        if (_backgroundIndex < 0 || _backgroundIndex >= _backgroundImages.Count)
        {
            _backgroundIndex = 0;
        }

        SetBackgroundImage(_backgroundImages[_backgroundIndex]);
        _backgroundTimer.Start();
    }

    private void BackgroundTimerOnTick(object? sender, EventArgs e)
    {
        if (_backgroundImages.Count == 0 || _failed)
        {
            return;
        }

        _backgroundIndex = (_backgroundIndex + 1) % _backgroundImages.Count;
        SetBackgroundImage(_backgroundImages[_backgroundIndex]);
    }

    private void SetBackgroundImage(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        BackgroundImage.Source = new BitmapImage(new Uri(path));
    }

    private void StartVideoBackground(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        BackgroundVideo.Visibility = Visibility.Visible;
        BackgroundImage.Visibility = Visibility.Collapsed;
        BackgroundVideo.Source = new Uri(path);
        BackgroundVideo.MediaEnded -= OnBackgroundVideoEnded;
        BackgroundVideo.MediaEnded += OnBackgroundVideoEnded;
        BackgroundVideo.Position = TimeSpan.Zero;
        BackgroundVideo.Play();
    }

    private void StopVideoBackground()
    {
        BackgroundVideo.Stop();
        BackgroundVideo.Source = null;
        BackgroundVideo.Visibility = Visibility.Collapsed;
        BackgroundImage.Visibility = Visibility.Visible;
        BackgroundVideo.MediaEnded -= OnBackgroundVideoEnded;
    }

    private void OnBackgroundVideoEnded(object? sender, RoutedEventArgs e)
    {
        BackgroundVideo.Position = TimeSpan.Zero;
        BackgroundVideo.Play();
    }

    private void TriggerFailure()
    {
        if (_failed)
        {
            return;
        }

        _failed = true;
        EndSong(failed: true, showResults: true);
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() => EndSong(failed: false, showResults: true));
    }

    private void EndSong(bool failed, bool showResults)
    {
        _player.Stop();
        _beatTimer.Stop();
        _feedbackTimer.Stop();
        _backgroundTimer.Stop();
        _countdownTimer.Stop();
        _previewTimer.Stop();
        _resultsDisplayTimer.Stop();
        StopArrowAnimations();
        ClearHitResult();
        HideCountdown();
        _isPlaying = false;
        StopVideoBackground();

        if (failed)
        {
            FailureOverlay.Visibility = Visibility.Visible;
            BackgroundImage.Opacity = 0.25;
            FailedText.Visibility = showResults ? Visibility.Collapsed : Visibility.Visible;
        }

        if (showResults)
        {
            NowPlayingPanel.Visibility = Visibility.Collapsed;
            ShowFinalResults(failed);
            _resultsDisplayTimer.Start();
        }

        ScorePanel.Visibility = Visibility.Collapsed;
        if (!showResults)
        {
            NowPlayingPanel.Visibility = Visibility.Collapsed;
            SongCarouselPanel.Visibility = Visibility.Visible;
            StartPreviewIfIdle();
        }
    }

    private void ResultsDisplayTimerOnTick(object? sender, EventArgs e)
    {
        _resultsDisplayTimer.Stop();
        FailureOverlay.Visibility = Visibility.Collapsed;
        FailedText.Visibility = Visibility.Collapsed;
        FinalScorePanel.Visibility = Visibility.Collapsed;
        BackgroundImage.Opacity = 0.6;
        NowPlayingPanel.Visibility = Visibility.Collapsed;
        SongCarouselPanel.Visibility = Visibility.Visible;
        StartPreviewIfIdle();
    }

    private void StopArrowAnimations()
    {
        foreach (var element in ArrowCanvas.Children.OfType<UIElement>())
        {
            element.BeginAnimation(Canvas.TopProperty, null);
        }
    }

    private void ResetFailureState()
    {
        _failed = false;
        FailureOverlay.Visibility = Visibility.Collapsed;
        FailedText.Visibility = Visibility.Collapsed;
        BackgroundImage.Opacity = 0.6;
    }

    private void StartGameCountdown()
    {
        _beatTimer.Stop();
        _countdownTimer.Stop();
        ClearActiveNotes();
        ArrowCanvas.Children.Clear();
        ClearHitResult();
        _countdownValue = 5;
        GetReadyText.Visibility = Visibility.Visible;
        CountdownText.Visibility = Visibility.Collapsed;
        _countdownTimer.Start();
    }

    private void CountdownTimerOnTick(object? sender, EventArgs e)
    {
        if (_countdownValue == 5)
        {
            GetReadyText.Visibility = Visibility.Collapsed;
            CountdownText.Visibility = Visibility.Visible;
        }

        CountdownText.Text = _countdownValue.ToString();
        _countdownValue -= 1;

        if (_countdownValue < 0)
        {
            _countdownTimer.Stop();
            HideCountdown();
            StartBeatTimer();
        }
    }

    private void HideCountdown()
    {
        GetReadyText.Visibility = Visibility.Collapsed;
        CountdownText.Visibility = Visibility.Collapsed;
    }

    private void ResetSessionStats()
    {
        _score = 0;
        _perfectCount = 0;
        _goodCount = 0;
        _poorCount = 0;
        _totalArrowsSpawned = 0;
        ResetPerfectStreak();
        UpdateScoreboard();
        FinalScorePanel.Visibility = Visibility.Collapsed;
    }

    private void SetDifficultyIndex(int index)
    {
        var clamped = Math.Clamp(index, 0, 2);
        _difficultyIndex = clamped;
        _bpm = clamped switch
        {
            0 => 60,
            1 => 80,
            _ => 100
        };
        UpdateBpm();
        UpdateDifficultyListSelection();
        if (_beatTimer.IsEnabled)
        {
            StartBeatTimer();
        }
    }

    private void InitializeDifficultyList()
    {
        DifficultyList.ItemsSource = new[] { "Easy", "Standard", "Expert" };
        DifficultyList.SelectedIndex = _difficultyIndex;
    }

    private void DifficultyListOnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DifficultyList.SelectedIndex < 0)
        {
            return;
        }

        SetDifficultyIndex(DifficultyList.SelectedIndex);
    }

    private void UpdateDifficultyListSelection()
    {
        if (DifficultyList.SelectedIndex != _difficultyIndex)
        {
            DifficultyList.SelectedIndex = _difficultyIndex;
        }
    }

    private void SelectPreviousSong()
    {
        if (_songs.Count == 0)
        {
            return;
        }

        _songIndex = (_songIndex - 1 + _songs.Count) % _songs.Count;
        SongList.SelectedIndex = _songIndex;
    }

    private void SelectNextSong()
    {
        if (_songs.Count == 0)
        {
            return;
        }

        _songIndex = (_songIndex + 1) % _songs.Count;
        SongList.SelectedIndex = _songIndex;
    }

    private void StartPreviewIfIdle()
    {
        if (_isPlaying || _currentSong is null)
        {
            return;
        }

        StopPreview();
        _previewPlayer.Open(new Uri(_currentSong.FilePath));
        _previewPlayer.Position = TimeSpan.Zero;
        _previewPlayer.Play();
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private void PreviewTimerOnTick(object? sender, EventArgs e)
    {
        StopPreview();
    }

    private void StopPreview()
    {
        _previewTimer.Stop();
        _previewPlayer.Stop();
        _previewPlayer.Close();
    }

    private void ShowFinalResults(bool failed)
    {
        var maxScore = _totalArrowsSpawned * 100;
        var cappedScore = Math.Min(_score, maxScore);
        var ratio = maxScore > 0 ? cappedScore / maxScore : 0d;
        var percent = ratio * 100d;
        var grade = failed ? "F" : GetGrade(percent);

        FinalScoreText.Text = $"Score: {_score}";
        FinalGradeText.Text = $"Grade: {grade}";
        FinalAccuracyText.Text = $"Accuracy: {percent:0}%";
        FinalArrowsText.Text = $"Arrows: {_totalArrowsSpawned}";
        FinalScorePanel.Visibility = Visibility.Visible;
    }

    private static string GetGrade(double percent)
    {
        return percent switch
        {
            >= 90 => "A",
            >= 80 => "B",
            >= 70 => "C",
            >= 60 => "D",
            _ => "F"
        };
    }

    private double GetLaneWidth()
    {
        if (LaneGrid.ActualWidth <= 0)
        {
            return 0;
        }

        return LaneGrid.ActualWidth / LaneCount;
    }

    private FrameworkElement CreateArrowShape(int lane, double size)
    {
        var geometry = Geometry.Parse("M 50,0 100,50 75,50 75,100 25,100 25,50 0,50 Z");
        var path = new System.Windows.Shapes.Path
        {
            Data = geometry,
            Width = size,
            Height = size,
            Stretch = Stretch.Fill,
            Stroke = Brushes.White,
            StrokeThickness = 3,
            Fill = new LinearGradientBrush(
                Color.FromRgb(0xB7, 0x4C, 0xFF),
                Color.FromRgb(0x2F, 0xB7, 0xFF),
                new Point(0, 0),
                new Point(1, 1))
        };

        var outline = new DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 6,
            ShadowDepth = 0,
            Opacity = 0.6
        };
        path.Effect = outline;

        var angle = lane switch
        {
            0 => -90,
            1 => 180,
            2 => 0,
            3 => 90,
            _ => 0
        };
        path.RenderTransform = new RotateTransform(angle, size / 2, size / 2);
        return path;
    }

    private sealed class ArrowNote
    {
        public ArrowNote(FrameworkElement element, int lane, bool isHold, Rectangle? trail)
        {
            Element = element;
            Lane = lane;
            IsHold = isHold;
            Trail = trail;
        }

        public FrameworkElement Element { get; }
        public int Lane { get; }
        public bool IsHold { get; }
        public Rectangle? Trail { get; }
        public bool IsHolding { get; set; }
        public double HoldStartDistance { get; set; }
    }
}
