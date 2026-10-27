namespace BroadcastControl.App.ViewModels.Right;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using BroadcastControl.App.Models;

/// <summary>
/// View state and actions for the corresponding AnalysisPanel.
/// </summary>
public sealed class AnalysisPanelViewModel : INotifyPropertyChanged
{
    private const int VisibleItemLimit = 30;
    private static readonly Brush LowThreatBrush = CreateBrush(0x7B, 0xD8, 0x8F);
    private static readonly Brush MediumThreatBrush = CreateBrush(0xFF, 0xC1, 0x45);
    private static readonly Brush HighThreatBrush = CreateBrush(0xFF, 0x6B, 0x6B);
    private Brush _currentThreatBrush = LowThreatBrush;

    public ObservableCollection<AnalysisItem> AnalysisItems { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public Brush CurrentThreatBrush => _currentThreatBrush;

    public void SetThreatLevel(string threatLevel)
    {
        _currentThreatBrush = threatLevel switch
        {
            "\uB0AE\uC74C" => LowThreatBrush,
            "\uC911\uAC04" => MediumThreatBrush,
            _ => HighThreatBrush
        };
        OnPropertyChanged(nameof(CurrentThreatBrush));
    }

    public void Append(string time, string message)
    {
        AnalysisItems.Insert(0, new AnalysisItem(time, message));
        while (AnalysisItems.Count > VisibleItemLimit)
        {
            AnalysisItems.RemoveAt(AnalysisItems.Count - 1);
        }
    }

    public string BuildDetectionAnalysis(string feedTitle, string primaryTarget, int detectionCount) =>
        $"{feedTitle} \uc601\uc0c1\uc5d0\uc11c \uc8fc \ud0d0\uc9c0\uccb4 '{primaryTarget}' \uae30\uc900 \uc704\ud5d8 \uac1d\uccb4 {detectionCount}\uac1c\uac00 \ud655\uc778\ub418\uc5c8\uc2b5\ub2c8\ub2e4. \uc6b4\uc6a9\uc790\ub294 \ud604 \ud654\uba74\uc758 \ubc14\uc6b4\ub529 \ubc15\uc2a4 \uc704\uce58\ub97c \ud655\uc778\ud558\uace0 \ucd94\uc801/\ub179\ud654 \uc0c1\ud0dc\ub97c \uc720\uc9c0\ud558\uc2ed\uc2dc\uc624.";

    public string BuildDetectionSummary(IReadOnlyList<DetectionInfo> detections) => string.Join(
        "\n",
        detections.OrderByDescending(d => d.Score)
            .Take(8)
            .Select((d, index) => $"{index + 1}. {d.ClassName} object{d.ObjectId} / \uc2e0\ub8b0\ub3c4 {d.Score:0.00} / bbox ({d.X1:0}, {d.Y1:0})-({d.X2:0}, {d.Y2:0})"));

    private static Brush CreateBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
