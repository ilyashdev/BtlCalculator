using Avalonia.Controls;
using Calculator.UI.ViewModels;

namespace Calculator.UI.Views;

public partial class DateCalculationView : UserControl
{
    public DateCalculationView(DateCalculationViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
