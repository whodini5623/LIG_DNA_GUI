using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Media;
using BroadcastControl.App.Models;
using BroadcastControl.App.Services;

namespace BroadcastControl.App.ViewModels.Middle;

/// <summary>
/// View state and actions for the corresponding CameraWorkspace.
/// </summary>
public sealed class CameraWorkspaceViewModel : INotifyPropertyChanged
{
    private const int EoUdpPort = 6000;
    private const int IrUdpPort = 6001;
    private readonly UdpEncodedVideoReceiverService _eoReceiver;
    private readonly UdpEncodedVideoReceiverService _irReceiver;
    private ImageSource? _eoFrame;
    private ImageSource? _irFrame;
    private readonly ImageSource _eoPlaceholderFrame = CreatePlaceholderFrame(Color.FromRgb(51, 94, 160));
    private readonly ImageSource _irPlaceholderFrame = CreatePlaceholderFrame(Color.FromRgb(192, 109, 40));
    private bool _isEoPrimary = true;
    private double _eoDisplayRotationAngle;
    private double _irDisplayRotationAngle;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<ReceivedVideoFrame>? EoFrameReady;
    public event Action<ReceivedVideoFrame>? IrFrameReady;
    public event Action<DetectionPacket>? EoDetectionsReceived;
    public event Action<DetectionPacket>? IrDetectionsReceived;
    public event Action<YoloStatusPacket>? StatusReceived;

    public CameraWorkspaceViewModel(
        [FromKeyedServices("eo")] UdpEncodedVideoReceiverService eoReceiver,
        [FromKeyedServices("ir")] UdpEncodedVideoReceiverService irReceiver)
    {
        _eoReceiver = eoReceiver;
        _irReceiver = irReceiver;
        _eoReceiver.FrameReady += Receiver_OnEoFrameReady;
        _irReceiver.FrameReady += Receiver_OnIrFrameReady;
        _eoReceiver.DetectionsReceived += OnEoDetectionsReceived;
        _irReceiver.DetectionsReceived += OnIrDetectionsReceived;
        _eoReceiver.StatusReceived += OnStatusReceived;
        _irReceiver.StatusReceived += OnStatusReceived;
    }

    public (bool EoStarted, bool IrStarted) StartStreams(double brightness, double contrast)
    {
        _eoReceiver.SetBrightness(brightness);
        _irReceiver.SetBrightness(brightness);
        _eoReceiver.SetContrast(contrast);
        _irReceiver.SetContrast(contrast);
        return (_eoReceiver.Start(EoUdpPort), _irReceiver.Start(IrUdpPort));
    }

    public void StopStreams()
    {
        _eoReceiver.FrameReady -= Receiver_OnEoFrameReady;
        _irReceiver.FrameReady -= Receiver_OnIrFrameReady;
        _eoReceiver.DetectionsReceived -= OnEoDetectionsReceived;
        _irReceiver.DetectionsReceived -= OnIrDetectionsReceived;
        _eoReceiver.StatusReceived -= OnStatusReceived;
        _irReceiver.StatusReceived -= OnStatusReceived;
        _eoReceiver.Stop();
        _irReceiver.Stop();
    }

    public void SetBrightness(double value)
    {
        _eoReceiver.SetBrightness(value);
        _irReceiver.SetBrightness(value);
    }

    public void SetContrast(double value)
    {
        _eoReceiver.SetContrast(value);
        _irReceiver.SetContrast(value);
    }

    public void UpdateViewportTransform(double zoomLevel, double panX, double panY, double width, double height)
    {
        _eoReceiver.UpdateViewportTransform(zoomLevel, panX, panY, width, height);
        _irReceiver.UpdateViewportTransform(zoomLevel, panX, panY, width, height);
    }

    private void Receiver_OnEoFrameReady(ReceivedVideoFrame frame) => EoFrameReady?.Invoke(frame);
    private void Receiver_OnIrFrameReady(ReceivedVideoFrame frame) => IrFrameReady?.Invoke(frame);
    private void OnEoDetectionsReceived(DetectionPacket packet) => EoDetectionsReceived?.Invoke(packet);
    private void OnIrDetectionsReceived(DetectionPacket packet) => IrDetectionsReceived?.Invoke(packet);
    private void OnStatusReceived(YoloStatusPacket packet) => StatusReceived?.Invoke(packet);

