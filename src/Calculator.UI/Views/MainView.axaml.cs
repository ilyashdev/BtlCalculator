using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Calculator.UI.Localization;
using Calculator.UI.Navigation;
using Calculator.UI.Platform;
using Calculator.UI.ViewModels;

namespace Calculator.UI.Views;

/// <summary>
/// Hosts the mode views. Code-behind swaps the mode view, routes the keyboard to it and shows the top bar buttons the
/// current mode needs.
/// </summary>
public partial class MainView : UserControl
{
    private readonly MainViewModel _viewModel;
    private readonly ModeViewFactory _views;
    private readonly WindowModeService _windowMode;
    private IModeView? _currentMode;

    // For the XAML previewer.
    public MainView()
        : this(new AppServices())
    {
    }

    public MainView(AppServices services)
    {
        InitializeComponent();

        _viewModel = new MainViewModel(services.Settings);
        _views = new ModeViewFactory(services);
        _windowMode = services.WindowMode;
        DataContext = _viewModel;

        _viewModel.SelectedModeChanged += (_, _) => ShowSelectedMode();
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsNavigationOpen))
            {
                Motion.SlideFromStart(NavigationPane, NavigationScrim, _viewModel.IsNavigationOpen);
            }
        };

        // The window sees every key before the focused control (tunnel), like the original.
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        ShowSelectedMode();
    }

/// <summary>Called when the view is replaced (a new language): the shared services outlive it.</summary>
    public void Detach() => _views.ReleaseViews();

    private void ShowSelectedMode()
    {
        if (_currentMode is not null)
        {
            _currentMode.ShowsHistoryButtonChanged -= OnShowsHistoryButtonChanged;
        }

        Control view = _views.GetView(_viewModel.SelectedMode);
        ModeHost.Content = view;
        Motion.FadeIn(view);
        _currentMode = view as IModeView;

        if (_currentMode is not null)
        {
            _currentMode.ShowsHistoryButtonChanged += OnShowsHistoryButtonChanged;
        }

        TopBarAccessoryHost.Content = _currentMode?.TopBarAccessory;
        UpdateTopBarButtons();
    }

    private void UpdateTopBarButtons()
    {
        bool compact = _windowMode.IsKeepOnTop;
        NavigationButton.IsVisible = !compact;
        TitleLabel.IsVisible = !compact;
        HistoryButton.IsVisible = !compact && _currentMode?.ShowsHistoryButton == true;
        KeepOnTopButton.IsVisible = _currentMode?.SupportsKeepOnTop == true && _windowMode.IsKeepOnTopSupported;

        // Fluent "picture in picture" (keep on top) and "picture in picture exit" (back to full view).
        KeepOnTopButton.Content = compact ? "" : "";
        ToolTip.SetTip(KeepOnTopButton, AppStrings.Get(compact ? "AlwaysOnTop_Exit" : "AlwaysOnTop_Enter"));
    }

    private void OnShowsHistoryButtonChanged(object? sender, EventArgs e) => UpdateTopBarButtons();

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _viewModel.IsNavigationOpen)
        {
            _viewModel.IsNavigationOpen = false;
            e.Handled = true;
            return;
        }

        if (KeyboardTranslator.Translate(e) is KeyboardInput input && _currentMode is not null)
        {
            _currentMode.HandleKeyboardInput(input);
        }
    }

    /// <summary>
    /// The back button of phones: closes the navigation pane, then what is open in the mode, then returns to the
    /// standard mode, the way other apps return to their home screen. Only from there it leaves the app.
    /// </summary>
    public bool HandleBack()
    {
        if (_viewModel.IsNavigationOpen)
        {
            _viewModel.IsNavigationOpen = false;
            return true;
        }

        if (_currentMode?.CloseOverlay() == true)
        {
            return true;
        }

        if (_viewModel.SelectedMode != AppMode.Standard)
        {
            _viewModel.SelectedMode = AppMode.Standard;
            return true;
        }

        return false;
    }

    private void OnScrimPressed(object? sender, PointerPressedEventArgs e) => _viewModel.IsNavigationOpen = false;

    private void OnHistoryClicked(object? sender, RoutedEventArgs e) => _currentMode?.ToggleHistory();

    private void OnKeepOnTopClicked(object? sender, RoutedEventArgs e)
    {
        bool compact = !_windowMode.IsKeepOnTop;
        _windowMode.SetKeepOnTop(compact);
        _currentMode?.SetCompactMode(compact);
        UpdateTopBarButtons();
    }
}
