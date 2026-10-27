using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using BroadcastControl.App.Infrastructure;
using BroadcastControl.App.Models;
using BroadcastControl.App.Services;
using BroadcastControl.App.ViewModels.Left;
using BroadcastControl.App.ViewModels.Middle;
using BroadcastControl.App.ViewModels.Right;

namespace BroadcastControl.App.ViewModels;

/// 메인 화면에서 사용하는 상태값을 한곳에서 관리하는 ViewModel이다.
/// 화면 모드, 위험 등급, 카메라 영상, 설정 값, 로그, 줌 상태 같은 UI 데이터를 묶어서 제공한다.
/// 현재는 데모 및 UDP 수신 화면 기준으로 동작하지만, 이후 실제 장비나 VLM 결과가 들어와도 같은 구조를 유지할 수 있도록 구성되어 있다.
public sealed partial class MainViewModel : INotifyPropertyChanged
{
    /// 모드, 위험 등급, 밝기/대조비, 줌, 로그, 테마 버튼 상태를 함께 관리한다.
    // 미니맵은 현재 확대된 영역을 간단히 보여주는 용도이므로, 본 화면 비율에 맞춰 작은 크기로 고정한다.

    private bool _isSystemPoweredOn = true;
    private string _currentMode = "\uC790\uB3D9";
    private string _selectedPrimaryTarget = "\uBCF5\uD569";
    private string _currentThreatLevel = "\uB0AE\uC74C";
    // 프로그램을 처음 켰을 때 밝기는 중간값인 50%에서 시작한다.
    private AppThemeMode _currentThemeMode;
    private double _motorPan;
    private double _motorTilt;
    private int _autoPanMotorStepSize = DefaultMotorStepSize;
    private int _autoTiltMotorStepSize = DefaultMotorStepSize;
    private int _manualPanMotorStepSize = DefaultMotorStepSize;
    private int _manualTiltMotorStepSize = DefaultMotorStepSize;
    private UiLanguage _uiLanguage = UiLanguage.English;
    private string _motorTargetPanText = "0.0";
    private string _motorTargetTiltText = "0.0";
    private bool _isTrackingModeEnabled = true;
    private readonly ApplicationLifecycleService _applicationLifecycleService;
    private readonly ThemeService _themeService;
    private readonly UdpMotorStatusReceiverService _motorStatusReceiverService;
    private readonly MobileAlertHubService _mobileAlertHubService;
    private readonly JetsonBridgeSshService _jetsonBridgeSshService;
    private readonly DetectionService _detectionService;
    private readonly DetectionAlertService _detectionAlertService;
    private readonly RecordingMetadataService _recordingMetadataService;
    private readonly DispatcherTimer _recordingMetadataTimer;
    private string? _lastStatusSignature;
    private DateTime _recordingMetadataWindowStart;
    private bool _servicesStarted;
    private Func<byte[]?>? _evidenceCapture;
    private const double MotorPanLimitDegrees = 360;
    private const double MotorTiltLimitDegrees = 360;
    private const int DefaultMotorStepSize = 8;
    private string? _lastAnalysisMessage;

