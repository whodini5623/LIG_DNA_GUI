namespace BroadcastControl.App.Models;

public sealed record AnalysisItem(string Time, string Message)
{
    public DateTime CreatedAt { get; init; } = DateTime.Now;
}

public sealed record SystemLogItem(string Time, string Message)
{
    public DateTime CreatedAt { get; init; } = DateTime.Now;
}
