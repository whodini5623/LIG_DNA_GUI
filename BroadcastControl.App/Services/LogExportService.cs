using System.Text;
using BroadcastControl.App.Models;

namespace BroadcastControl.App.Services;

/// <summary>
/// Formats log history for export and writes log files to the user's desktop.
/// </summary>
public sealed class LogExportService
{
    public string BuildAnalysisSnapshot(
        IEnumerable<AnalysisItem> history,
        DateTime startInclusive,
        DateTime endExclusive,
        bool includeAll)
    {
        var items = SelectItems(history, startInclusive, endExclusive, includeAll);
        return BuildSnapshot("LIG DNA GUI VLM Analysis Result", "No VLM analysis result in this period.", items, startInclusive, endExclusive, includeAll);
    }

    public string BuildSystemLogSnapshot(
        IEnumerable<SystemLogItem> history,
        DateTime startInclusive,
        DateTime endExclusive,
        bool includeAll)
    {
        var items = SelectItems(history, startInclusive, endExclusive, includeAll);
        return BuildSnapshot("LIG DNA GUI System Log", "No system log in this period.", items, startInclusive, endExclusive, includeAll);
    }

    public string SaveAnalysisLogs(IEnumerable<AnalysisItem> history)
    {
        var builder = new StringBuilder();
        builder.AppendLine("LIG DNA GUI Situation Analysis Log");
        builder.AppendLine($"Saved At: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine();
        foreach (var item in history)
        {
            builder.AppendLine($"[{item.Time}] {item.Message}");
        }

        return SaveToDesktop("analysis_log", builder.ToString());
    }

    public string SaveSystemLogs(IEnumerable<SystemLogItem> history)
    {
        var builder = new StringBuilder();
        builder.AppendLine("LIG DNA GUI System Log");
        builder.AppendLine($"Saved At: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine();
        foreach (var item in history)
        {
            builder.AppendLine($"[{item.Time}] {item.Message}");
        }

        return SaveToDesktop("system_log", builder.ToString());
    }

    private static AnalysisItem[] SelectItems(
        IEnumerable<AnalysisItem> history,
        DateTime startInclusive,
        DateTime endExclusive,
        bool includeAll)
    {
        return includeAll
            ? history.OrderBy(item => item.CreatedAt).ToArray()
            : history.Where(item => item.CreatedAt >= startInclusive && item.CreatedAt < endExclusive)
                .OrderBy(item => item.CreatedAt).ToArray();
    }

    private static SystemLogItem[] SelectItems(
        IEnumerable<SystemLogItem> history,
        DateTime startInclusive,
        DateTime endExclusive,
        bool includeAll)
    {
        return includeAll
            ? history.OrderBy(item => item.CreatedAt).ToArray()
            : history.Where(item => item.CreatedAt >= startInclusive && item.CreatedAt < endExclusive)
                .OrderBy(item => item.CreatedAt).ToArray();
    }

    private static string BuildSnapshot<T>(
        string title,
        string emptyMessage,
        IReadOnlyList<T> items,
        DateTime startInclusive,
        DateTime endExclusive,
        bool includeAll) where T : class
    {
        var builder = new StringBuilder();
        builder.AppendLine(title);
        builder.AppendLine($"Saved At: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        if (!includeAll)
        {
            builder.AppendLine($"Window: {startInclusive:yyyy-MM-dd HH:mm:ss} - {endExclusive:yyyy-MM-dd HH:mm:ss}");
        }

        builder.AppendLine();
        if (items.Count == 0)
        {
            builder.AppendLine(emptyMessage);
        }
        else
        {
            foreach (var item in items)
            {
                var (createdAt, message) = item switch
                {
                    AnalysisItem analysis => (analysis.CreatedAt, analysis.Message),
                    SystemLogItem log => (log.CreatedAt, log.Message),
                    _ => throw new ArgumentOutOfRangeException(nameof(items))
                };
                builder.AppendLine($"[{createdAt:yyyy-MM-dd HH:mm:ss}] {message}");
            }
        }

        return builder.ToString();
    }

    private static string SaveToDesktop(string prefix, string content)
    {
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var filePath = Path.Combine(desktopPath, $"{prefix}_{timestamp}.txt");
        File.WriteAllText(filePath, content, new UTF8Encoding(false));
        return filePath;
    }
}
