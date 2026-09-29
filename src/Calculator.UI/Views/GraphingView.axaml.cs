using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Calculator.UI.Graphing;
using Calculator.UI.Localization;
using Calculator.UI.Navigation;
using Calculator.UI.Platform;
using Calculator.UI.Services;
using Calculator.UI.ViewModels;

namespace Calculator.UI.Views;

/// <summary>
/// Code-behind connects the graph surface with the view model: drawing, pan, pinch and wheel zoom, tracing with the
/// pointer or the arrow keys, saving a picture, the keypad input into the edited equation, and the narrow layout that
/// shows either the equations or the graph.
/// </summary>
public partial class GraphingView : UserControl, IModeView, IDisposable
{
    private const double TwoPaneMinWidth = 640;
    private const double WheelZoomStep = 0.15;
    private const double TraceCursorStep = 4;
    private const double TraceCursorEdge = 12;

    // Closer than this, two fingers are too close to measure the pinch reliably.
    private const double MinPinchDistance = 8;

    private readonly GraphingViewModel _viewModel;
    private readonly IShareService _share;
    private readonly GraphDrawable _drawable;
    private readonly GraphingKeypad _keypad;
    private readonly GraphPaneSwitch _paneSwitch = new();

    // The pointer that drags the graph (the first finger on touch screens), and where it was last.
    private IPointer? _panningPointer;
    private Point _lastPanPosition;

    // The fingers on the graph (at most two) and where each was last. Two fingers pinch: the graph zooms by the change
    // of the distance between them and moves with the point in the middle.
    private readonly Dictionary<IPointer, Point> _touches = [];

    private bool? _isTwoPaneState;
    private bool _isGraphShownInSinglePane;

    // The text box that has the keyboard focus, and the one the keypad writes into (the last one edited).
    private TextBox? _focusedEntry;
    private TextBox? _keypadTarget;

    public GraphingView(GraphingViewModel viewModel, IShareService share)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _share = share;
        DataContext = viewModel;

        // Curves are computed in the background and published on the UI thread.
        var curves = new CurveCache();
        curves.FrameReady += (_, _) => Canvas.InvalidateVisual();
        _drawable = new GraphDrawable(viewModel, curves);
        Canvas.Drawable = _drawable;
        Canvas.SizeChanged += (_, e) => viewModel.SetGraphSize(e.NewSize.Width, e.NewSize.Height);
        Canvas.PointerPressed += OnCanvasPointerPressed;
        Canvas.PointerMoved += OnCanvasPointerMoved;
        Canvas.PointerReleased += OnCanvasPointerReleased;
        Canvas.PointerCaptureLost += (_, e) => ReleasePointer(e.Pointer);
        Canvas.PointerExited += OnCanvasPointerExited;
        Canvas.PointerWheelChanged += OnCanvasWheel;
        viewModel.RedrawRequested += (_, _) => Canvas.InvalidateVisual();
        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        var keypadViewModel = new GraphingKeypadViewModel();
        keypadViewModel.KeyInvoked += OnKeypadKeyInvoked;
        _keypad = new GraphingKeypad(keypadViewModel);
        KeypadHost.Content = _keypad;

        ShareButton.Command = new AsyncDelegateCommand(ShareGraphPictureAsync);
        _paneSwitch.PaneSelected += (_, graph) => ShowSinglePane(graph);
        _paneSwitch.IsVisible = false;

        ApplyGraphTheme();
        UpdateTracingButton();
        if (Application.Current is Application application)
        {
            application.ActualThemeVariantChanged += OnAppThemeChanged;
        }

