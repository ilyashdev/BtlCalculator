using Calculator.UI.Platform;
using Calculator.UI.Localization;
using Calculator.UI.Navigation;

namespace Calculator.UI.ViewModels;

/// <summary>An entry of the navigation pane: a group header or a mode.</summary>
public sealed class NavigationItemViewModel : ObservableObject
{
    private bool _isSelected;

    private NavigationItemViewModel(AppModeInfo? mode, string title, string glyph)
    {
        Mode = mode;
        Title = title;
        Glyph = glyph;
    }

    /// <summary>Null for group headers.</summary>
    public AppModeInfo? Mode { get; }

    public bool IsHeader => Mode is null;

    public bool IsMode => Mode is not null;

    public string Title { get; }

    public string Glyph { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public static NavigationItemViewModel Header(string title) => new(null, title, string.Empty);

    public static NavigationItemViewModel ForMode(AppModeInfo mode) => new(mode, AppStrings.Get(mode.TitleKey), mode.Glyph);
}

/// <summary>State of the main window: selected mode, title and the navigation pane.</summary>
public sealed class MainViewModel : ObservableObject
{
    private const string LastModePreference = "LastMode";

    private readonly ISettingsStore _settings;
    private AppMode _selectedMode;
    private bool _isNavigationOpen;

    public MainViewModel(ISettingsStore settings)
    {
        _settings = settings;
        Items =
        [
            NavigationItemViewModel.Header(AppStrings.Get("CalculatorModeText")),
            .. ModesOf(AppModeGroup.Calculator),
            NavigationItemViewModel.Header(AppStrings.Get("ConverterModeText")),
            .. ModesOf(AppModeGroup.Converter),
        ];
        FooterItems = [NavigationItemViewModel.ForMode(AppModeInfo.Of(AppMode.Settings))];

        NavigateCommand = new DelegateCommand<NavigationItemViewModel>(item =>
        {
            if (item.Mode is AppModeInfo info)
            {
                SelectedMode = info.Mode;
            }
        });
        ToggleNavigationCommand = new DelegateCommand(() => IsNavigationOpen = !IsNavigationOpen);
        CloseNavigationCommand = new DelegateCommand(() => IsNavigationOpen = false);

        // The app opens in the mode used last time (Settings is not restored).
        _selectedMode = Enum.TryParse(settings.GetString(LastModePreference, nameof(AppMode.Standard)), out AppMode saved) && saved != AppMode.Settings
            ? saved
            : AppMode.Standard;
        UpdateItemSelection();
    }

    /// <summary>Raised when another mode is selected; the page swaps the mode view.</summary>
    public event EventHandler? SelectedModeChanged;

    public IReadOnlyList<NavigationItemViewModel> Items { get; }

    /// <summary>Entries pinned to the bottom of the pane (Settings).</summary>
    public IReadOnlyList<NavigationItemViewModel> FooterItems { get; }

    public AppMode SelectedMode
    {
        get => _selectedMode;
        set
        {
            IsNavigationOpen = false;
            if (!SetProperty(ref _selectedMode, value))
            {
                return;
            }

            UpdateItemSelection();
            _settings.Set(LastModePreference, value.ToString());
            OnPropertyChanged(nameof(Title));
            SelectedModeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string Title => AppStrings.Get(AppModeInfo.Of(SelectedMode).TitleKey);

    public bool IsNavigationOpen
    {
        get => _isNavigationOpen;
        set => SetProperty(ref _isNavigationOpen, value);
    }

    public DelegateCommand<NavigationItemViewModel> NavigateCommand { get; }

    public DelegateCommand ToggleNavigationCommand { get; }

    public DelegateCommand CloseNavigationCommand { get; }

    private void UpdateItemSelection()
    {
        foreach (NavigationItemViewModel item in Items.Concat(FooterItems))
        {
            item.IsSelected = item.Mode?.Mode == _selectedMode;
        }
    }

    private static IEnumerable<NavigationItemViewModel> ModesOf(AppModeGroup group) =>
        AppModeInfo.All.Where(info => info.Group == group).Select(NavigationItemViewModel.ForMode);
}