    public MainViewModel(
        ApplicationLifecycleService applicationLifecycleService,
        ThemeService themeService,
        UdpMotorStatusReceiverService motorStatusReceiverService,
        MobileAlertHubService mobileAlertHubService,
        JetsonBridgeSshService jetsonBridgeSshService,
        DetectionService detectionService,
        DetectionAlertService detectionAlertService,
        RecordingMetadataService recordingMetadataService,
        RecordedVideosViewModel recordedVideos,
        CameraWorkspaceViewModel cameraWorkspace,
        MotorControlPanelViewModel motorControlPanel,
        RecordingPanelViewModel recordingPanel)
    {
        _applicationLifecycleService = applicationLifecycleService;
        _themeService = themeService;
        _motorStatusReceiverService = motorStatusReceiverService;
        _mobileAlertHubService = mobileAlertHubService;
        _jetsonBridgeSshService = jetsonBridgeSshService;
        _detectionService = detectionService;
        _detectionAlertService = detectionAlertService;
        _recordingMetadataService = recordingMetadataService;
        RecordedVideos = recordedVideos;
        _recordingMetadataTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _recordingMetadataTimer.Tick += RecordingMetadataTimer_OnTick;
        Text = new LocalizedTextProvider(() => _uiLanguage);
        CameraWorkspace = cameraWorkspace;
        MotorControlPanel = motorControlPanel;
        DetectionOverlay = new DetectionOverlayViewModel();
        CameraControlBar = new CameraControlBarViewModel(key => Text[key]);
        CameraControlBar.PropertyChanged += (_, e) =>
        {
            OnPropertyChanged(e.PropertyName);
            if (e.PropertyName == nameof(CameraControlBarViewModel.Brightness))
            {
                CameraWorkspace.SetBrightness(CameraControlBar.Brightness);
            }
            else if (e.PropertyName == nameof(CameraControlBarViewModel.Contrast))
            {
                CameraWorkspace.SetContrast(CameraControlBar.Contrast);
            }
        };
        CameraWorkspace.PropertyChanged += (_, e) =>
        {
            OnPropertyChanged(e.PropertyName);
            if (e.PropertyName == nameof(CameraWorkspaceViewModel.IsEoPrimary)) RefreshDetectionOverlay();
        };
        CameraWorkspace.EoFrameReady += OnEoFrameReady;
        CameraWorkspace.IrFrameReady += OnIrFrameReady;
        CameraWorkspace.EoDetectionsReceived += OnEoDetectionsReceived;
        CameraWorkspace.IrDetectionsReceived += OnIrDetectionsReceived;
        CameraWorkspace.StatusReceived += OnYoloStatusReceived;
        MotorDetails = new MotorDetailsViewModel();
        AnalysisPanel = new AnalysisPanelViewModel();
        AnalysisPanel.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName);
        SystemLog = new SystemLogViewModel();
        SettingsDrawer = new SettingsDrawerViewModel();
        RecordingPanel = recordingPanel;
        RecordingPanel.PropertyChanged += (_, e) =>
        {
            OnPropertyChanged(e.PropertyName switch
            {
                nameof(RecordingPanelViewModel.IsManualRecordingEnabled) => nameof(IsManualRecordingEnabled),
                nameof(RecordingPanelViewModel.IsRecordingActive) => nameof(IsRecordingActive),
                _ => e.PropertyName
            });
            OnPropertyChanged(nameof(ManualRecordingButtonText));
            OnPropertyChanged(nameof(RecordingIndicatorBrush));
            OnPropertyChanged(nameof(RecordingTextBrush));
            OnPropertyChanged(nameof(RecordingIndicatorOpacity));
        };
        SettingsDrawer.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName switch
        {
            nameof(SettingsDrawerViewModel.IsOpen) => nameof(IsSettingsOpen),
            _ => e.PropertyName
        });
        MotorDetails.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName switch
        {
            nameof(MotorDetailsViewModel.IsOpen) => nameof(IsMotorDetailsOpen),
            nameof(MotorDetailsViewModel.PanMotorPositionText) => nameof(PanMotorPositionText),
            nameof(MotorDetailsViewModel.TiltMotorPositionText) => nameof(TiltMotorPositionText),
            _ => e.PropertyName
        });
        UpdateRecordingContext();

        // 앱이 현재 사용 중인 테마를 읽어서 설정 창 버튼 상태와 맞춘다.
        if (Application.Current is App app)
        {
            _currentThemeMode = app.CurrentThemeMode;
        }

        SystemLog.Append(DateTime.Now.ToString("HH:mm:ss"), Text["SystemStarted"]);

        PrimaryTargets = new ObservableCollection<PrimaryTargetOption>(CreatePrimaryTargetOptions());

        // 화면의 모든 버튼은 Command 바인딩으로 연결되므로 생성자에서 한 번에 등록한다.
        TogglePowerCommand = new RelayCommand(_ => _applicationLifecycleService.Shutdown());
        SetModeCommand = new RelayCommand(SetMode, _ => IsSystemPoweredOn);
        ToggleSettingsCommand = new RelayCommand(_ => SettingsDrawer.Toggle());
        SelectPrimaryTargetCommand = new RelayCommand(SelectPrimaryTarget, _ => IsSystemPoweredOn);
        ResetBrightnessCommand = new RelayCommand(_ => Brightness = 50, _ => IsSystemPoweredOn);
        ResetContrastCommand = new RelayCommand(_ => Contrast = 50, _ => IsSystemPoweredOn);
        // 확대 제목 버튼을 누르면 기본 배율 x1.0으로 즉시 복귀한다.
        ResetZoomCommand = new RelayCommand(_ => ZoomLevel = 1.0, _ => CanUseZoomControls);
        ToggleManualRecordingCommand = new RelayCommand(_ => ToggleManualRecording(), _ => IsSystemPoweredOn);
        SetThemeCommand = new RelayCommand(SetTheme);
        SetLanguageCommand = new RelayCommand(SetLanguage);
        SaveAnalysisLogsCommand = new RelayCommand(_ => _ = SaveRecordingMetadataAsync(DateTime.Now, DateTime.Now, true, true, false));
        SaveSystemLogsCommand = new RelayCommand(_ => _ = SaveRecordingMetadataAsync(DateTime.Now, DateTime.Now, true, false, true));
        SwapFeedsCommand = new RelayCommand(_ => CameraWorkspace.IsEoPrimary = !CameraWorkspace.IsEoPrimary);
        MoveMotorCommand = new RelayCommand(MoveMotor, _ => CanUseMotorControls);
        SendMotorTargetCommand = new RelayCommand(_ => SendMotorTargetAngles(), _ => CanUseMotorTargetControls);
        AdjustMotorStepCommand = new RelayCommand(AdjustMotorStep, _ => IsSystemPoweredOn);
        ToggleTrackingModeCommand = new RelayCommand(_ => ToggleTrackingMode(), _ => IsSystemPoweredOn);
        ToggleMotorDetailsCommand = new RelayCommand(_ => IsMotorDetailsOpen = !IsMotorDetailsOpen);
        CloseMotorDetailsCommand = new RelayCommand(_ => IsMotorDetailsOpen = false);
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SelectedPrimaryTarget)) RefreshDetectionOverlay();
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public CameraWorkspaceViewModel CameraWorkspace { get; }

    public MotorControlPanelViewModel MotorControlPanel { get; }

    public DetectionOverlayViewModel DetectionOverlay { get; }

    public CameraControlBarViewModel CameraControlBar { get; }

    public MotorDetailsViewModel MotorDetails { get; }

    public AnalysisPanelViewModel AnalysisPanel { get; }

    public SystemLogViewModel SystemLog { get; }

    public SettingsDrawerViewModel SettingsDrawer { get; }

    public RecordingPanelViewModel RecordingPanel { get; }

    public RecordedVideosViewModel RecordedVideos { get; }

    public ObservableCollection<AnalysisItem> AnalysisItems => AnalysisPanel.AnalysisItems;

    public ObservableCollection<SystemLogItem> SystemLogs => SystemLog.SystemLogs;

    public ObservableCollection<MotorStatusItem> PanMotorStatusItems => MotorDetails.PanMotorStatusItems;

    public ObservableCollection<MotorStatusItem> TiltMotorStatusItems => MotorDetails.TiltMotorStatusItems;

    public ObservableCollection<PrimaryTargetOption> PrimaryTargets { get; }

    public LocalizedTextProvider Text { get; }

    public ICommand TogglePowerCommand { get; }

    public ICommand SetModeCommand { get; }

    public ICommand ToggleSettingsCommand { get; }

    public ICommand SelectPrimaryTargetCommand { get; }

    public ICommand ResetBrightnessCommand { get; }

    public ICommand ResetContrastCommand { get; }

    public ICommand ResetZoomCommand { get; }

    public ICommand ToggleManualRecordingCommand { get; }

    public ICommand SetThemeCommand { get; }

    public ICommand SetLanguageCommand { get; }

    public ICommand SaveAnalysisLogsCommand { get; }

    public ICommand SaveSystemLogsCommand { get; }

    public ICommand SwapFeedsCommand { get; }

    public ICommand MoveMotorCommand { get; }

    public ICommand SendMotorTargetCommand { get; }

    public ICommand AdjustMotorStepCommand { get; }

    public ICommand ToggleTrackingModeCommand { get; }

    public ICommand ToggleMotorDetailsCommand { get; }

    public ICommand CloseMotorDetailsCommand { get; }

    public bool IsSettingsOpen
    {
        get => SettingsDrawer.IsOpen;
        set => SettingsDrawer.IsOpen = value;
    }

    public bool IsSystemPoweredOn
    {
        get => _isSystemPoweredOn;
        private set
        {
            if (SetProperty(ref _isSystemPoweredOn, value))
            {
                CameraControlBar.SetSystemPoweredOn(value);
                UpdateRecordingContext();
                OnPropertyChanged(nameof(IsManualMode));
                OnPropertyChanged(nameof(ManualRecordingButtonOpacity));
                OnPropertyChanged(nameof(ManualRecordingButtonText));
                OnPropertyChanged(nameof(CanSelectAutoMode));
                OnPropertyChanged(nameof(CanSelectManualMode));
                OnPropertyChanged(nameof(CanUseMotorControls));
                OnPropertyChanged(nameof(CanUseMotorTargetControls));
                OnPropertyChanged(nameof(MotorControlsOpacity));
                OnPropertyChanged(nameof(CanUseZoomControls));
                OnPropertyChanged(nameof(TrackingModeOpacity));
                OnPropertyChanged(nameof(IsAutoMode));
                OnPropertyChanged(nameof(IsRecordingActive));
                OnPropertyChanged(nameof(RecordingIndicatorBrush));
                OnPropertyChanged(nameof(RecordingTextBrush));
                OnPropertyChanged(nameof(RecordingIndicatorOpacity));
                RaiseAllCommandStates();
            }
        }
    }

    public bool IsEoPrimary => CameraWorkspace.IsEoPrimary;

    // 상단 전원 버튼은 실제 프로그램 종료 버튼으로 사용한다.
    public string PowerButtonText => Text["PowerExit"];

    public string CurrentMode
    {
        get => _currentMode;
        private set
        {
            if (SetProperty(ref _currentMode, value))
            {
                UpdateRecordingContext();
                OnPropertyChanged(nameof(CurrentModeText));
                OnPropertyChanged(nameof(AutoModeOpacity));
                OnPropertyChanged(nameof(ManualModeOpacity));
                OnPropertyChanged(nameof(IsManualMode));
                OnPropertyChanged(nameof(ManualRecordingButtonOpacity));
                OnPropertyChanged(nameof(ManualRecordingButtonText));
                OnPropertyChanged(nameof(CanSelectAutoMode));
                OnPropertyChanged(nameof(CanSelectManualMode));
                OnPropertyChanged(nameof(CanUseMotorControls));
                OnPropertyChanged(nameof(CanUseMotorTargetControls));
                OnPropertyChanged(nameof(MotorControlsOpacity));
                OnPropertyChanged(nameof(CanUseZoomControls));
                OnPropertyChanged(nameof(ShowZoomMiniMap));
                OnPropertyChanged(nameof(TrackingModeOpacity));
                OnPropertyChanged(nameof(IsAutoMode));
                OnPropertyChanged(nameof(IsRecordingActive));
                OnPropertyChanged(nameof(RecordingIndicatorBrush));
                OnPropertyChanged(nameof(RecordingTextBrush));
                OnPropertyChanged(nameof(RecordingIndicatorOpacity));
                RaiseAllCommandStates();
            }
        }
    }

    public string CurrentModeText => $"{Text["CameraMode"]}: {TranslateMode(CurrentMode)}";

    // 현재 선택된 모드 버튼만 선명하게 보여서 별도 텍스트 없이도 상태를 알아볼 수 있게 한다.
    public double AutoModeOpacity => CurrentMode == "\uC790\uB3D9" ? 1.0 : 0.35;

    public double ManualModeOpacity => CurrentMode == "\uC218\uB3D9" ? 1.0 : 0.35;

    // 녹화 상태는 현재 수동 녹화 여부와 자동 녹화 조건을 함께 반영한 결과값이다.
    // 자동 모드에서는 위험 등급이 높음일 때만 자동 녹화 상태로 간주하고,
    // 수동 모드에서는 사용자가 직접 녹화를 켠 경우에만 활성화된다.

    public bool IsRecordingActive => RecordingPanel.IsRecordingActive;

    public Brush RecordingIndicatorBrush => RecordingPanel.RecordingIndicatorBrush;

    public Brush RecordingTextBrush => RecordingPanel.RecordingTextBrush;

    public double RecordingIndicatorOpacity => RecordingPanel.RecordingIndicatorOpacity;

    public bool IsManualMode => IsSystemPoweredOn && CurrentMode == "\uC218\uB3D9";

    public bool IsAutoMode => IsSystemPoweredOn && CurrentMode == "\uC790\uB3D9";

    public bool CanSelectAutoMode => IsSystemPoweredOn && CurrentMode != "\uC790\uB3D9";

    public bool CanSelectManualMode => IsSystemPoweredOn && CurrentMode != "\uC218\uB3D9";

    public bool CanUseMotorControls => IsManualMode;

    public bool CanUseMotorTargetControls => IsManualMode;

    public double MotorControlsOpacity => CanUseMotorControls ? 1.0 : 0.38;

    public int AutoPanMotorStepSize
    {
        get => _autoPanMotorStepSize;
        private set
        {
            var normalized = Math.Clamp(value, 1, 10);
            if (SetProperty(ref _autoPanMotorStepSize, normalized))
            {
                OnPropertyChanged(nameof(AutoPanMotorStepSizeText));
            }
        }
    }

    public int AutoTiltMotorStepSize
    {
        get => _autoTiltMotorStepSize;
        private set
        {
            var normalized = Math.Clamp(value, 1, 10);
            if (SetProperty(ref _autoTiltMotorStepSize, normalized))
            {
                OnPropertyChanged(nameof(AutoTiltMotorStepSizeText));
            }
        }
    }

    public int ManualPanMotorStepSize
    {
        get => _manualPanMotorStepSize;
        private set
        {
            var normalized = Math.Clamp(value, 1, 10);
            if (SetProperty(ref _manualPanMotorStepSize, normalized))
            {
                OnPropertyChanged(nameof(ManualPanMotorStepSizeText));
            }
        }
    }

    public int ManualTiltMotorStepSize
    {
        get => _manualTiltMotorStepSize;
        private set
        {
            var normalized = Math.Clamp(value, 1, 10);
            if (SetProperty(ref _manualTiltMotorStepSize, normalized))
            {
                OnPropertyChanged(nameof(ManualTiltMotorStepSizeText));
            }
        }
    }

    public string AutoPanMotorStepSizeText => AutoPanMotorStepSize.ToString(CultureInfo.InvariantCulture);

    public string AutoTiltMotorStepSizeText => AutoTiltMotorStepSize.ToString(CultureInfo.InvariantCulture);

    public string ManualPanMotorStepSizeText => ManualPanMotorStepSize.ToString(CultureInfo.InvariantCulture);

    public string ManualTiltMotorStepSizeText => ManualTiltMotorStepSize.ToString(CultureInfo.InvariantCulture);

    public bool IsTrackingModeEnabled
    {
        get => _isTrackingModeEnabled;
        private set
        {
            if (SetProperty(ref _isTrackingModeEnabled, value))
            {
                OnPropertyChanged(nameof(TrackingModeText));
                OnPropertyChanged(nameof(TrackingModeOpacity));
            }
        }
    }

    public string TrackingModeText => IsTrackingModeEnabled ? Text["TrackingOn"] : Text["TrackingOff"];

    public double TrackingModeOpacity => IsSystemPoweredOn
        ? (IsTrackingModeEnabled ? 1.0 : 0.42)
        : 0.32;

    public string PanMotorPositionText => MotorDetails.PanMotorPositionText;

    public string TiltMotorPositionText => MotorDetails.TiltMotorPositionText;

    public bool IsMotorDetailsOpen
    {
        get => MotorDetails.IsOpen;
        set => MotorDetails.IsOpen = value;
    }

    public bool CanUseZoomControls => CameraControlBar.CanUseZoomControls;

    public double ManualRecordingButtonOpacity => IsSystemPoweredOn ? 1.0 : 0.45;

    public bool IsDarkThemeActive => _currentThemeMode == AppThemeMode.Dark;

    public bool IsLightThemeActive => _currentThemeMode == AppThemeMode.Light;

    public double DarkThemeButtonOpacity => IsDarkThemeActive ? 1.0 : 0.55;

    public double LightThemeButtonOpacity => IsLightThemeActive ? 1.0 : 0.55;

    public bool IsEnglishLanguage => _uiLanguage == UiLanguage.English;

    public bool IsKoreanLanguage => _uiLanguage == UiLanguage.Korean;

    public double EnglishLanguageButtonOpacity => IsEnglishLanguage ? 1.0 : 0.55;

    public double KoreanLanguageButtonOpacity => IsKoreanLanguage ? 1.0 : 0.55;

    // 수동 녹화는 수동 모드에서만 켜고 끌 수 있도록 제한한다.
    public bool IsManualRecordingEnabled => RecordingPanel.IsManualRecordingEnabled;

    public string ManualRecordingButtonText => IsRecordingActive ? Text["StopRecording"] : Text["StartRecording"];

    public string CurrentThreatLevel
    {
        get => _currentThreatLevel;
        private set
        {
            if (SetProperty(ref _currentThreatLevel, value))
            {
                AnalysisPanel.SetThreatLevel(value);
                UpdateRecordingContext();

                OnPropertyChanged(nameof(CurrentThreatText));
                OnPropertyChanged(nameof(CurrentThreatBrush));
                OnPropertyChanged(nameof(IsRecordingActive));
                OnPropertyChanged(nameof(RecordingIndicatorBrush));
                OnPropertyChanged(nameof(RecordingTextBrush));
                OnPropertyChanged(nameof(RecordingIndicatorOpacity));
                OnPropertyChanged(nameof(ManualRecordingButtonText));
            }
        }
    }

    public string CurrentThreatText => $"{Text["ThreatLevel"]}: {TranslateThreatLevel(CurrentThreatLevel)}";

    public Brush CurrentThreatBrush => AnalysisPanel.CurrentThreatBrush;

    public string SelectedPrimaryTarget
    {
        get => _selectedPrimaryTarget;
        private set
        {
            if (SetProperty(ref _selectedPrimaryTarget, value))
            {
                OnPropertyChanged(nameof(PrimaryTargetText));
                OnPropertyChanged(nameof(PrimaryTargetShortText));
            }
        }
    }

    public string PrimaryTargetText => $"{Text["PrimaryTarget"]}: {TranslatePrimaryTarget(SelectedPrimaryTarget)}";

    public string PrimaryTargetShortText => $"{Text["PrimaryTarget"]}: {GetShortPrimaryTargetName(SelectedPrimaryTarget)}";

    // 카메라 이름은 짧고 명확하게 유지해서 실제 화면을 가리지 않도록 한다.
    public string EoTitle => CameraWorkspace.EoTitle;

    public string IrTitle => CameraWorkspace.IrTitle;

    public string EoSubtitle => CameraWorkspace.EoSubtitle;

    public string IrSubtitle => CameraWorkspace.IrSubtitle;

    public string LargeFeedTitle => CameraWorkspace.LargeFeedTitle;

    public string InsetFeedTitle => CameraWorkspace.InsetFeedTitle;

    public double LargeFeedRotationAngle => CameraWorkspace.LargeFeedRotationAngle;

    public double InsetFeedRotationAngle => CameraWorkspace.InsetFeedRotationAngle;

    public Stretch LargeFeedStretch => CameraWorkspace.LargeFeedStretch;

    public Stretch InsetFeedStretch => CameraWorkspace.InsetFeedStretch;

    public string LargeFeedSubtitle => CameraWorkspace.LargeFeedSubtitle;

    public string InsetFeedSubtitle => CameraWorkspace.InsetFeedSubtitle;

    public double Brightness
    {
        get => CameraControlBar.Brightness;
        set => CameraControlBar.Brightness = value;
    }

    public string BrightnessText => CameraControlBar.BrightnessText;

    public double Contrast
    {
        get => CameraControlBar.Contrast;
        set => CameraControlBar.Contrast = value;
    }

    public string ContrastText => CameraControlBar.ContrastText;

    public double ZoomLevel
    {
        get => CameraControlBar.ZoomLevel;
        set => CameraControlBar.ZoomLevel = value;
    }

    public string ZoomLevelText => CameraControlBar.ZoomLevelText;

    public double LargeFeedScale => CameraControlBar.LargeFeedScale;

    public double ZoomTransformX => CameraControlBar.ZoomTransformX;

    public double ZoomTransformY => CameraControlBar.ZoomTransformY;

    public bool ShowZoomMiniMap => CameraControlBar.ShowZoomMiniMap;

    public double MiniMapViewportWidth => CameraControlBar.MiniMapViewportWidth;

    public double MiniMapViewportHeight => CameraControlBar.MiniMapViewportHeight;

    public double MiniMapViewportLeft => CameraControlBar.MiniMapViewportLeft;

    public double MiniMapViewportTop => CameraControlBar.MiniMapViewportTop;
    public string MotorPanText => $"모터 좌우: {_motorPan:0.0}도";

    public string MotorTiltText => $"모터 상하: {_motorTilt:0.0}도";

    public string MotorTargetPanText
    {
        get => _motorTargetPanText;
        set => SetProperty(ref _motorTargetPanText, value);
    }

    public string MotorTargetTiltText
    {
        get => _motorTargetTiltText;
        set => SetProperty(ref _motorTargetTiltText, value);
    }
    public void UpdateDetectionSummary(IReadOnlyList<DetectionInfo> detections)
    {
        // 자동 모드에서는 탐지 유무를 보고 스캔(0)과 추적(1) 모드 패킷을 전환한다.
        if (!DetectionOverlay.UpdateDetectionState(detections))
        {
            return;
        }

        if (!IsAutoMode)
        {
            return;
        }

        if (!TrySendCurrentModeToController(out var modeError))
        {
            AppendImportantLog($"자동 모드 상태 전송에 실패했습니다: {modeError}");
            return;
        }

    }

    public void ApplyVlmAnalysisResult(string threatLevel, string analysisMessage)
    {
        var normalizedThreatLevel = NormalizeThreatLevel(threatLevel);
        var threatChanged = !string.Equals(CurrentThreatLevel, normalizedThreatLevel, StringComparison.Ordinal);
        CurrentThreatLevel = normalizedThreatLevel;

        if (!string.IsNullOrWhiteSpace(analysisMessage) &&
            !string.Equals(_lastAnalysisMessage, analysisMessage, StringComparison.Ordinal))
        {
            _lastAnalysisMessage = analysisMessage;
            AppendAnalysisLog(analysisMessage);
        }

        if (threatChanged)
        {
            AppendImportantLog($"위험 등급이 {CurrentThreatLevel}(으)로 변경되었습니다.");
        }
    }

    public void AppendImportantLog(string message) =>
        SystemLog.Append(DateTime.Now.ToString("HH:mm:ss"), message);

    private void AppendAnalysisLog(string message) =>
        AnalysisPanel.Append(DateTime.Now.ToString("HH:mm:ss"), message);

    public void InitializeMotorControlState()
    {
        if (!TrySendCurrentModeToController(out var modeError))
        {
            AppendImportantLog($"초기 모터 모드 전송에 실패했습니다: {modeError}");
            return;
        }

        if (!TrySendButtonPacket(MotorButtonMask.None, out var buttonError))
        {
            AppendImportantLog($"초기 버튼 패킷 전송에 실패했습니다: {buttonError}");
        }

        if (!TrySendStepSizePacket(out var stepError))
        {
            AppendImportantLog($"초기 모터 step size 전송에 실패했습니다: {stepError}");
        }
    }

    public void MoveMotorStep(string direction)
    {
        if (!TryMapDirectionToButton(direction, out var buttons))
        {
            return;
        }

        UpdateManualButtonState(buttons);
    }

    public void SetMotorPosition(double panDegrees, double tiltDegrees)
    {
        _motorPan = NormalizeMotorDegrees(panDegrees, MotorPanLimitDegrees);
        _motorTilt = NormalizeMotorDegrees(tiltDegrees, MotorTiltLimitDegrees);
        MotorDetails.SetMotorPosition(_motorPan, _motorTilt);

        OnPropertyChanged(nameof(MotorPanText));
        OnPropertyChanged(nameof(MotorTiltText));
    }

    public void UpdateManualButtonState(MotorButtonMask buttons)
    {
        if (!IsManualMode)
        {
            return;
        }

        ApplyMotorButtonStateToUi(buttons);

        if (!TrySendCurrentModeToController(out var modeError))
        {
            AppendImportantLog($"모터 수동 모드 전송에 실패했습니다: {modeError}");
            return;
        }

        if (!TrySendButtonPacket(buttons, out var buttonError))
        {
            AppendImportantLog($"모터 버튼 패킷 전송에 실패했습니다: {buttonError}");
        }
    }

    public async Task StartServicesAsync()
    {
        if (_servicesStarted) return;
        _servicesStarted = true;
        _motorStatusReceiverService.StatusReceived += OnMotorStatusReceived;
        _motorStatusReceiverService.ReceiverError += OnMotorStatusReceiverError;
        _jetsonBridgeSshService.MessageReady += OnJetsonMessageReady;

        InitializeMotorControlState();
        _motorStatusReceiverService.Start();
        AppendImportantLog($"Motor status receiver listening on port {_motorStatusReceiverService.Port}.");
        _recordingMetadataWindowStart = DateTime.Now;
        _recordingMetadataTimer.Start();
        await _jetsonBridgeSshService.StartAsync();

        var streams = CameraWorkspace.StartStreams(Brightness, Contrast);
        if (!streams.EoStarted) AppendImportantLog("Failed to start the EO UDP stream receiver on port 6000.");
        if (!streams.IrStarted) AppendImportantLog("Failed to start the IR UDP stream receiver on port 6001.");
        if (!_mobileAlertHubService.Start(8088))
            AppendImportantLog("Failed to start the mobile alert hub on port 8088.");
        else
            AppendImportantLog($"Mobile alert hub started. {_mobileAlertHubService.AccessHintUrls}");
    }

    public async Task StopServicesAsync()
    {
        if (!_servicesStarted) return;
        _servicesStarted = false;
        _recordingMetadataTimer.Stop();
        _motorStatusReceiverService.StatusReceived -= OnMotorStatusReceived;
        _motorStatusReceiverService.ReceiverError -= OnMotorStatusReceiverError;
        _jetsonBridgeSshService.MessageReady -= OnJetsonMessageReady;
        var recordingStopMessage = RecordingPanel.UpdateRecordingServiceState(false);
        if (!string.IsNullOrWhiteSpace(recordingStopMessage)) AppendImportantLog(recordingStopMessage);
        CameraWorkspace.StopStreams();
        _motorStatusReceiverService.Dispose();
        _mobileAlertHubService.Dispose();
        try
        {
            await _jetsonBridgeSshService.StopAsync();
        }
        finally
        {
            _jetsonBridgeSshService.Dispose();
        }
    }

    public void SetEvidenceCaptureProvider(Func<byte[]?> captureProvider)
    {
        _evidenceCapture = captureProvider;
        RecordingPanel.SetEvidenceCaptureProvider(captureProvider);
        HandleRecordingActiveStateChanged();
    }

    public void UpdateCameraViewport(double width, double height)
    {
        CameraControlBar.UpdateViewportSize(width, height);
        UpdateCameraTransform(width, height);
    }

    public void UpdateCameraTransform(double width, double height)
    {
        CameraWorkspace.UpdateViewportTransform(ZoomLevel, ZoomTransformX, ZoomTransformY, width, height);
    }

    private void OnEoFrameReady(ReceivedVideoFrame frame)
    {
        _detectionService.StoreEoFrame(frame);
        CameraWorkspace.OnEoFrameReady(frame);
        RefreshDetectionOverlay();
    }

    private void OnIrFrameReady(ReceivedVideoFrame frame)
    {
        _detectionService.StoreIrFrame(frame);
        CameraWorkspace.OnIrFrameReady(frame);
        RefreshDetectionOverlay();
    }

    private void OnEoDetectionsReceived(DetectionPacket packet) => HandleDetections(true, packet);
    private void OnIrDetectionsReceived(DetectionPacket packet) => HandleDetections(false, packet);

    private void HandleDetections(bool isEo, DetectionPacket packet)
    {
        var filtered = _detectionService.StoreAndFilterDetections(isEo, packet, SelectedPrimaryTarget);
        if (isEo == CameraWorkspace.IsEoPrimary)
        {
            UpdateDetectionSummary(filtered);
            _ = UpdateAnalysisAndAlertAsync(packet.FrameId, filtered);
        }
        RefreshDetectionOverlay();
    }

    public void OnMiddleOverlayRefreshRequested(object? sender, EventArgs e) => RefreshDetectionOverlay();

    private void RefreshDetectionOverlay()
    {
        if (!_detectionService.TryGetRenderableFrameAndDetection(
                CameraWorkspace.IsEoPrimary, out var frame, out var packet))
        {
            DetectionOverlay.SetDisplayDetections(Array.Empty<DetectionInfo>(), 1, 1);
            return;
        }

        var display = DetectionService.FilterDisplayDetections(packet.Detections, SelectedPrimaryTarget);
        UpdateDetectionSummary(display);
        DetectionOverlay.SetDisplayDetections(
            display,
            packet.Width > 0 ? packet.Width : frame.Width,
            packet.Height > 0 ? packet.Height : frame.Height);
    }

    private void OnYoloStatusReceived(YoloStatusPacket packet)
    {
        var signature = $"{packet.Enabled}:{packet.ModelLoaded}:{packet.ConfThreshold}:{packet.LastError}:{packet.Source}";
        if (string.Equals(_lastStatusSignature, signature, StringComparison.Ordinal)) return;
        _lastStatusSignature = signature;
        if (!packet.ModelLoaded) AppendImportantLog("YOLO model is not loaded.");
        if (!string.IsNullOrWhiteSpace(packet.LastError)) AppendImportantLog($"YOLO status error: {packet.LastError}");
    }

    private void OnMotorStatusReceived(object? sender, MotorStatusSnapshot snapshot) =>
        DispatchToUi(() => MotorDetails.UpdateMotorStatus(snapshot));

    private void OnMotorStatusReceiverError(object? sender, string message) =>
        DispatchToUi(() => AppendImportantLog($"Motor status receiver error: {message}"));

    private void OnJetsonMessageReady(string message) => DispatchToUi(() => AppendImportantLog(message));

    private async Task UpdateAnalysisAndAlertAsync(uint frameId, IReadOnlyList<DetectionInfo> detections)
    {
        if (detections.Count == 0)
        {
            ApplyVlmAnalysisResult("Low", "선택된 주 탐지체에 해당하는 위협 객체가 탐지되지 않았습니다.");
            return;
        }
        var analysis = AnalysisPanel.BuildDetectionAnalysis(LargeFeedTitle, SelectedPrimaryTarget, detections.Count);
        var summary = AnalysisPanel.BuildDetectionSummary(detections);
        ApplyVlmAnalysisResult("High", $"{analysis} 감지 정보: {summary}");
        try
        {
            if (await _detectionAlertService.TryPublishAsync(
                    SelectedPrimaryTarget, frameId, detections, analysis, summary, CurrentThreatLevel,
                    _evidenceCapture?.Invoke()))
                AppendImportantLog("모바일 위험 알림을 전송했습니다.");
        }
        catch (Exception ex)
        {
            AppendImportantLog($"위험 알림 전송 실패: {ex.Message}");
        }
    }

    private void HandleRecordingActiveStateChanged()
    {
        var message = RecordingPanel.UpdateRecordingServiceState(IsRecordingActive);
        if (!string.IsNullOrWhiteSpace(message)) AppendImportantLog(message);
    }

    private async void RecordingMetadataTimer_OnTick(object? sender, EventArgs e)
    {
        var end = DateTime.Now;
        var start = _recordingMetadataWindowStart == default ? end.AddMinutes(-1) : _recordingMetadataWindowStart;
        _recordingMetadataWindowStart = end;
        await SaveRecordingMetadataAsync(start, end, false, true, true);
    }

    private async Task SaveRecordingMetadataAsync(
        DateTime start, DateTime end, bool manual, bool includeAnalysis, bool includeSystemLog)
    {
        try
        {
            await _recordingMetadataService.SaveAsync(
                AnalysisItems, SystemLogs, start, end, manual, includeAnalysis, includeSystemLog);
            if (manual) AppendImportantLog("Recording metadata saved.");
        }
        catch (Exception ex)
        {
            AppendImportantLog($"Recording metadata save failed: {ex.Message}");
        }
    }

    private static void DispatchToUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }

    private static string NormalizeThreatLevel(string threatLevel)
    {
        return threatLevel.Trim().ToLowerInvariant() switch
        {
            "high" or "높음" => "\uB192\uC74C",
            "medium" or "중간" => "\uC911\uAC04",
            _ => "\uB0AE\uC74C"
        };
    }

    private bool TrySendCurrentModeToController(out string? error)
    {
        var mode = IsManualMode
            ? MotorPacketMode.Manual
            : IsTrackingModeEnabled && DetectionOverlay.HasTrackedTarget
                ? MotorPacketMode.Tracking
                : MotorPacketMode.Scan;

        return MotorControlPanel.TrySendModePacket(mode, out error);
    }

    private bool TrySendButtonPacket(MotorButtonMask buttons, out string? error)
    {
        return MotorControlPanel.TrySendButtonPacket(buttons, out error);
    }

    private bool TrySendStepSizePacket(out string? error)
    {
        var mode = IsManualMode ? MotorStepMode.Manual : MotorStepMode.Auto;
        return MotorControlPanel.TrySendStepSizePacket(GetPanStepSize(mode), GetTiltStepSize(mode), out error);
    }

    private void ApplyMotorButtonStateToUi(MotorButtonMask buttons)
    {
        if ((buttons & MotorButtonMask.Center) == MotorButtonMask.Center)
        {
            _motorPan = 0;
            _motorTilt = 0;
        }
        else
        {
            if ((buttons & MotorButtonMask.Left) == MotorButtonMask.Left)
            {
                _motorPan = Math.Clamp(_motorPan - ManualPanMotorStepSize, 0, MotorPanLimitDegrees);
            }

            if ((buttons & MotorButtonMask.Right) == MotorButtonMask.Right)
            {
                _motorPan = Math.Clamp(_motorPan + ManualPanMotorStepSize, 0, MotorPanLimitDegrees);
            }

            if ((buttons & MotorButtonMask.Up) == MotorButtonMask.Up)
            {
                _motorTilt = Math.Clamp(_motorTilt + ManualTiltMotorStepSize, 0, MotorTiltLimitDegrees);
            }

            if ((buttons & MotorButtonMask.Down) == MotorButtonMask.Down)
            {
                _motorTilt = Math.Clamp(_motorTilt - ManualTiltMotorStepSize, 0, MotorTiltLimitDegrees);
            }
        }

        MotorDetails.SetMotorPosition(_motorPan, _motorTilt);

        OnPropertyChanged(nameof(MotorPanText));
        OnPropertyChanged(nameof(MotorTiltText));
    }

    private static bool TryMapDirectionToButton(string direction, out MotorButtonMask buttons)
    {
        buttons = direction switch
        {
            "Left" => MotorButtonMask.Right,
            "Right" => MotorButtonMask.Left,
            "Up" => MotorButtonMask.Up,
            "Down" => MotorButtonMask.Down,
            "Center" => MotorButtonMask.Center,
            _ => MotorButtonMask.None
        };

        return buttons != MotorButtonMask.None;
    }

    private void UpdateRecordingContext() => RecordingPanel.UpdateContext(
        IsSystemPoweredOn, IsManualMode, IsAutoMode, CurrentThreatLevel == "\uB192\uC74C");

    private void SetMode(object? parameter)
    {
        if (!IsSystemPoweredOn || parameter is not string mode ||
            mode is not ("\uC790\uB3D9" or "\uC218\uB3D9") || mode == CurrentMode) return;

        CurrentMode = mode;
        if (!TrySendCurrentModeToController(out var modeError))
            AppendImportantLog($"Motor mode send failed: {modeError}");
        if (!TrySendStepSizePacket(out var stepError))
            AppendImportantLog($"Motor step size send failed: {stepError}");
        if (IsAutoMode && !TrySendButtonPacket(MotorButtonMask.None, out var buttonError))
            AppendImportantLog($"Motor button reset failed: {buttonError}");
    }

    private void SelectPrimaryTarget(object? parameter)
    {
        if (IsSystemPoweredOn && parameter is string target &&
            PrimaryTargets.Any(option => option.Value == target))
            SelectedPrimaryTarget = target;
    }

    private void ToggleManualRecording() => RecordingPanel.ToggleManualRecording();

    private void SetTheme(object? parameter)
    {
        if (parameter is not string themeName) return;
        _currentThemeMode = string.Equals(themeName, "Light", StringComparison.OrdinalIgnoreCase)
            ? AppThemeMode.Light : AppThemeMode.Dark;
        _themeService.Apply(_currentThemeMode);
        OnPropertyChanged(nameof(IsDarkThemeActive));
        OnPropertyChanged(nameof(IsLightThemeActive));
        OnPropertyChanged(nameof(DarkThemeButtonOpacity));
        OnPropertyChanged(nameof(LightThemeButtonOpacity));
    }

    private void SetLanguage(object? parameter)
    {
        if (parameter is not string languageName) return;
        var language = string.Equals(languageName, "Korean", StringComparison.OrdinalIgnoreCase)
            ? UiLanguage.Korean : UiLanguage.English;
        if (_uiLanguage == language) return;
        _uiLanguage = language;
        Text.Refresh();
        foreach (var option in PrimaryTargets)
            option.DisplayName = TranslatePrimaryTarget(option.Value);
        foreach (var property in new[] { nameof(PowerButtonText), nameof(CurrentModeText),
                     nameof(ManualRecordingButtonText), nameof(TrackingModeText), nameof(CurrentThreatText),
                     nameof(PrimaryTargetText), nameof(PrimaryTargetShortText), nameof(IsEnglishLanguage),
                     nameof(IsKoreanLanguage), nameof(EnglishLanguageButtonOpacity), nameof(KoreanLanguageButtonOpacity) })
            OnPropertyChanged(property);
    }

    private void MoveMotor(object? parameter)
    {
        if (CanUseMotorControls && parameter is string direction) MoveMotorStep(direction);
    }

    private void SendMotorTargetAngles()
    {
        if (!CanUseMotorTargetControls ||
            !double.TryParse(MotorTargetPanText, NumberStyles.Float, CultureInfo.InvariantCulture, out var pan) ||
            !double.TryParse(MotorTargetTiltText, NumberStyles.Float, CultureInfo.InvariantCulture, out var tilt))
        {
            AppendImportantLog("Enter valid motor angles between 0 and 360.");
            return;
        }
        pan = NormalizeMotorDegrees(pan, MotorPanLimitDegrees);
        tilt = NormalizeMotorDegrees(tilt, MotorTiltLimitDegrees);
        MotorTargetPanText = pan.ToString("0.0", CultureInfo.InvariantCulture);
        MotorTargetTiltText = tilt.ToString("0.0", CultureInfo.InvariantCulture);
        if (!MotorControlPanel.TrySendTargetAnglePacket(pan, tilt, out var error))
        {
            AppendImportantLog($"Motor target send failed: {error}");
            return;
        }
        SetMotorPosition(pan, tilt);
    }

    private void AdjustMotorStep(object? parameter)
    {
        if (parameter is not string text) return;
        var parts = text.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var mode = parts.Length > 0 && parts[0].Equals("Manual", StringComparison.OrdinalIgnoreCase)
            ? MotorStepMode.Manual : MotorStepMode.Auto;
        var axis = parts.Length > 1 && parts[1].Equals("Tilt", StringComparison.OrdinalIgnoreCase)
            ? MotorStepAxis.Tilt : parts.Length > 1 && parts[1].Equals("Pan", StringComparison.OrdinalIgnoreCase)
                ? MotorStepAxis.Pan : MotorStepAxis.Both;
        var operation = parts.Length > 2 ? parts[2] : parts.Length > 1 && axis == MotorStepAxis.Both ? parts[1] : string.Empty;
        var reset = operation.Equals("Reset", StringComparison.OrdinalIgnoreCase);
        var delta = 0;
        if (!reset && !int.TryParse(operation, NumberStyles.Integer, CultureInfo.InvariantCulture, out delta))
        {
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out delta)) return;
            axis = MotorStepAxis.Both;
        }
        else if (reset) delta = 0;

        if (axis is MotorStepAxis.Pan or MotorStepAxis.Both)
        {
            if (mode == MotorStepMode.Auto) AutoPanMotorStepSize = reset ? DefaultMotorStepSize : AutoPanMotorStepSize + delta;
            else ManualPanMotorStepSize = reset ? DefaultMotorStepSize : ManualPanMotorStepSize + delta;
        }
        if (axis is MotorStepAxis.Tilt or MotorStepAxis.Both)
        {
            if (mode == MotorStepMode.Auto) AutoTiltMotorStepSize = reset ? DefaultMotorStepSize : AutoTiltMotorStepSize + delta;
            else ManualTiltMotorStepSize = reset ? DefaultMotorStepSize : ManualTiltMotorStepSize + delta;
        }
        if ((mode == MotorStepMode.Auto && IsAutoMode || mode == MotorStepMode.Manual && IsManualMode) &&
            !TrySendStepSizePacket(out var error)) AppendImportantLog($"Motor step size send failed: {error}");
    }

    private void ToggleTrackingMode()
    {
        if (!IsSystemPoweredOn) return;
        IsTrackingModeEnabled = !IsTrackingModeEnabled;
        if (!TrySendCurrentModeToController(out var error)) AppendImportantLog($"Tracking mode send failed: {error}");
    }

    private void RaiseAllCommandStates()
    {
        foreach (var command in new[] { SetModeCommand, SelectPrimaryTargetCommand, ResetBrightnessCommand,
                     ResetContrastCommand, ResetZoomCommand, ToggleManualRecordingCommand, MoveMotorCommand,
                     SendMotorTargetCommand, AdjustMotorStepCommand, ToggleTrackingModeCommand })
            if (command is RelayCommand relay) relay.RaiseCanExecuteChanged();
    }

    private string TranslateMode(string mode) => mode switch
    {
        "\uC790\uB3D9" => Text["AutoMode"], "\uC218\uB3D9" => Text["ManualMode"], _ => mode
    };

    private string TranslateThreatLevel(string level) => level switch
    {
        "\uB192\uC74C" => Text["ThreatHigh"], "\uC911\uAC04" => Text["ThreatMedium"], _ => Text["ThreatLow"]
    };

    private string TranslatePrimaryTarget(string target) => target switch
    {
        "\uBCF5\uD569" => Text["TargetComposite"], "\uC0AC\uB78C" => Text["TargetPerson"],
        "\uBB34\uAE30\uCCB4\uACC4" => Text["TargetWeapon"], "\uD1B5\uC2E0 \uC7A5\uBE44" => Text["TargetComm"],
        "\uBE44\uAD70\uC0AC \uD45C\uC801" => Text["TargetCivil"], _ => target
    };

    private string GetShortPrimaryTargetName(string target) => TranslatePrimaryTarget(target);

    private IEnumerable<PrimaryTargetOption> CreatePrimaryTargetOptions() => new[]
    {
        "\uBCF5\uD569", "\uC0AC\uB78C", "\uBB34\uAE30\uCCB4\uACC4",
        "\uD1B5\uC2E0 \uC7A5\uBE44", "\uBE44\uAD70\uC0AC \uD45C\uC801"
    }.Select(target => new PrimaryTargetOption(target, TranslatePrimaryTarget(target)));

    private int GetPanStepSize(MotorStepMode mode) => mode == MotorStepMode.Auto ? AutoPanMotorStepSize : ManualPanMotorStepSize;
    private int GetTiltStepSize(MotorStepMode mode) => mode == MotorStepMode.Auto ? AutoTiltMotorStepSize : ManualTiltMotorStepSize;
    private static double NormalizeMotorDegrees(double degrees, double limit) =>
        Math.Clamp(Math.Round(degrees, 1, MidpointRounding.AwayFromZero), 0, limit);

    /// <summary>
    /// ViewModel 공통 속성 변경 도우미 메서드다.
    /// 값이 실제로 바뀐 경우에만 PropertyChanged를 발생시켜 불필요한 화면 갱신을 줄인다.
    /// </summary>
    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        if (propertyName == nameof(IsRecordingActive)) HandleRecordingActiveStateChanged();
    }
}
