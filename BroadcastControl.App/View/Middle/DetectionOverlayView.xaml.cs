using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using BroadcastControl.App.Models;

namespace BroadcastControl.App.View.Middle;

public partial class DetectionOverlayView : UserControl
{
    private enum DisplayRotation { None, Rotate180, RotateLeft90 }
    private bool _isRenderingOverlay;
    private string? _lastOverlaySignature;

    public DetectionOverlayView() => InitializeComponent();

    public void ClearOverlay()
    {
        DetectionOverlayCanvas.Children.Clear();
        _lastOverlaySignature = null;
    }

    public void RenderDetections(
        IReadOnlyList<DetectionInfo> displayDetections,
        int sourceWidth,
        int sourceHeight,
        bool forceRefresh = false)
    {
        if (_isRenderingOverlay) return;
        if (displayDetections.Count == 0 || sourceWidth <= 0 || sourceHeight <= 0)
        {
            ClearOverlay();
            return;
        }

        _isRenderingOverlay = true;
        try
        {
            var rotation = GetCurrentDisplayRotation();
            var rotatedWidth = GetRotatedWidth(sourceWidth, sourceHeight, rotation);
            var rotatedHeight = GetRotatedHeight(sourceWidth, sourceHeight, rotation);
            var rotatedDetections = displayDetections
                .Select(d => RotateDetectionForDisplay(d, sourceWidth, sourceHeight, rotation))
                .ToArray();
            var signature = $"{rotation}:{BuildOverlaySignature(rotatedDetections)}";
            if (!forceRefresh && string.Equals(_lastOverlaySignature, signature, StringComparison.Ordinal)) return;
            _lastOverlaySignature = signature;
            DetectionOverlayCanvas.Children.Clear();

            var viewportWidth = Math.Max(DetectionOverlayCanvas.ActualWidth, 1);
            var viewportHeight = Math.Max(DetectionOverlayCanvas.ActualHeight, 1);
            var scale = Math.Max(viewportWidth / rotatedWidth, viewportHeight / rotatedHeight);
            var baseLeft = (viewportWidth - rotatedWidth * scale) / 2.0;
            var baseTop = (viewportHeight - rotatedHeight * scale) / 2.0;
            foreach (var detection in rotatedDetections)
            {
                var left = baseLeft + detection.X1 * scale;
                var top = baseTop + detection.Y1 * scale;
                var width = Math.Max(2, (detection.X2 - detection.X1) * scale);
                var height = Math.Max(2, (detection.Y2 - detection.Y1) * scale);
                if (width >= 2 && height >= 2) AddDetectionVisualToCanvas(left, top, width, height, detection);
            }
        }
        finally
        {
            _isRenderingOverlay = false;
        }
    }

    private DisplayRotation GetCurrentDisplayRotation() => DisplayRotation.None;

    private static string BuildOverlaySignature(IReadOnlyList<DetectionInfo> detections)
        => string.Join("|", detections.Select(d => $"{d.ObjectId}:{d.ClassName}:{d.X1:0}:{d.Y1:0}:{d.X2:0}:{d.Y2:0}"));

    private static int GetRotatedWidth(int width, int height, DisplayRotation rotation)
        => rotation == DisplayRotation.RotateLeft90 ? height : width;

    private static int GetRotatedHeight(int width, int height, DisplayRotation rotation)
        => rotation == DisplayRotation.RotateLeft90 ? width : height;

    private static DetectionInfo RotateDetectionForDisplay(DetectionInfo detection, int width, int height, DisplayRotation rotation)
        => rotation switch
        {
            DisplayRotation.Rotate180 => new DetectionInfo(detection.ClassName, detection.Score,
                width - detection.X2, height - detection.Y2, width - detection.X1, height - detection.Y1, detection.ObjectId),
            DisplayRotation.RotateLeft90 => RotateDetectionLeft90(detection, width),
            _ => detection
        };

