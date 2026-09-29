using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Calculator.UI.Platform;
using Calculator.UI.Services;
using Calculator.UI.Views;

namespace Calculator.UI;

public partial class App : Application
{
    // Default and minimum window sizes of the legacy app.
    private const double DefaultWidth = 322;
    private const double DefaultHeight = 540;
    private const double MinimumWidth = 322;
    private const double MinimumHeight = 460;

    private const string WidthPreference = "Window.Width";
    private const string HeightPreference = "Window.Height";

    // A restored window takes at most this part of the screen (a size saved on a bigger screen still fits).
    private const double MaximumScreenShare = 0.8;

    private AppServices? _services;
    private Window? _window;

    // Phones: the one view of the app. It stays; the main view in it is replaced when the language changes.
    private ContentControl? _phoneRoot;
    private MainView? _mainView;

    /// <summary>The share sheet of the platform; the app head sets it before the app starts.</summary>
    public IShareService Share { get; set; } = new NoShareService();

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();

        // Without a lifetime (headless tests) the views are created by whoever runs the app.
        if (ApplicationLifetime is null)
        {
            return;
        }

        _services = new AppServices { Share = Share };

        // The language comes first: every text of the app is read in it.
        _services.Languages.ApplySaved();
        _services.Themes.ApplySaved();
        _services.Languages.LanguageChanged += (_, _) => ShowMainView();

        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                _window = CreateWindow(_services);
                desktop.MainWindow = _window;
                break;
            case ISingleViewApplicationLifetime singleView:
                _phoneRoot = CreatePhoneRoot(_services);
                singleView.MainView = _phoneRoot;
                break;
        }

        ShowMainView();
    }

    /// <summary>
    /// A new main view in the current language (texts are read when views are created). The memory is shared and stays;
    /// the current input of the modes starts over. Right-to-left languages mirror the view like the original; numbers,
    /// keypads and graphs inside it stay left to right (see the views).
    /// </summary>
    private void ShowMainView()
    {
        AppServices services = _services!;
        MainView? old = _mainView;
        _mainView = new MainView(services)
        {
            FlowDirection = LanguageService.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
        };

        if (_window is not null)
        {
            _window.Content = _mainView;
            services.System.Attach(_window);
            ApplyWindowLook(_window, _mainView);
        }
        else if (_phoneRoot is not null)
        {
            _phoneRoot.Content = _mainView;
        }

        old?.Detach();
    }

    /// <summary>
    /// The back button of phones; the platform head calls it. True when the app handled it (closed something or
    /// returned to the standard mode), false when the app should be left.
    /// </summary>
    public bool HandleBack() => _mainView?.HandleBack() == true;

    /// <summary>The view of phones. Once it is on screen it connects the system services.</summary>
    private static ContentControl CreatePhoneRoot(AppServices services)
    {
        var root = new ContentControl();
        root.AttachedToVisualTree += (_, _) =>
        {
            if (TopLevel.GetTopLevel(root) is TopLevel topLevel)
            {
                services.System.Attach(topLevel);
            }
        };
        return root;
    }

    private static Window CreateWindow(AppServices services)
    {
        var window = new Window
        {
            Title = Branding.ShortName,
            MinWidth = MinimumWidth,
            MinHeight = MinimumHeight,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };

        (window.Width, window.Height) = RestoredSize(window, services);

        // Windows 11 look: the Mica material behind the content, where the system has it. The title bar stays the
        // system one: it moves the window and has the usual buttons on every platform.
        window.TransparencyLevelHint = [WindowTransparencyLevel.Mica, WindowTransparencyLevel.None];

        // The material the system gives is known once the window is open.
        window.Opened += (_, _) =>
        {
            if (window.Content is MainView view)
            {
                ApplyWindowLook(window, view);
            }
        };
        services.WindowMode.Attach(window);

        // Only a size the user chose is remembered, not a maximized or compact ("keep on top") window.
        window.Resized += (_, e) =>
        {
            if (e.Reason == WindowResizeReason.User && services.WindowMode.HasNormalSize)
            {
                services.Settings.Set(WidthPreference, e.ClientSize.Width);
                services.Settings.Set(HeightPreference, e.ClientSize.Height);
            }
        };
        return window;
    }

    /// <summary>
    /// Over Mica the view leaves its background transparent, so the translucent keys show the material, like the
    /// original; without it (other systems, Windows 10) it paints the page color.
    /// </summary>
    private static void ApplyWindowLook(Window window, MainView view)
    {
        bool mica = window.ActualTransparencyLevel == WindowTransparencyLevel.Mica;
        window.Background = mica ? Brushes.Transparent : null;
        if (mica)
        {
            view.Background = Brushes.Transparent;
        }
    }

    /// <summary>The saved size, or the original's default; never below the minimum or above most of the screen.</summary>
    private static (double Width, double Height) RestoredSize(Window window, AppServices services)
    {
        double width = Math.Max(MinimumWidth, services.Settings.GetDouble(WidthPreference, DefaultWidth));
        double height = Math.Max(MinimumHeight, services.Settings.GetDouble(HeightPreference, DefaultHeight));

        if (window.Screens.Primary is Screen screen && screen.Scaling > 0)
        {
            PixelRect area = screen.WorkingArea;
            width = Math.Min(width, Math.Max(MinimumWidth, area.Width / screen.Scaling * MaximumScreenShare));
            height = Math.Min(height, Math.Max(MinimumHeight, area.Height / screen.Scaling * MaximumScreenShare));
        }

        return (width, height);
    }
}
