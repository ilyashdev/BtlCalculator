using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Calculator.UI.Localization;
using Calculator.UI.Platform;
using Calculator.UI.Services;

namespace Calculator.UI.Views;

public partial class SettingsView : UserControl
{
    private readonly AppThemeService _themes;
    private readonly LanguageService _languages;
    private readonly List<LanguageOption> _languageOptions;

    public SettingsView(AppThemeService themes, LanguageService languages)
    {
        InitializeComponent();

        _themes = themes;
        _languages = languages;
        VersionLabel.Text = typeof(SettingsView).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0];

        RadioButton selected = themes.Choice switch
        {
            ThemeChoice.Light => LightTheme,
            ThemeChoice.Dark => DarkTheme,
            _ => SystemTheme,
        };
        selected.IsChecked = true;

        // "Use system setting" is the same text as for the theme.
        _languageOptions = [.. AppLanguages.CreateOptions(AppStrings.Get("SystemThemeRadioButton.Content"))];
        LanguagePicker.ItemsSource = _languageOptions;
        LanguagePicker.SelectedIndex = Math.Max(0, _languageOptions.FindIndex(option => option.CultureName == languages.CultureName));
    }

    private void OnThemeChecked(object? sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { IsChecked: true } button)
        {
            return;
        }

        ThemeChoice choice = button == LightTheme ? ThemeChoice.Light : button == DarkTheme ? ThemeChoice.Dark : ThemeChoice.System;
        if (choice != _themes.Choice)
        {
            _themes.SetChoice(choice);
        }
    }

    /// <summary>The app switches at once: its views are rebuilt in the new language (see App.ShowMainView).</summary>
    private void OnLanguageSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (LanguagePicker.SelectedIndex >= 0 && _languageOptions.Count > LanguagePicker.SelectedIndex)
        {
            _languages.SetLanguage(_languageOptions[LanguagePicker.SelectedIndex].CultureName);
        }
    }
}
