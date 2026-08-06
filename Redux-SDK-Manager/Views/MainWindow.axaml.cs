using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Redux_SDK_Manager.ViewModels;

namespace Redux_SDK_Manager.Views;

public partial class MainWindow : Window
{
    // Set once the user has confirmed closing over a running setup, so the second Close() goes through.
    private bool _forceClose;

    public MainWindow()
    {
        InitializeComponent();

        Opened += (_, _) =>
        {
            UpdateMaximizedState();
            RefreshBackdropClip();
        };
        // The content panel's rect can move on any layout pass (resize, maximize). Recomputing the
        // frosted-glass clip on every pass keeps the blur aligned with the panel regardless of cause.
        LayoutUpdated += (_, _) => RefreshBackdropClip();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty)
        {
            UpdateMaximizedState();
        }
    }

    // Warn before closing while a project is still being set up: the headless Unity run dies with the
    // app and the project would be left half-imported.
    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_forceClose || e.Cancel) return;
        if (DataContext is not MainWindowViewModel vm || !vm.Projects.AnySettingUp) return;

        e.Cancel = true;
        var closeAnyway = await vm.Dialog.ConfirmAsync("Setup in progress",
            "Project setup is still running. If you close now you'll have to finish setting the project up by hand in Unity. Close anyway?",
            "Close anyway", "Wait");
        if (closeAnyway)
        {
            _forceClose = true;
            Close();
        }
    }

    // The red 2px frame stays in every state (brand signature). Only the corner rounding drops at
    // maximize, since the OS squares the window anyway and a rounded clip would notch the edges.
    private void UpdateMaximizedState()
    {
        var maximized = WindowState == WindowState.Maximized;
        OuterFrame?.Classes.Set("maximized", maximized);
        InnerChrome?.Classes.Set("maximized", maximized);
        TitleBar?.Classes.Set("maximized", maximized);
        if (ResizeGrips is not null) ResizeGrips.IsVisible = !maximized;
        if (MaximizeGlyph is not null) MaximizeGlyph.Text = maximized ? "❐" : "◻";
    }

    private void RefreshBackdropClip()
    {
        if (ContentBackdropBlur is null || ContentPanel is null) return;
        if (ContentPanel.Bounds.Width <= 0 || ContentPanel.Bounds.Height <= 0) return;

        var transform = ContentPanel.TransformToVisual(ContentBackdropBlur);
        if (transform is null) return;

        var topLeft = transform.Value.Transform(new Point(0, 0));
        var radius = RadiusLarge;
        ContentBackdropBlur.Clip = new RectangleGeometry(new Rect(topLeft, ContentPanel.Bounds.Size), radius, radius);
    }

    private double RadiusLarge =>
        this.TryFindResource("RadiusLarge", out var value) && value is CornerRadius corner ? corner.TopLeft : 12;

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        if (e.ClickCount == 2)
        {
            ToggleMaximized();
            return;
        }

        BeginMoveDrag(e);
    }

    private void ResizeGrip_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (sender is Control { Tag: string tag } && Enum.TryParse<WindowEdge>(tag, out var edge))
        {
            BeginResizeDrag(edge, e);
        }
    }

    private void Minimize_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object? sender, RoutedEventArgs e) => ToggleMaximized();

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    private void ToggleMaximized() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
}
