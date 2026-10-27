using System.ComponentModel;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using BroadcastControl.App.Services;
using BroadcastControl.App.ViewModels;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.DependencyInjection;

namespace BroadcastControl.App;

public partial class MainWindow : Window
{
    private const double WindowedWidth = 1600;
    private const double WindowedHeight = 900;
    private const int EoUdpPort = 6000;
    private const int IrUdpPort = 6001;
    private const int MobileAlertPort = 8088;
    private const int OverlayCacheLimit = 48;
    private const string DefaultRecordedVideoUrl = "http://192.168.3.143:8090/";
    private const string RecordedVideoCacheFolderName = "LIG_DNA_GUI_recorded_videos";
    private static readonly TimeSpan MobileAlertCooldown = TimeSpan.FromSeconds(10);
    private static readonly HttpClient RecordedVideoHttpClient = new();
    private readonly MainViewModel _viewModel;
    private readonly UdpEncodedVideoReceiverService _eoUdpCaptureService;
    private readonly UdpEncodedVideoReceiverService _irUdpCaptureService;
    private readonly ViewportRecordingService _viewportRecordingService;
    private readonly UdpMotorControlService _motorControlService;
    private readonly UdpMotorStatusReceiverService _motorStatusReceiverService;
    private readonly MobileAlertHubService _mobileAlertHubService;
    private readonly JetsonBridgeSshService _jetsonBridgeSshService;
    private readonly DispatcherTimer _motorHoldTimer;
    private readonly DispatcherTimer _recordingMetadataTimer;

    private bool _isFullscreenMode = true;
    private string? _lastStatusSignature;
    private bool _isViewportRecordingActive;
    private readonly List<RecordedVideoItem> _recordedVideoFiles = new();
    private string? _recordedVideoSelectedFolder;
    private string? _lastDetectionAlertSignature;
    private DateTimeOffset _lastMobileAlertAt = DateTimeOffset.MinValue;
    private DateTime _recordingMetadataWindowStart;
    private readonly Dictionary<string, int> _activeMotorDirections = new(StringComparer.Ordinal);
    private readonly HashSet<Key> _pressedMotorKeys = new();
    private const uint OverlayFrameTolerance = 12;
    private const float DisplayScoreThreshold = 0.60f;
    private sealed class RecordedVideoItem
    {
        public string Name { get; set; } = string.Empty;

        public string Url { get; set; } = string.Empty;

        public long SizeBytes { get; set; }

        public string Folder { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public bool IsFolder { get; set; }

        public bool IsBack { get; set; }
    }

    private static readonly HashSet<string> NonMilitaryTargetClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "chair",
        "dining table",
        "tv",
        "laptop",
        "cell phone",
        "bottle",
        "couch",
        "bench",
        "refrigerator"
    };

