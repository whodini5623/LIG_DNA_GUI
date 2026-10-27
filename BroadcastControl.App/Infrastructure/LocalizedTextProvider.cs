using System.ComponentModel;
using BroadcastControl.App.Models;

namespace BroadcastControl.App.Infrastructure;

public sealed class LocalizedTextProvider : INotifyPropertyChanged
{
    private static readonly Dictionary<string, (string English, string Korean)> Values = new()
    {
        ["PowerExit"] = ("Exit", "\uC804\uC6D0 \uC885\uB8CC"),
        ["RecordingOn"] = ("Recording", "\uC601\uC0C1 \uB179\uD654 \uC911"),
        ["RecordingStatus"] = ("Recording Status", "\uC601\uC0C1 \uB179\uD654 \uC0C1\uD0DC"),
        ["Brightness"] = ("Bright", "\uBC1D\uAE30"),
        ["Contrast"] = ("Contrast", "\uB300\uC870\uBE44"),
        ["AutoMode"] = ("Auto", "\uC790\uB3D9"),
        ["ManualMode"] = ("Manual", "\uC218\uB3D9"),
        ["StartRecording"] = ("Start Rec", "\uB179\uD654 \uC2DC\uC791"),
        ["StopRecording"] = ("Stop Rec", "\uB179\uD654 \uC885\uB8CC"),
        ["Settings"] = ("Settings", "\uC124\uC815\uCC3D"),
        ["PrimaryTargetChange"] = ("Primary Target", "\uC8FC \uD0D0\uC9C0\uCCB4 \uBCC0\uACBD"),
        ["PrimaryTarget"] = ("Target", "\uC8FC \uD0D0\uC9C0\uCCB4"),
        ["ThemeChange"] = ("Theme", "\uD14C\uB9C8 \uBCC0\uACBD"),
        ["DarkTheme"] = ("Dark", "\uC5B4\uB450\uC6B4 \uD14C\uB9C8"),
        ["LightTheme"] = ("Light", "\uBC1D\uC740 \uD14C\uB9C8"),
        ["LanguageChange"] = ("Language", "\uC5B8\uC5B4 \uBCC0\uACBD"),
        ["English"] = ("English", "\uC601\uC5B4"),
        ["Korean"] = ("Korean", "\uD55C\uAD6D\uC5B4"),
        ["ScreenMode"] = ("Screen Mode", "\uD654\uBA74 \uBAA8\uB4DC"),
        ["WindowMode"] = ("Window Mode", "\uCC3D\uBAA8\uB4DC\uB85C \uC804\uD658"),
        ["FullscreenMode"] = ("Fullscreen", "\uC804\uCCB4\uD654\uBA74\uC73C\uB85C \uC804\uD658"),
        ["Details"] = ("Details", "\uC0C1\uC138"),
        ["MotorPosition"] = ("Motor Position", "\uBAA8\uD130 \uC704\uCE58"),
        ["MotorTarget"] = ("Motor Angle Setting", "\uBAA8\uD130 \uAC01\uB3C4 \uC124\uC815"),
        ["MotorControl"] = ("Motor Control", "\uBAA8\uD130 \uCEE8\uD2B8\uB864"),
        ["SystemStatus"] = ("System Status", "\uC2DC\uC2A4\uD15C \uD604\uD669"),
        ["AnalysisPanel"] = ("Status of LLM Analysis", "\uC0C1\uD669 \uBD84\uC11D"),
        ["SystemLog"] = ("System Log", "\uC2DC\uC2A4\uD15C \uB85C\uADF8"),
        ["Save"] = ("Save", "\uC800\uC7A5"),
        ["RecordedVideos"] = ("Recorded Videos", "\uB179\uD654 \uC601\uC0C1 \uBCF4\uAE30"),
        ["Refresh"] = ("Refresh", "\uC0C8\uB85C\uACE0\uCE68"),
        ["Close"] = ("Close", "\uB2EB\uAE30"),
        ["SavedVideos"] = ("Saved Videos", "\uC800\uC7A5\uB41C \uC601\uC0C1"),
        ["RecordedData"] = ("Recording Data", "\uC800\uC7A5 \uC601\uC0C1 \uD655\uC778"),
        ["CamZoom"] = ("Cam ZOOM", "\uC804\uC790 ZOOM"),
        ["ZoomMiniMap"] = ("Zoom Map", "Zoom \uBBF8\uB2C8\uB9F5"),
        ["TrackingOn"] = ("Tracking", "\uCD94\uC801"),
        ["TrackingOff"] = ("Tracking Off", "\uBE44\uCD94\uC801"),
        ["ThreatLevel"] = ("Threat", "\uC704\uD5D8 \uB4F1\uAE09"),
        ["ThreatLow"] = ("Low", "\uB0AE\uC74C"),
        ["ThreatMedium"] = ("Medium", "\uC911\uAC04"),
        ["ThreatHigh"] = ("High", "\uB192\uC74C"),
        ["TargetComposite"] = ("Composite", "\uBCF5\uD569"),
        ["TargetPerson"] = ("Person", "\uC0AC\uB78C"),
        ["TargetWeapon"] = ("Weapon System", "\uBB34\uAE30\uCCB4\uACC4"),
        ["TargetComm"] = ("Communication Equipment", "\uD1B5\uC2E0 \uC7A5\uBE44"),
        ["TargetCivil"] = ("Non-military Target", "\uBE44\uAD70\uC0AC \uD45C\uC801"),
        ["SystemStarted"] = ("System startup started.", "\uC2DC\uC2A4\uD15C \uAC00\uB3D9\uC744 \uC2DC\uC791\uD569\uB2C8\uB2E4."),
        ["LanguageChanged"] = ("Display language changed.", "\uD45C\uC2DC \uC5B8\uC5B4\uAC00 \uBCC0\uACBD\uB418\uC5C8\uC2B5\uB2C8\uB2E4."),
    };

    private readonly Func<UiLanguage> _languageAccessor;

    public LocalizedTextProvider(Func<UiLanguage> languageAccessor)
    {
        _languageAccessor = languageAccessor;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key]
    {
        get
        {
            if (!Values.TryGetValue(key, out var value))
            {
                return key;
            }

            return _languageAccessor() == UiLanguage.Korean ? value.Korean : value.English;
        }
    }

    public void Refresh()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
}
