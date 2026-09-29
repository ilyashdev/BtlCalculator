using Avalonia.Markup.Xaml;

namespace Calculator.UI.Localization;

/// <summary>XAML: Text="{loc:Localize HistoryLabel.Text}". Views are recreated when the language changes.</summary>
public sealed class LocalizeExtension : MarkupExtension
{
    public LocalizeExtension()
    {
    }

    public LocalizeExtension(string key)
    {
        Key = key;
    }

    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) => AppStrings.Get(Key);
}