    private static readonly HashSet<string> CompositeTargetClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "person",
        "airplane",
        "bicycle",
        "car",
        "motorcycle",
        "bus",
        "truck",
        "train",
        "boat",
        "cell phone",
        "laptop",
        "chair",
        "dining table",
        "tv",
        "couch",
        "bench",
        "bottle",
        "refrigerator"
    };

    public MainWindow(
        MainViewModel viewModel,
        UdpMotorControlService motorControlService,
        [FromKeyedServices("eo")] UdpEncodedVideoReceiverService eoUdpCaptureService,
        [FromKeyedServices("ir")] UdpEncodedVideoReceiverService irUdpCaptureService,
        ViewportRecordingService viewportRecordingService,
        UdpMotorStatusReceiverService motorStatusReceiverService,
        MobileAlertHubService mobileAlertHubService,
        JetsonBridgeSshService jetsonBridgeSshService)
    {
        InitializeComponent();

        RecordedVideosView.RefreshRequested += OnRefreshRecordedVideosRequested;
        RecordedVideosView.VideoSelectionRequested += e =>
            RecordedVideoList_OnSelectionChanged(RecordedVideosView.RecordedVideoList, e);
        RecordedVideosView.PlaybackFailed += OnRecordedVideoPlaybackFailed;
        SettingsDrawerView.WindowModeToggleRequested += WindowModeToggleButton_OnClick;

        CameraWorkspace.OverlayRefreshRequested += OnMiddleOverlayRefreshRequested;
        CameraWorkspace.MotorButtonPressed += MotorButton_OnPreviewMouseLeftButtonDown;
        CameraWorkspace.MotorButtonReleased += MotorButton_OnPreviewMouseLeftButtonUp;
        CameraWorkspace.MotorButtonExited += MotorButton_OnMouseLeave;
        CameraControlBar.MotorButtonPressed += MotorButton_OnPreviewMouseLeftButtonDown;
        CameraControlBar.MotorButtonReleased += MotorButton_OnPreviewMouseLeftButtonUp;
        CameraControlBar.MotorButtonExited += MotorButton_OnMouseLeave;

        _motorControlService = motorControlService;
        _viewModel = viewModel;
        _eoUdpCaptureService = eoUdpCaptureService;
        _irUdpCaptureService = irUdpCaptureService;
        _viewportRecordingService = viewportRecordingService;
        _motorStatusReceiverService = motorStatusReceiverService;
        _mobileAlertHubService = mobileAlertHubService;
        _jetsonBridgeSshService = jetsonBridgeSshService;
        _motorHoldTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _motorHoldTimer.Tick += MotorHoldTimer_OnTick;
        _recordingMetadataTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _recordingMetadataTimer.Tick += RecordingMetadataTimer_OnTick;
        DataContext = _viewModel;

        Loaded += OnLoaded;
        Closed += OnClosed;
        PreviewKeyDown += MainWindow_OnPreviewKeyDown;
        PreviewKeyUp += MainWindow_OnPreviewKeyUp;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Maximized;
        SettingsDrawerView.UpdateWindowModeButtonText(_isFullscreenMode);

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.ManualAnalysisSaveRequested += ViewModel_OnManualAnalysisSaveRequested;
        _viewModel.ManualSystemLogSaveRequested += ViewModel_OnManualSystemLogSaveRequested;
        _jetsonBridgeSshService.MessageReady += JetsonBridgeSshService_OnMessageReady;
        _eoUdpCaptureService.FrameReady += _viewModel.OnEoFrameReady;
        _eoUdpCaptureService.DetectionsReceived += OnEoDetectionsReceived;
        _eoUdpCaptureService.StatusReceived += OnYoloStatusReceived;
        _irUdpCaptureService.FrameReady += _viewModel.OnIrFrameReady;
        _irUdpCaptureService.DetectionsReceived += OnIrDetectionsReceived;
        _irUdpCaptureService.StatusReceived += OnYoloStatusReceived;
        _motorStatusReceiverService.StatusReceived += OnMotorStatusReceived;
        _motorStatusReceiverService.ReceiverError += OnMotorStatusReceiverError;

        _eoUdpCaptureService.SetBrightness(_viewModel.Brightness);
        _eoUdpCaptureService.SetContrast(_viewModel.Contrast);
        _irUdpCaptureService.SetBrightness(_viewModel.Brightness);
        _irUdpCaptureService.SetContrast(_viewModel.Contrast);
        _viewModel.InitializeMotorControlState();
        _motorStatusReceiverService.Start();
        _viewModel.AppendImportantLog($"모터 상태 수신 대기 포트: {_motorStatusReceiverService.Port}");

        UpdateRecordingViewportState();
        RenderDetectionOverlay();
        UpdateMotorAutomationState();
        _recordingMetadataWindowStart = DateTime.Now;
        _recordingMetadataTimer.Start();

        SettingsDrawerView.SetOpenState(_viewModel.IsSettingsOpen, animate: false);
        await _jetsonBridgeSshService.StartAsync();

        if (_eoUdpCaptureService.Start(EoUdpPort))
        {
        }
        else
        {
            _viewModel.AppendImportantLog($"Failed to start the EO UDP stream receiver on port {EoUdpPort}.");
        }

        if (_irUdpCaptureService.Start(IrUdpPort))
        {
        }
        else
        {
            _viewModel.AppendImportantLog($"Failed to start the IR UDP stream receiver on port {IrUdpPort}.");
        }

        if (_mobileAlertHubService.Start(MobileAlertPort))
        {
            _viewModel.AppendImportantLog($"모바일 위험 알림 앱이 시작되었습니다: {_mobileAlertHubService.AccessHintUrls}");
        }
        else
        {
            _viewModel.AppendImportantLog($"모바일 위험 알림 앱 시작에 실패했습니다. 포트 {MobileAlertPort}를 확인하세요.");
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.Brightness):
                _eoUdpCaptureService.SetBrightness(_viewModel.Brightness);
                _irUdpCaptureService.SetBrightness(_viewModel.Brightness);
                break;

            case nameof(MainViewModel.Contrast):
                _eoUdpCaptureService.SetContrast(_viewModel.Contrast);
                _irUdpCaptureService.SetContrast(_viewModel.Contrast);
                break;

            case nameof(MainViewModel.IsRecordingActive):
                HandleRecordingActiveStateChanged();
                break;

            case nameof(MainViewModel.IsSettingsOpen):
                SettingsDrawerView.SetOpenState(_viewModel.IsSettingsOpen, animate: true);
                break;

            case nameof(MainViewModel.CurrentMode):
            case nameof(MainViewModel.IsSystemPoweredOn):
                UpdateMotorAutomationState();
                break;

            case nameof(MainViewModel.IsEnglishLanguage):
            case nameof(MainViewModel.IsKoreanLanguage):
                SettingsDrawerView.UpdateWindowModeButtonText(_isFullscreenMode);
                break;
        }
    }

    private void OnMiddleOverlayRefreshRequested(object? sender, EventArgs e)
    {
        UpdateRecordingViewportState();
        RenderDetectionOverlay(forceRefresh: true);
    }

    private void OnYoloStatusReceived(YoloStatusPacket statusPacket)
    {
        var signature = $"{statusPacket.Enabled}:{statusPacket.ModelLoaded}:{statusPacket.ConfThreshold}:{statusPacket.LastError}:{statusPacket.Source}";
        if (string.Equals(_lastStatusSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        _lastStatusSignature = signature;

        if (!statusPacket.ModelLoaded)
        {
            _viewModel.AppendImportantLog("YOLO 모델이 아직 로드되지 않았습니다.");
        }

        if (!string.IsNullOrWhiteSpace(statusPacket.LastError))
        {
            _viewModel.AppendImportantLog($"YOLO 상태 오류: {statusPacket.LastError}");
        }
    }

    private void OnMotorStatusReceived(object? sender, MotorStatusSnapshot snapshot)
    {
        Dispatcher.Invoke(() => _viewModel.UpdateMotorStatus(snapshot));
    }

    private void OnMotorStatusReceiverError(object? sender, string message)
    {
        Dispatcher.Invoke(() => _viewModel.AppendImportantLog($"모터 상태 수신 오류: {message}"));
    }
    private void OnEoSegmentChanged(PlaybackSegmentInfo segmentInfo)
    {
    }

    private void OnEoSegmentLoopRestarted(PlaybackSegmentInfo segmentInfo)
    {
    }

    private void OnEoDiagnosticsMessageReady(string message)
    {
    }

    private void UpdateRecordingViewportState()
    {
        _eoUdpCaptureService.UpdateViewportTransform(
            _viewModel.ZoomLevel,
            _viewModel.ZoomTransformX,
            _viewModel.ZoomTransformY,
            CameraWorkspace.CameraViewport.ActualWidth,
            CameraWorkspace.CameraViewport.ActualHeight);
        _irUdpCaptureService.UpdateViewportTransform(
            _viewModel.ZoomLevel,
            _viewModel.ZoomTransformX,
            _viewModel.ZoomTransformY,
            CameraWorkspace.CameraViewport.ActualWidth,
            CameraWorkspace.CameraViewport.ActualHeight);
    }
    private void RenderDetectionOverlay(bool forceRefresh = false)
    {
        var latestFrame = _viewModel.IsEoPrimary
            ? _viewModel.LatestEoFrame
            : _viewModel.LatestIrFrame;
        if (latestFrame is null ||
            !TryGetRenderableFrameAndDetection(out var frameToRender, out var detectionPacket))
        {
            CameraWorkspace.DetectionOverlay.ClearOverlay();
            return;
        }

        var displayDetections = FilterDisplayDetections(detectionPacket.Detections);
        if (displayDetections.Count == 0)
        {
            CameraWorkspace.DetectionOverlay.ClearOverlay();
            return;
        }

        var sourceWidth = detectionPacket.Width > 0 ? detectionPacket.Width : frameToRender.Width;
        var sourceHeight = detectionPacket.Height > 0 ? detectionPacket.Height : frameToRender.Height;
        CameraWorkspace.DetectionOverlay.RenderDetections(
            displayDetections,
            sourceWidth,
            sourceHeight,
            forceRefresh);
    }

    private static void TrimCache<T>(Dictionary<uint, T> cache)
    {
        while (cache.Count > OverlayCacheLimit)
        {
            var oldestKey = cache.Keys.Min();
            cache.Remove(oldestKey);
        }
    }
    private string BuildVlmStyleAnalysis(IReadOnlyList<DetectionInfo> detections)
    {
        return
            $"{_viewModel.LargeFeedTitle} 영상에서 주 탐지체 '{_viewModel.SelectedPrimaryTarget}' 기준 위험 객체 {detections.Count}개가 확인되었습니다. " +
            "운용자는 현 화면의 바운딩 박스 위치를 확인하고 추적/녹화 상태를 유지하십시오.";
    }

    private static string BuildDetectionSummary(IReadOnlyList<DetectionInfo> detections)
    {
        return string.Join(
            "\n",
            detections
                .OrderByDescending(d => d.Score)
                .Take(8)
                .Select((d, index) => $"{index + 1}. {d.ClassName} object{d.ObjectId} / 신뢰도 {d.Score:0.00} / bbox ({d.X1:0}, {d.Y1:0})-({d.X2:0}, {d.Y2:0})"));
    }

    private static byte[]? CaptureElementAsPng(FrameworkElement element)
    {
        var width = Math.Max(1, (int)Math.Round(element.ActualWidth));
        var height = Math.Max(1, (int)Math.Round(element.ActualHeight));
        if (width < 2 || height < 2)
        {
            return null;
        }

        try
        {
            element.UpdateLayout();
            var renderTarget = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            renderTarget.Render(element);
            renderTarget.Freeze();

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderTarget));

            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }
        catch
        {
            return null;
        }
    }
    private void HandleRecordingActiveStateChanged()
    {
        if (_viewModel.IsRecordingActive)
        {
            if (_isViewportRecordingActive)
            {
                return;
            }

            var filePath = _viewportRecordingService.StartRecordingToDesktop(CameraWorkspace.CameraPanel);
            _isViewportRecordingActive = true;
            _viewModel.AppendImportantLog($"Recording started: {filePath}");
            return;
        }

        if (!_isViewportRecordingActive)
        {
            return;
        }

        var savedPath = _viewportRecordingService.StopRecording();
        _isViewportRecordingActive = false;
        if (!string.IsNullOrWhiteSpace(savedPath))
        {
            if (System.IO.File.Exists(savedPath))
            {
                _viewModel.AppendImportantLog($"Video saved: {savedPath} ({_viewportRecordingService.RecordedFrameCount} frames)");
            }
            else if (!string.IsNullOrWhiteSpace(_viewportRecordingService.LastRecordingErrorMessage))
            {
                _viewModel.AppendImportantLog($"Video save failed: {_viewportRecordingService.LastRecordingErrorMessage}");
            }
            else
            {
                _viewModel.AppendImportantLog($"Video file was not created: {savedPath} ({_viewportRecordingService.RecordedFrameCount} frames)");
            }
        }
    }

    private void OnRecordedVideoPlaybackFailed(Exception exception)
    {
        _viewModel.AppendImportantLog($"녹화 영상 재생에 실패했습니다: {exception.Message}");
    }

    private async void OpenRecordedVideosButton_OnClick(object sender, RoutedEventArgs e)
    {
        RecordedVideosView.OpenPanel();
        await LoadRecordedVideosAsync();
        e.Handled = true;
    }

    private async void OnRefreshRecordedVideosRequested()
    {
        await LoadRecordedVideosAsync();
    }

    private async void RecordedVideoList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RecordedVideosView.RecordedVideoList.SelectedItem is not RecordedVideoItem item)
        {
            return;
        }

        if (item.IsBack)
        {
            _recordedVideoSelectedFolder = null;
            ShowRecordedVideoFolderList();
            return;
        }

        if (item.IsFolder)
        {
            _recordedVideoSelectedFolder = item.Folder;
            ShowRecordedVideoFolderContents(item.Folder);
            return;
        }

        if (string.IsNullOrWhiteSpace(item.Url))
        {
            return;
        }

        try
        {
            RecordedVideosView.PrepareVideoForDownload();
            RecordedVideosView.RecordedVideosStatusText.Text = $"{item.DisplayName} 내려받는 중...";

            var localPath = await EnsureRecordedVideoCachedAsync(item);
            RecordedVideosView.PlayCachedVideo(localPath, item.DisplayName);
        }
        catch (Exception ex)
        {
            RecordedVideosView.RecordedVideosStatusText.Text = "영상을 재생할 수 없습니다.";
            _viewModel.AppendImportantLog($"녹화 영상 재생 준비에 실패했습니다: {ex.Message}");
        }
    }

    private async Task LoadRecordedVideosAsync()
    {
        var baseUri = GetRecordedVideoBaseUri();
        var apiUri = new Uri(baseUri, "api/videos");
        RecordedVideosView.RecordedVideosStatusText.Text = "목록을 불러오는 중...";

        try
        {
            using var stream = await RecordedVideoHttpClient.GetStreamAsync(apiUri);
            var videos = await JsonSerializer.DeserializeAsync<List<RecordedVideoItem>>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            videos ??= new List<RecordedVideoItem>();

            foreach (var video in videos)
            {
                video.Folder = GetRecordedVideoFolder(video.Name);
                video.DisplayName = GetRecordedVideoFileName(video.Name);
                if (Uri.TryCreate(video.Url, UriKind.Absolute, out _))
                {
                    continue;
                }

                video.Url = new Uri(baseUri, video.Url).ToString();
            }

            _recordedVideoFiles.Clear();
            _recordedVideoFiles.AddRange(videos);

            if (!string.IsNullOrWhiteSpace(_recordedVideoSelectedFolder) &&
                _recordedVideoFiles.Any(video => string.Equals(video.Folder, _recordedVideoSelectedFolder, StringComparison.Ordinal)))
            {
                ShowRecordedVideoFolderContents(_recordedVideoSelectedFolder);
            }
            else
            {
                _recordedVideoSelectedFolder = null;
                ShowRecordedVideoFolderList();
            }
        }
        catch (Exception ex)
        {
            RecordedVideosView.RecordedVideoList.ItemsSource = null;
            RecordedVideosView.RecordedVideosStatusText.Text = "목록을 불러오지 못했습니다.";
            _viewModel.AppendImportantLog($"녹화 영상 목록을 불러오지 못했습니다: {ex.Message}");
        }
    }

    private static Uri GetRecordedVideoBaseUri()
    {
        var videoUrl = Environment.GetEnvironmentVariable("JETSON_VIDEO_URL");
        if (string.IsNullOrWhiteSpace(videoUrl))
        {
            videoUrl = DefaultRecordedVideoUrl;
        }

        if (!videoUrl.EndsWith("/", StringComparison.Ordinal))
        {
            videoUrl += "/";
        }

        return new Uri(videoUrl, UriKind.Absolute);
    }

    private void ShowRecordedVideoFolderList()
    {
        var folders = _recordedVideoFiles
            .GroupBy(video => video.Folder)
            .OrderByDescending(group => group.Key, StringComparer.Ordinal)
            .Select(group => new RecordedVideoItem
            {
                Folder = group.Key,
                DisplayName = $"[폴더] {group.Key} ({group.Count()}개)",
                IsFolder = true
            })
            .ToList();

        RecordedVideosView.RecordedVideoList.ItemsSource = folders;
        RecordedVideosView.RecordedVideosStatusText.Text = folders.Count == 0
            ? "저장된 영상 폴더가 아직 없습니다."
            : $"{folders.Count}개 폴더";
    }

    private void ShowRecordedVideoFolderContents(string folder)
    {
        var videos = _recordedVideoFiles
            .Where(video => string.Equals(video.Folder, folder, StringComparison.Ordinal))
            .OrderBy(video => video.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var items = new List<RecordedVideoItem>
        {
            new()
            {
                DisplayName = "[상위 폴더로]",
                IsBack = true
            }
        };
        items.AddRange(videos);

        RecordedVideosView.RecordedVideoList.ItemsSource = items;
        RecordedVideosView.RecordedVideosStatusText.Text = $"{folder} / {videos.Count}개 영상";
    }

    private static string GetRecordedVideoFolder(string name)
    {
        var normalized = name.Replace('\\', '/');
        var separatorIndex = normalized.LastIndexOf('/');
        return separatorIndex > 0
            ? normalized[..separatorIndex]
            : "기존 영상";
    }

    private static string GetRecordedVideoFileName(string name)
    {
        var normalized = name.Replace('\\', '/');
        var separatorIndex = normalized.LastIndexOf('/');
        return separatorIndex >= 0 && separatorIndex < normalized.Length - 1
            ? normalized[(separatorIndex + 1)..]
            : normalized;
    }

    private async void RecordingMetadataTimer_OnTick(object? sender, EventArgs e)
    {
        var windowEnd = DateTime.Now;
        var windowStart = _recordingMetadataWindowStart == default
            ? windowEnd.AddMinutes(-1)
            : _recordingMetadataWindowStart;
        _recordingMetadataWindowStart = windowEnd;

        await SaveRecordingMetadataAsync(
            windowStart,
            windowEnd,
            manual: false,
            includeAnalysis: true,
            includeSystemLog: true);
    }

    private async void ViewModel_OnManualAnalysisSaveRequested(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        await SaveRecordingMetadataAsync(
            now,
            now,
            manual: true,
            includeAnalysis: true,
            includeSystemLog: false);
    }

    private async void ViewModel_OnManualSystemLogSaveRequested(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        await SaveRecordingMetadataAsync(
            now,
            now,
            manual: true,
            includeAnalysis: false,
            includeSystemLog: true);
    }

    private async Task SaveRecordingMetadataAsync(
        DateTime windowStart,
        DateTime windowEnd,
        bool manual,
        bool includeAnalysis,
        bool includeSystemLog)
    {
        try
        {
            var payload = new Dictionary<string, object?>
            {
                ["manual"] = manual
            };

            if (includeAnalysis)
            {
                payload["analysisText"] = _viewModel.BuildAnalysisLogSnapshot(windowStart, windowEnd, includeAll: manual);
            }

            if (includeSystemLog)
            {
                payload["systemLogText"] = _viewModel.BuildSystemLogSnapshot(windowStart, windowEnd, includeAll: manual);
            }

            var apiUri = new Uri(GetRecordedVideoBaseUri(), "api/logs");
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await RecordedVideoHttpClient.PostAsync(apiUri, content);
            response.EnsureSuccessStatusCode();

            if (manual)
            {
                var targetName = includeAnalysis ? "VLM 분석 결과" : "시스템 로그";
                _viewModel.AppendImportantLog($"{targetName}를 젝슨 영상 폴더에 C_ 파일로 저장했습니다.");
            }
        }
        catch (Exception ex)
        {
            var modeText = manual ? "수동" : "자동";
            _viewModel.AppendImportantLog($"{modeText} 로그/VLM 저장에 실패했습니다: {ex.Message}");
        }
    }

    private static async Task<string> EnsureRecordedVideoCachedAsync(RecordedVideoItem item)
    {
        var cacheDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), RecordedVideoCacheFolderName);
        Directory.CreateDirectory(cacheDirectory);

        var localPath = System.IO.Path.Combine(cacheDirectory, SanitizeFileName(item.Name));
        if (File.Exists(localPath))
        {
            var localSize = new FileInfo(localPath).Length;
            if (item.SizeBytes <= 0 || localSize == item.SizeBytes)
            {
                return localPath;
            }
        }

        var bytes = await RecordedVideoHttpClient.GetByteArrayAsync(item.Url);
        await File.WriteAllBytesAsync(localPath, bytes);
        return localPath;
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = System.IO.Path.GetInvalidFileNameChars();
        var chars = fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray();
        var sanitized = new string(chars);
        return string.IsNullOrWhiteSpace(sanitized) ? "recorded_video" : sanitized;
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
    }

    private void Button_Click_1(object sender, RoutedEventArgs e)
    {
    }

    private void WindowModeToggleButton_OnClick(object sender, RoutedEventArgs e)
    {
        ToggleWindowMode();
    }

    private void ToggleWindowMode()
    {
        if (_isFullscreenMode)
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;
            WindowState = WindowState.Normal;
            Width = WindowedWidth;
            Height = WindowedHeight;
            Left = Math.Max(0, (SystemParameters.WorkArea.Width - Width) / 2);
            Top = Math.Max(0, (SystemParameters.WorkArea.Height - Height) / 2);
            _isFullscreenMode = false;
        }
        else
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
            _isFullscreenMode = true;
        }

        SettingsDrawerView.UpdateWindowModeButtonText(_isFullscreenMode);
    }

    private void MotorButton_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string direction })
        {
            return;
        }

        StartMotorRepeat(direction);
        e.Handled = true;
    }

    private void MotorButton_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string direction })
        {
            return;
        }

        StopMotorRepeat(direction);
        e.Handled = true;
    }

    private void MotorButton_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            sender is not FrameworkElement { Tag: string direction })
        {
            return;
        }

        StopMotorRepeat(direction);
    }

    private void MainWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox && e.Key == Key.C)
        {
            return;
        }

        if (TryHandleMotorStepKey(e))
        {
            return;
        }

        if (!TryMapKeyToMotorDirection(e.Key, out var direction))
        {
            return;
        }

        if (_pressedMotorKeys.Add(e.Key))
        {
            StartMotorRepeat(direction);
        }

        e.Handled = true;
    }

    private bool TryHandleMotorStepKey(KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox)
        {
            return false;
        }

        var delta = e.Key switch
        {
            Key.Add or Key.OemPlus => 1,
            Key.Subtract or Key.OemMinus => -1,
            _ => 0
        };

        if (delta == 0)
        {
            return false;
        }

        var isManualStepKey = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        var commandParameter = isManualStepKey
            ? $"Manual:{delta}"
            : $"Auto:{delta}";

        if (_viewModel.AdjustMotorStepCommand.CanExecute(commandParameter))
        {
            _viewModel.AdjustMotorStepCommand.Execute(commandParameter);
        }

        e.Handled = true;
        return true;
    }

    private void MainWindow_OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox && e.Key == Key.C)
        {
            return;
        }

        if (!TryMapKeyToMotorDirection(e.Key, out var direction))
        {
            return;
        }

        _pressedMotorKeys.Remove(e.Key);
        StopMotorRepeat(direction);
        e.Handled = true;
    }

    private void StartMotorRepeat(string direction)
    {
        if (!_viewModel.IsManualMode)
        {
            return;
        }

        if (_activeMotorDirections.TryGetValue(direction, out var count))
        {
            _activeMotorDirections[direction] = count + 1;
        }
        else
        {
            _activeMotorDirections[direction] = 1;
        }

        SendActiveMotorButtons();
        CameraControlBar.UpdateMotorPadButtonVisualStates(
            _activeMotorDirections.ContainsKey("Left"),
            _activeMotorDirections.ContainsKey("Right"),
            _activeMotorDirections.ContainsKey("Up"),
            _activeMotorDirections.ContainsKey("Down"),
            _activeMotorDirections.ContainsKey("Center"));

        if (_activeMotorDirections.Count > 0)
        {
            _motorHoldTimer.Start();
        }
    }

    private void StopMotorRepeat(string direction)
    {
        if (!_activeMotorDirections.TryGetValue(direction, out var count))
        {
            return;
        }

        if (count <= 1)
        {
            _activeMotorDirections.Remove(direction);
        }
        else
        {
            _activeMotorDirections[direction] = count - 1;
        }

        SendActiveMotorButtons();
        CameraControlBar.UpdateMotorPadButtonVisualStates(
            _activeMotorDirections.ContainsKey("Left"),
            _activeMotorDirections.ContainsKey("Right"),
            _activeMotorDirections.ContainsKey("Up"),
            _activeMotorDirections.ContainsKey("Down"),
            _activeMotorDirections.ContainsKey("Center"));

        if (_activeMotorDirections.Count == 0)
        {
            _motorHoldTimer.Stop();
        }
    }

    private void MotorHoldTimer_OnTick(object? sender, EventArgs e)
    {
        if (_activeMotorDirections.Count == 0 || !_viewModel.IsManualMode)
        {
            _motorHoldTimer.Stop();
            return;
        }

        SendActiveMotorButtons();
    }

    private void SendActiveMotorButtons()
    {
        if (!_viewModel.IsManualMode)
        {
            return;
        }

        _viewModel.UpdateManualButtonState(GetActiveMotorButtons());
    }

    private void UpdateMotorAutomationState()
    {
        if (_viewModel.IsAutoMode)
        {
            StopManualMotorInput();
        }
    }

    private void StopManualMotorInput()
    {
        _motorHoldTimer.Stop();
        _activeMotorDirections.Clear();
        _pressedMotorKeys.Clear();
        CameraControlBar.UpdateMotorPadButtonVisualStates(
            _activeMotorDirections.ContainsKey("Left"),
            _activeMotorDirections.ContainsKey("Right"),
            _activeMotorDirections.ContainsKey("Up"),
            _activeMotorDirections.ContainsKey("Down"),
            _activeMotorDirections.ContainsKey("Center"));
    }

    private static bool TryMapKeyToMotorDirection(Key key, out string direction)
    {
        direction = key switch
        {
            Key.Left => "Left",
            Key.Right => "Right",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.C => "Center",
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(direction);
    }

    private MotorButtonMask GetActiveMotorButtons()
    {
        if (_activeMotorDirections.Count == 0)
        {
            return MotorButtonMask.None;
        }

        if (_activeMotorDirections.ContainsKey("Center"))
        {
            return MotorButtonMask.Center;
        }

        var buttons = MotorButtonMask.None;

        if (_activeMotorDirections.ContainsKey("Left"))
        {
            buttons |= MotorButtonMask.Left;
        }

        if (_activeMotorDirections.ContainsKey("Right"))
        {
            buttons |= MotorButtonMask.Right;
        }

        if (_activeMotorDirections.ContainsKey("Up"))
        {
            buttons |= MotorButtonMask.Up;
        }

        if (_activeMotorDirections.ContainsKey("Down"))
        {
            buttons |= MotorButtonMask.Down;
        }

        return buttons;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        StopManualMotorInput();
        RecordedVideosView.PlaybackFailed -= OnRecordedVideoPlaybackFailed;
        _recordingMetadataTimer.Stop();
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.ManualAnalysisSaveRequested -= ViewModel_OnManualAnalysisSaveRequested;
        _viewModel.ManualSystemLogSaveRequested -= ViewModel_OnManualSystemLogSaveRequested;
        _jetsonBridgeSshService.MessageReady -= JetsonBridgeSshService_OnMessageReady;
        _eoUdpCaptureService.FrameReady -= _viewModel.OnEoFrameReady;
        _eoUdpCaptureService.DetectionsReceived -= OnEoDetectionsReceived;
        _eoUdpCaptureService.StatusReceived -= OnYoloStatusReceived;
        _irUdpCaptureService.FrameReady -= _viewModel.OnIrFrameReady;
        _irUdpCaptureService.DetectionsReceived -= OnIrDetectionsReceived;
        _irUdpCaptureService.StatusReceived -= OnYoloStatusReceived;
        _motorStatusReceiverService.StatusReceived -= OnMotorStatusReceived;
        _motorStatusReceiverService.ReceiverError -= OnMotorStatusReceiverError;
        _mobileAlertHubService.Dispose();
        _viewportRecordingService.Dispose();
        _eoUdpCaptureService.Dispose();
        _irUdpCaptureService.Dispose();
        _motorStatusReceiverService.Dispose();
        _motorControlService.Dispose();
        _ = StopJetsonBridgeAfterCloseAsync();
    }

    private async Task StopJetsonBridgeAfterCloseAsync()
    {
        try
        {
            await _jetsonBridgeSshService.StopAsync();
        }
        finally
        {
            _jetsonBridgeSshService.Dispose();
        }
    }

    private void JetsonBridgeSshService_OnMessageReady(string message)
    {
        Dispatcher.Invoke(() => _viewModel.AppendImportantLog(message));
    }

}

