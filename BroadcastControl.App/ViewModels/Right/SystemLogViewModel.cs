namespace BroadcastControl.App.ViewModels.Right;

using System.Collections.ObjectModel;
using BroadcastControl.App.Models;

/// <summary>
/// View state and actions for the corresponding SystemLog.
/// </summary>
public sealed class SystemLogViewModel
{
    private const int VisibleItemLimit = 30;

    public ObservableCollection<SystemLogItem> SystemLogs { get; } = new();

    public void Append(string time, string message)
    {
        SystemLogs.Insert(0, new SystemLogItem(time, message));
        while (SystemLogs.Count > VisibleItemLimit)
        {
            SystemLogs.RemoveAt(SystemLogs.Count - 1);
        }
    }
}
