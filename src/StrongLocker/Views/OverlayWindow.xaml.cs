using System;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using StrongLocker.Interop;
using StrongLocker.Services;

namespace StrongLocker.Views;

public partial class OverlayWindow : Window
{
    private readonly OverlayOptions _options;
    private readonly DispatcherTimer _clockTimer;

    // Rolling buffer of the most recent keystrokes. We keep the last N characters
    // (N = password length) and test that window against the stored hash - the
    // lock behaves like a silent key logger, not a text field.
    private readonly StringBuilder _recent = new();
    private bool _unlocking;

    public OverlayWindow(OverlayOptions options)
    {
        InitializeComponent();
        _options = options;

        Background = Brushes.Black;
        BgLayer.Fill = options.Background;
        if (options.BlurRadius > 0)
        {
            BgLayer.Effect = new BlurEffect
            {
                Radius = options.BlurRadius,
                KernelType = KernelType.Gaussian,
                RenderingBias = RenderingBias.Performance
            };
        }
        TintLayer.Opacity = options.TintOpacity;

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        if (options.ShowClock)
        {
            ClockPanel.Visibility = Visibility.Visible;
            _clockTimer.Tick += (_, _) => UpdateClock();
            _clockTimer.Start();
            UpdateClock();
        }

        if (options.IsPreview)
        {
            PreviewBadge.Visibility = Visibility.Visible;
            Loaded += (_, _) => { Activate(); Keyboard.Focus(this); };
            MouseDown += (_, _) => Dismiss();
        }
        else if (options.IsPrimary)
        {
            EmergencyHint.Visibility = Visibility.Visible;
            Loaded += (_, _) => { Activate(); Keyboard.Focus(this); };
        }

        PreviewKeyDown += OnPreviewKeyDown;
        PreviewTextInput += OnPreviewTextInput;
        Closing += OnClosing;
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        ClockText.Text = now.ToString(_options.Clock24Hour ? "HH:mm" : "h:mm tt");
        DateText.Text = now.ToString("dddd, MMMM d");
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var b = _options.ScreenBounds;
        NativeMethods.SetWindowPos(handle, NativeMethods.HWND_TOPMOST,
            b.Left, b.Top, b.Width, b.Height, NativeMethods.SWP_SHOWWINDOW);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_options.IsPreview) { Dismiss(); e.Handled = true; return; }

        // Emergency: Esc removes our overlay and hands off to the Windows lock screen.
        if (_options.IsPrimary && e.Key == Key.Escape && _options.EmergencyLock is not null)
        {
            _unlocking = true;
            _options.EmergencyLock.Invoke();
            e.Handled = true;
        }
    }

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (_options.IsPreview) { Dismiss(); e.Handled = true; return; }
        if (!_options.IsPrimary || _options.VerifyPassword is null) return;

        int n = _options.PasswordLength;
        // When the stored length is known, keep a rolling window of the last N
        // keystrokes (typo-tolerant). When it is unknown (legacy settings),
        // fall back to matching the whole typed buffer so unlocking still works.
        int cap = n > 0 ? n : 128;

        foreach (char c in e.Text)
        {
            if (char.IsControl(c)) continue;
            _recent.Append(c);
            if (_recent.Length > cap) _recent.Remove(0, _recent.Length - cap);

            bool ready = n > 0 ? _recent.Length == n : _recent.Length >= 1;
            if (ready && _options.VerifyPassword(_recent.ToString()))
            {
                _unlocking = true;
                _options.Unlocked?.Invoke();
                break;
            }
        }
        e.Handled = true;
    }

    private void Dismiss()
    {
        _unlocking = true;
        _options.Unlocked?.Invoke();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_unlocking) { e.Cancel = true; return; }
        _clockTimer.Stop();
    }

    /// <summary>Closes the overlay as part of an authorized unlock or preview exit.</summary>
    public void ForceClose()
    {
        _unlocking = true;
        Close();
    }
}