    private static DetectionInfo RotateDetectionLeft90(DetectionInfo detection, int sourceWidth)
    {
        var points = new[]
        {
            RotatePointLeft90(detection.X1, detection.Y1, sourceWidth), RotatePointLeft90(detection.X2, detection.Y1, sourceWidth),
            RotatePointLeft90(detection.X2, detection.Y2, sourceWidth), RotatePointLeft90(detection.X1, detection.Y2, sourceWidth)
        };
        return new DetectionInfo(detection.ClassName, detection.Score,
            (float)points.Min(p => p.X), (float)points.Min(p => p.Y),
            (float)points.Max(p => p.X), (float)points.Max(p => p.Y), detection.ObjectId);
    }

    private static Point RotatePointLeft90(double x, double y, int sourceWidth) => new(y, sourceWidth - x);

    private void AddDetectionVisualToCanvas(
        double rectLeft,
        double rectTop,
        double rectWidth,
        double rectHeight,
        DetectionInfo detection)
    {
        var accentBrush = new SolidColorBrush(Color.FromRgb(105, 255, 132));
        accentBrush.Freeze();
        var mainRectangle = new Rectangle
        {
            Width = rectWidth,
            Height = rectHeight,
            Stroke = accentBrush,
            StrokeThickness = 2,
            RadiusX = 2,
            RadiusY = 2,
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(mainRectangle, rectLeft);
        Canvas.SetTop(mainRectangle, rectTop);
        DetectionOverlayCanvas.Children.Add(mainRectangle);

        var cornerLength = Math.Max(12, Math.Min(rectWidth, rectHeight) * 0.18);
        AddCornerToCanvas(rectLeft, rectTop, cornerLength, true, true, accentBrush);
        AddCornerToCanvas(rectLeft + rectWidth, rectTop, cornerLength, false, true, accentBrush);
        AddCornerToCanvas(rectLeft, rectTop + rectHeight, cornerLength, true, false, accentBrush);
        AddCornerToCanvas(rectLeft + rectWidth, rectTop + rectHeight, cornerLength, false, false, accentBrush);

        var labelText = new TextBlock
        {
            Text = detection.LabelText,
            Foreground = accentBrush,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold
        };

        labelText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var labelWidth = labelText.DesiredSize.Width;
        var labelHeight = labelText.DesiredSize.Height;
        var labelLeft = Math.Max(0, Math.Min(rectLeft, Math.Max(0, DetectionOverlayCanvas.ActualWidth - labelWidth - 4)));
        var preferredTop = rectTop - labelHeight - 6;
        var labelTop = preferredTop >= 0 ? preferredTop : Math.Min(DetectionOverlayCanvas.ActualHeight - labelHeight - 4, rectTop + 6);
        Canvas.SetLeft(labelText, labelLeft);
        Canvas.SetTop(labelText, Math.Max(0, labelTop));
        DetectionOverlayCanvas.Children.Add(labelText);
    }

    private void AddCornerToCanvas(
        double anchorX,
        double anchorY,
        double length,
        bool isLeft,
        bool isTop,
        Brush strokeBrush)
    {
        var horizontal = new Line
        {
            X1 = anchorX,
            Y1 = anchorY,
            X2 = anchorX + (isLeft ? length : -length),
            Y2 = anchorY,
            Stroke = strokeBrush,
            StrokeThickness = 3,
            StrokeStartLineCap = PenLineCap.Square,
            StrokeEndLineCap = PenLineCap.Square
        };

        var vertical = new Line
        {
            X1 = anchorX,
            Y1 = anchorY,
            X2 = anchorX,
            Y2 = anchorY + (isTop ? length : -length),
            Stroke = strokeBrush,
            StrokeThickness = 3,
            StrokeStartLineCap = PenLineCap.Square,
            StrokeEndLineCap = PenLineCap.Square
        };

        DetectionOverlayCanvas.Children.Add(horizontal);
        DetectionOverlayCanvas.Children.Add(vertical);
    }

}