        SizeChanged += (_, e) => UpdateLayoutForWidth(e.NewSize.Width);
    }

    // Graphing has no history.
    public event EventHandler? ShowsHistoryButtonChanged
    {
        add { }
        remove { }
    }

    public bool ShowsHistoryButton => false;

    public bool SupportsKeepOnTop => false;

    public Control? TopBarAccessory => _paneSwitch;

    /// <summary>The application outlives this view (it is replaced when the language changes): stop listening to it.</summary>
    public void Dispose()
    {
        if (Application.Current is Application application)
        {
            application.ActualThemeVariantChanged -= OnAppThemeChanged;
        }
    }

    public void ToggleHistory()
    {
        // No history in this mode.
    }

    public void SetCompactMode(bool compact)
    {
        // Only the standard mode supports "keep on top".
    }

    /// <summary>
    /// The graph options, then a keypad flyout, then the key graph features, one per call. With one pane, the graph
    /// then goes back to the equations.
    /// </summary>
    public bool CloseOverlay()
    {
        if (SettingsPanel.IsVisible)
        {
            Motion.Hide(SettingsPanel);
            _viewModel.AreGraphOptionsOpen = false;
            return true;
        }

        if (_keypad.ClosePanels())
        {
            return true;
        }

        if (_viewModel.IsAnalysisVisible)
        {
            _viewModel.IsAnalysisVisible = false;
            return true;
        }

        if (_isTwoPaneState == false && _isGraphShownInSinglePane)
        {
            ShowSinglePane(graph: false);
            return true;
        }

        return false;
    }

    public void HandleKeyboardInput(KeyboardInput input)
    {
        if (input.Key == KeyboardKey.Escape)
        {
            CloseOverlay();
            return;
        }

        // Typing goes to the focused text box by itself; the arrow keys move the tracing cursor when none is edited.
        if (_focusedEntry is null && _viewModel.IsTracingActive)
        {
            (double dx, double dy) = input.Key switch
            {
                KeyboardKey.Left => (-TraceCursorStep, 0.0),
                KeyboardKey.Right => (TraceCursorStep, 0.0),
                KeyboardKey.Up => (0.0, -TraceCursorStep),
                KeyboardKey.Down => (0.0, TraceCursorStep),
                _ => (0.0, 0.0),
            };

            if (dx != 0 || dy != 0)
            {
                MoveTraceCursor(dx, dy);
            }
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(GraphingViewModel.GraphThemeMode):
                ApplyGraphTheme();
                break;
            case nameof(GraphingViewModel.IsAnalysisVisible):
                Motion.SetVisible(AnalysisPanel, _viewModel.IsAnalysisVisible);
                break;
            case nameof(GraphingViewModel.IsTracingActive):
                UpdateTracingButton();
                if (_viewModel.IsTracingActive)
                {
                    _drawable.TraceCursor = new Point(Canvas.Bounds.Width / 2, Canvas.Bounds.Height / 2);
                    MoveTraceCursor(0, 0);
                }

                break;
        }
    }

    private void OnAppThemeChanged(object? sender, EventArgs e)
    {
        ApplyGraphTheme();
        _viewModel.RefreshThemeColors();
    }

    /// <summary>The graph surface and the buttons on it use the graph theme, which may differ from the app theme.</summary>
    private void ApplyGraphTheme()
    {
        bool dark = _viewModel.GraphTheme == AppTheme.Dark;
        _drawable.BackgroundColor = dark ? Color.Parse("#1F1F1F") : Colors.White;
        _drawable.GridColor = dark ? Color.Parse("#4F4F4F") : Color.Parse("#C6C6C6");
        _drawable.AxisColor = dark ? Colors.White : Colors.Black;
        _drawable.TextColor = dark ? Colors.White : Colors.Black;
        _drawable.TooltipBackground = dark ? Color.Parse("#2B2B2B") : Colors.White;

        // Legacy GraphControlCommandPanel: #303030 without a border in dark, the base fill with a card stroke in light.
        // The panels follow the app theme, not the graph theme: dark panels on the light graph in dark mode, as in
        // the original.
        bool darkApp = AppThemeService.Current == AppTheme.Dark;
        foreach (Border panel in new[] { CommandPanel, ZoomPanel })
        {
            panel.Background = new SolidColorBrush(darkApp ? Color.Parse("#303030") : Color.Parse("#F3F3F3"));
            panel.BorderBrush = new SolidColorBrush(darkApp ? Color.Parse("#303030") : Color.Parse("#EBEBEB"));
        }

        foreach (Button button in new[] { TracingButton, ShareButton, SettingsButton, ZoomInButton, ZoomOutButton, ResetViewButton })
        {
            button.Foreground = darkApp ? Brushes.White : Brushes.Black;
        }

        UpdateTracingButton();
        Canvas.InvalidateVisual();
    }

    private void UpdateTracingButton()
    {
        bool active = _viewModel.IsTracingActive;
        ToolTip.SetTip(TracingButton, AppStrings.Get(active ? "disableTracingButtonToolTip" : "enableTracingButtonToolTip"));
        TracingButton.Classes.Set("active", active);
    }

    private void UpdateLayoutForWidth(double width)
    {
        bool twoPanes = width >= TwoPaneMinWidth;
        if (_isTwoPaneState == twoPanes)
        {
            return;
        }

        _isTwoPaneState = twoPanes;
        _paneSwitch.IsVisible = !twoPanes;
        if (twoPanes)
        {
            // Graph on the left (two thirds), equations on the right, as in the original.
            RootGrid.ColumnDefinitions[0].Width = new GridLength(2, GridUnitType.Star);
            RootGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            EquationsPane.IsVisible = true;
            GraphPane.IsVisible = true;
        }
        else
        {
            ShowSinglePane(_isGraphShownInSinglePane);
        }
    }

    private void ShowSinglePane(bool graph)
    {
        _isGraphShownInSinglePane = graph;
        _paneSwitch.SetGraphSelected(graph);
        RootGrid.ColumnDefinitions[0].Width = graph ? GridLength.Star : new GridLength(0);
        RootGrid.ColumnDefinitions[1].Width = graph ? new GridLength(0) : GridLength.Star;
        EquationsPane.IsVisible = !graph;
        GraphPane.IsVisible = graph;
        Motion.FadeIn(graph ? GraphPane : EquationsPane);
    }

    private void OnSettingsClicked(object? sender, RoutedEventArgs e)
    {
        bool open = !SettingsPanel.IsVisible;
        _viewModel.AreGraphOptionsOpen = open;
        Motion.SetVisible(SettingsPanel, open);
    }

    // Equation lines --------------------------------------------------------------------------------------------

    private void OnEquationPointerEntered(object? sender, PointerEventArgs e) => SetPointerOver(sender, true);

    private void OnEquationPointerExited(object? sender, PointerEventArgs e) => SetPointerOver(sender, false);

    private static void SetPointerOver(object? sender, bool isOver)
    {
        if (sender is StyledElement { DataContext: GraphEquationViewModel equation })
        {
            equation.IsPointerOver = isOver;
        }
    }

    private void OnEquationFocused(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: GraphEquationViewModel equation } entry)
        {
            _focusedEntry = entry;
            _keypadTarget = entry;
            equation.HasFocus = true;
        }
    }

    private void OnEquationUnfocused(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: GraphEquationViewModel equation } entry)
        {
            if (_focusedEntry == entry)
            {
                _focusedEntry = null;
            }

            equation.HasFocus = false;
        }
    }

    private void OnEquationKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _viewModel.EnsureTrailingEmptyEquation();
        }
    }

    /// <summary>
    /// Applies a keypad key to the equation edited last, at its caret. Without one (nothing edited yet, or that line
    /// was removed) the key goes to the end of the last line.
    /// </summary>
    private void OnKeypadKeyInvoked(object? sender, GraphKeypadKey key)
    {
        if (key.Action == GraphKeypadAction.Submit)
        {
            TopLevel.GetTopLevel(this)?.FocusManager?.Focus(null);
            _viewModel.EnsureTrailingEmptyEquation();
            return;
        }

        if (_keypadTarget is { DataContext: GraphEquationViewModel target } entry && _viewModel.Equations.Contains(target))
        {
            int start = Math.Min(entry.SelectionStart, entry.SelectionEnd);
            int length = Math.Abs(entry.SelectionEnd - entry.SelectionStart);
            (string text, int caret) = EquationTextEditor.Apply(entry.Text ?? string.Empty, start, length, key);
            entry.Text = text;
            entry.SelectionStart = caret;
            entry.SelectionEnd = caret;
            entry.CaretIndex = caret;
            return;
        }

        GraphEquationViewModel last = _viewModel.Equations[^1];
        last.Text = EquationTextEditor.Apply(last.Text, last.Text.Length, 0, key).Text;
    }

    // Graph surface ---------------------------------------------------------------------------------------------

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Point position = e.GetPosition(Canvas);
        if (e.Pointer.Type == PointerType.Touch && _touches.Count < 2)
        {
            _touches[e.Pointer] = position;
            e.Pointer.Capture(Canvas);
            if (_touches.Count == 2)
            {
                // The second finger turns the drag into a pinch.
                _panningPointer = null;
                return;
            }
        }

        if (_panningPointer is not null || !e.GetCurrentPoint(Canvas).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _panningPointer = e.Pointer;
        _lastPanPosition = position;
        e.Pointer.Capture(Canvas);
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        Point position = e.GetPosition(Canvas);
        if (_touches.Count == 2 && _touches.ContainsKey(e.Pointer))
        {
            ZoomWithTwoFingers(e.Pointer, position);
            return;
        }

        if (e.Pointer == _panningPointer)
        {
            _viewModel.Pan(position.X - _lastPanPosition.X, position.Y - _lastPanPosition.Y);
            _lastPanPosition = position;
            return;
        }

        // While the graph is dragged, the pointer moves with it: no tracing then (it would search the curves on every step).
        if (_panningPointer is not null || _touches.Count > 0)
        {
            return;
        }

        if (_viewModel.IsTracingActive)
        {
            _drawable.TraceCursor = position;
        }

        _viewModel.UpdateTrace(_drawable.FindTrace(position));
    }

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        ReleasePointer(e.Pointer);
        e.Pointer.Capture(null);
    }

    /// <summary>A finger or button is up. When one of two fingers is lifted, the other one drags again.</summary>
    private void ReleasePointer(IPointer pointer)
    {
        if (pointer == _panningPointer)
        {
            _panningPointer = null;
        }

        if (_touches.Remove(pointer) && _touches.Count == 1)
        {
            (_panningPointer, _lastPanPosition) = _touches.First();
        }
    }

    /// <summary>
    /// One of the two fingers moved: the graph zooms around the middle point by the change of the distance between the
    /// fingers, and moves with the middle point. The point under each finger stays under it.
    /// </summary>
    private void ZoomWithTwoFingers(IPointer moved, Point position)
    {
        Point before = _touches[moved];
        Point other = _touches.First(touch => touch.Key != moved).Value;
        _touches[moved] = position;

        Point middleBefore = new((before.X + other.X) / 2, (before.Y + other.Y) / 2);
        Point middle = new((position.X + other.X) / 2, (position.Y + other.Y) / 2);
        _viewModel.Pan(middle.X - middleBefore.X, middle.Y - middleBefore.Y);

        double distanceBefore = Point.Distance(before, other);
        double distance = Point.Distance(position, other);
        if (distanceBefore >= MinPinchDistance && distance >= MinPinchDistance)
        {
            _viewModel.Zoom(middle.X, middle.Y, distanceBefore / distance);
        }
    }

    private void OnCanvasPointerExited(object? sender, PointerEventArgs e)
    {
        // With active tracing the cursor stays where it is, so the keyboard can continue from there.
        if (!_viewModel.IsTracingActive && _panningPointer is null)
        {
            _viewModel.UpdateTrace(null);
        }
    }

    private void OnCanvasWheel(object? sender, PointerWheelEventArgs e)
    {
        // One notch is 1; zoom in when the wheel moves away from the user.
        double scale = 1 + (Math.Abs(e.Delta.Y) * WheelZoomStep);
        Point position = e.GetPosition(Canvas);
        _viewModel.Zoom(position.X, position.Y, e.Delta.Y > 0 ? 1 / scale : scale);
        e.Handled = true;
    }

    /// <summary>Moves the tracing cursor; at the edge of the graph the graph pans instead, like the original.</summary>
    private void MoveTraceCursor(double dx, double dy)
    {
        double width = Canvas.Bounds.Width;
        double height = Canvas.Bounds.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        Point cursor = _drawable.TraceCursor ?? new Point(width / 2, height / 2);
        double x = cursor.X + dx;
        double y = cursor.Y + dy;

        double panX = x < TraceCursorEdge ? TraceCursorEdge - x : x > width - TraceCursorEdge ? width - TraceCursorEdge - x : 0;
        double panY = y < TraceCursorEdge ? TraceCursorEdge - y : y > height - TraceCursorEdge ? height - TraceCursorEdge - y : 0;
        if (panX != 0 || panY != 0)
        {
            _viewModel.Pan(panX, panY);
        }

        var moved = new Point(x + panX, y + panY);
        _drawable.TraceCursor = moved;
        _viewModel.UpdateTrace(_drawable.FindTrace(moved));
    }

    // Picture ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// Shares a picture of the graph through the share sheet of the system; where there is none, the picture is saved
    /// where the user chooses.
    /// </summary>
    private async Task ShareGraphPictureAsync()
    {
        if (TopLevel.GetTopLevel(this) is not TopLevel owner)
        {
            return;
        }

        var size = new PixelSize(Math.Max(1, (int)Canvas.Bounds.Width), Math.Max(1, (int)Canvas.Bounds.Height));
        using var picture = new RenderTargetBitmap(size);
        picture.Render(Canvas);

        Directory.CreateDirectory(AppFolders.Cache);
        string path = Path.Combine(AppFolders.Cache, "graph.png");
        picture.Save(path, new PngBitmapEncoderOptions());
        if (await _share.ShareFileAsync(owner, path, AppStrings.Format("ShareActionTitle", Branding.ShortName)))
        {
            return;
        }

        if (owner.StorageProvider is not { CanSave: true } storage)
        {
            return;
        }

        IStorageFile? file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = "graph.png",
            DefaultExtension = "png",
            FileTypeChoices = [FilePickerFileTypes.ImagePng],
        });
        if (file is not null)
        {
            await using Stream stream = await file.OpenWriteAsync();
            picture.Save(stream, new PngBitmapEncoderOptions());
        }
    }}