    public bool IsEoPrimary
    {
        get => _isEoPrimary;
        set
        {
            if (_isEoPrimary == value)
            {
                return;
            }

            _isEoPrimary = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LargeFeedImage));
            OnPropertyChanged(nameof(InsetFeedImage));
            OnPropertyChanged(nameof(LargeFeedRotationAngle));
            OnPropertyChanged(nameof(InsetFeedRotationAngle));
            OnPropertyChanged(nameof(LargeFeedTitle));
            OnPropertyChanged(nameof(InsetFeedTitle));
            OnPropertyChanged(nameof(LargeFeedSubtitle));
            OnPropertyChanged(nameof(InsetFeedSubtitle));
        }
    }

    public ImageSource LargeFeedImage => IsEoPrimary
        ? _eoFrame ?? _eoPlaceholderFrame
        : _irFrame ?? _irPlaceholderFrame;

    public ImageSource InsetFeedImage => IsEoPrimary
        ? _irFrame ?? _irPlaceholderFrame
        : _eoFrame ?? _eoPlaceholderFrame;

    public string EoTitle => "EO cam";

    public string IrTitle => "IR cam";

    public string EoSubtitle => "Jetson YOLO EO stream";

    public string IrSubtitle => "ZYBO10 -> Jetson YOLO IR stream";

    public string LargeFeedTitle => IsEoPrimary ? EoTitle : IrTitle;

    public string InsetFeedTitle => IsEoPrimary ? IrTitle : EoTitle;

    public string LargeFeedSubtitle => IsEoPrimary ? EoSubtitle : IrSubtitle;

    public string InsetFeedSubtitle => IsEoPrimary ? IrSubtitle : EoSubtitle;

    public Stretch LargeFeedStretch => Stretch.UniformToFill;

    public Stretch InsetFeedStretch => Stretch.UniformToFill;

    public double LargeFeedRotationAngle => IsEoPrimary ? _eoDisplayRotationAngle : _irDisplayRotationAngle;

    public double InsetFeedRotationAngle => IsEoPrimary ? _irDisplayRotationAngle : _eoDisplayRotationAngle;

    public void RotateLargeFeedClockwise()
    {
        if (IsEoPrimary)
        {
            _eoDisplayRotationAngle = NextRotationAngle(_eoDisplayRotationAngle);
        }
        else
        {
            _irDisplayRotationAngle = NextRotationAngle(_irDisplayRotationAngle);
        }

        OnPropertyChanged(nameof(LargeFeedRotationAngle));
        OnPropertyChanged(nameof(InsetFeedRotationAngle));
    }

    public void RotateInsetFeedClockwise()
    {
        if (IsEoPrimary)
        {
            _irDisplayRotationAngle = NextRotationAngle(_irDisplayRotationAngle);
        }
        else
        {
            _eoDisplayRotationAngle = NextRotationAngle(_eoDisplayRotationAngle);
        }

        OnPropertyChanged(nameof(LargeFeedRotationAngle));
        OnPropertyChanged(nameof(InsetFeedRotationAngle));
    }

    public void OnEoFrameReady(ReceivedVideoFrame frame)
    {
        _eoFrame = frame.Bitmap;
        OnPropertyChanged(nameof(LargeFeedImage));
        OnPropertyChanged(nameof(InsetFeedImage));
    }

    public void OnIrFrameReady(ReceivedVideoFrame frame)
    {
        _irFrame = frame.Bitmap;
        OnPropertyChanged(nameof(LargeFeedImage));
        OnPropertyChanged(nameof(InsetFeedImage));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static double NextRotationAngle(double currentAngle) => (currentAngle + 90) % 360;

    private static ImageSource CreatePlaceholderFrame(Color accentColor)
    {
        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            var background = new LinearGradientBrush(
                Color.FromRgb(23, 28, 36),
                Color.FromRgb(73, 25, 24),
                new Point(0, 0),
                new Point(1, 1));

            dc.DrawRectangle(background, null, new Rect(0, 0, 320, 240));
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(180, accentColor.R, accentColor.G, accentColor.B)), null, new Point(220, 92), 46, 32);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(120, 255, 208, 90)), null, new Point(112, 152), 64, 22);

            var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(36, 255, 255, 255)), 1);
            for (var x = 0; x <= 320; x += 40)
            {
                dc.DrawLine(gridPen, new Point(x, 0), new Point(x, 240));
            }

            for (var y = 0; y <= 240; y += 40)
            {
                dc.DrawLine(gridPen, new Point(0, y), new Point(320, y));
            }
        }

        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }
}
