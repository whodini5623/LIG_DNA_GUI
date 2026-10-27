namespace BroadcastControl.App.Models;

public sealed class RecordedVideoItem
{
    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string Folder { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool IsFolder { get; set; }

    public bool IsBack { get; set; }
}
