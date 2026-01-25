using Avalonia.Controls;
using Avalonia.Input;
using ResearchHub.App.ViewModels;

namespace ResearchHub.App.Views;

public partial class ScreeningView : UserControl
{
    public ScreeningView()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ScreeningViewModel vm) return;

        switch (e.Key)
        {
            case Key.I:
                if (vm.IncludeCommand.CanExecute(null))
                    vm.IncludeCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.E:
                if (vm.ExcludeCommand.CanExecute(null))
                    vm.ExcludeCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.M:
                if (vm.MaybeCommand.CanExecute(null))
                    vm.MaybeCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
