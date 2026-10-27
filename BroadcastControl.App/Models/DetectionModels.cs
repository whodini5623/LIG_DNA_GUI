namespace BroadcastControl.App.Models;

public readonly record struct DetectionInfo(
    string ClassName,
    float Score,
    float X1,
    float Y1,
    float X2,
    float Y2,
    int ObjectId)
{
    public string LabelText => $"{ClassName} object{ObjectId} ({Score:0.00})";
}

public readonly record struct DetectionPacket(
    ulong StampNs,
    uint FrameId,
    int Width,
    int Height,
    IReadOnlyList<DetectionInfo> Detections);

public readonly record struct YoloStatusPacket(
    bool Enabled,
    bool ModelLoaded,
    float ConfThreshold,
    string LastError,
    string Source,
    ulong StampNs,
    uint FrameId);
