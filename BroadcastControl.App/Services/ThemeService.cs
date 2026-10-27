using System.Windows;

namespace BroadcastControl.App.Services;

/// <summary>
/// Applies the selected application-wide theme.
/// </summary>
public sealed class ThemeService
{
    public void Apply(AppThemeMode themeMode)
    {
        if (Application.Current is App app)
        {
            app.ApplyTheme(themeMode);
        }
    }
}
