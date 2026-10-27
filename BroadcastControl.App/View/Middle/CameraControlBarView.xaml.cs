using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using BroadcastControl.App.Models;
using BroadcastControl.App.ViewModels;

namespace BroadcastControl.App.View.Middle;

public partial class CameraControlBarView : UserControl
{
    private readonly DispatcherTimer _motorHoldTimer;
    private readonly Dictionary<string, int> _activeMotorDirections = new(StringComparer.Ordinal);
    private readonly HashSet<Key> _pressedMotorKeys = new();

    public CameraControlBarView()
    {
        InitializeComponent();
        _motorHoldTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _motorHoldTimer.Tick += MotorHoldTimer_OnTick;
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is MainViewModel oldViewModel) oldViewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
            if (e.NewValue is MainViewModel newViewModel) newViewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        };
        Loaded += (_, _) =>
        {
            if (Window.GetWindow(this) is { } window)
            {
                window.PreviewKeyDown += Window_OnPreviewKeyDown;
                window.PreviewKeyUp += Window_OnPreviewKeyUp;
            }
        };
        Unloaded += (_, _) =>
        {
            if (Window.GetWindow(this) is { } window)
            {
                window.PreviewKeyDown -= Window_OnPreviewKeyDown;
                window.PreviewKeyUp -= Window_OnPreviewKeyUp;
            }
            StopManualMotorInput();
        };
    }

    private void ViewModel_OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.CurrentMode) or nameof(MainViewModel.IsSystemPoweredOn))
            UpdateMotorAutomationState();
    }

    private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e) => HandleKeyDown(e);
    private void Window_OnPreviewKeyUp(object sender, KeyEventArgs e) => HandleKeyUp(e);

    public void UpdateMotorPadButtonVisualStates(bool left, bool right, bool up, bool down, bool center)
    {
        SetMotorPadButtonActive(MotorPadLeftButton, left);
        SetMotorPadButtonActive(MotorPadRightButton, right);
        SetMotorPadButtonActive(MotorPadUpButton, up);
        SetMotorPadButtonActive(MotorPadDownButton, down);
        SetMotorPadButtonActive(MotorPadCenterButton, center);
    }

    private static void SetMotorPadButtonActive(Button? button, bool isActive)
    {
        if (button is null) return;
        if (!isActive)
        {
            button.ClearValue(Control.BackgroundProperty);
            button.ClearValue(Control.BorderBrushProperty);
            return;
        }

        button.Background = new SolidColorBrush(Color.FromRgb(0x36, 0x55, 0x64));
        button.BorderBrush = new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99));
    }
    public void HandleMotorButtonPressed(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string direction })
        {
            StartMotorRepeat(direction);
            e.Handled = true;
        }
    }

    public void HandleMotorButtonReleased(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string direction })
        {
            StopMotorRepeat(direction);
            e.Handled = true;
        }
    }

    public void HandleMotorButtonExited(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement { Tag: string direction })
            StopMotorRepeat(direction);
    }

    public void HandleKeyDown(KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox && e.Key == Key.C) return;
        if (Keyboard.FocusedElement is not TextBox)
        {
            var delta = e.Key switch
            {
                Key.Add or Key.OemPlus => 1,
                Key.Subtract or Key.OemMinus => -1,
                _ => 0
            };
            if (delta != 0)
            {
                var viewModel = ViewModel;
                var isManualStep = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
                var parameter = $"{(isManualStep ? "Manual" : "Auto")}:{delta}";
                if (viewModel?.AdjustMotorStepCommand.CanExecute(parameter) == true)
                    viewModel.AdjustMotorStepCommand.Execute(parameter);
                e.Handled = true;
                return;
            }
        }

        if (!TryMapKeyToMotorDirection(e.Key, out var direction)) return;
        if (_pressedMotorKeys.Add(e.Key)) StartMotorRepeat(direction);
        e.Handled = true;
    }

    public void HandleKeyUp(KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox && e.Key == Key.C) return;
        if (!TryMapKeyToMotorDirection(e.Key, out var direction)) return;
        _pressedMotorKeys.Remove(e.Key);
        StopMotorRepeat(direction);
        e.Handled = true;
    }

    public void UpdateMotorAutomationState()
    {
        if (ViewModel?.IsManualMode != true) StopManualMotorInput();
    }

    public void StopManualMotorInput()
    {
        _motorHoldTimer.Stop();
        _activeMotorDirections.Clear();
        _pressedMotorKeys.Clear();
        UpdateMotorPadButtonVisualStates(false, false, false, false, false);
        if (ViewModel is { } viewModel && viewModel.IsManualMode)
            viewModel.UpdateManualButtonState(MotorButtonMask.None);
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void StartMotorRepeat(string direction)
    {
        if (ViewModel?.IsManualMode != true) return;
        _activeMotorDirections[direction] = _activeMotorDirections.GetValueOrDefault(direction) + 1;
        SendActiveMotorButtons();
        RefreshMotorPadVisuals();
        _motorHoldTimer.Start();
    }

    private void StopMotorRepeat(string direction)
    {
        if (!_activeMotorDirections.TryGetValue(direction, out var count)) return;
        if (count <= 1) _activeMotorDirections.Remove(direction);
        else _activeMotorDirections[direction] = count - 1;
        SendActiveMotorButtons();
        RefreshMotorPadVisuals();
        if (_activeMotorDirections.Count == 0) _motorHoldTimer.Stop();
    }

    private void MotorHoldTimer_OnTick(object? sender, EventArgs e)
    {
        if (_activeMotorDirections.Count == 0 || ViewModel?.IsManualMode != true)
        {
            _motorHoldTimer.Stop();
            return;
        }
        SendActiveMotorButtons();
    }

    private void SendActiveMotorButtons()
    {
        if (ViewModel?.IsManualMode == true) ViewModel.UpdateManualButtonState(GetActiveMotorButtons());
    }

    private MotorButtonMask GetActiveMotorButtons()
    {
        if (_activeMotorDirections.ContainsKey("Center")) return MotorButtonMask.Center;
        var buttons = MotorButtonMask.None;
        if (_activeMotorDirections.ContainsKey("Left")) buttons |= MotorButtonMask.Left;
        if (_activeMotorDirections.ContainsKey("Right")) buttons |= MotorButtonMask.Right;
        if (_activeMotorDirections.ContainsKey("Up")) buttons |= MotorButtonMask.Up;
        if (_activeMotorDirections.ContainsKey("Down")) buttons |= MotorButtonMask.Down;
        return buttons;
    }

    private void RefreshMotorPadVisuals() => UpdateMotorPadButtonVisualStates(
        _activeMotorDirections.ContainsKey("Left"), _activeMotorDirections.ContainsKey("Right"),
        _activeMotorDirections.ContainsKey("Up"), _activeMotorDirections.ContainsKey("Down"),
        _activeMotorDirections.ContainsKey("Center"));

    private static bool TryMapKeyToMotorDirection(Key key, out string direction)
    {
        direction = key switch
        {
            Key.Left => "Left", Key.Right => "Right", Key.Up => "Up", Key.Down => "Down", Key.C => "Center",
            _ => string.Empty
        };
        return direction.Length > 0;
    }

    private void MotorButton_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => HandleMotorButtonPressed(sender, e);
    private void MotorButton_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => HandleMotorButtonReleased(sender, e);
    private void MotorButton_OnMouseLeave(object sender, MouseEventArgs e) => HandleMotorButtonExited(sender, e);
}
