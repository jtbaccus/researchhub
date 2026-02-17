using Avalonia.Controls;
using Avalonia.Input;
using ResearchHub.App.ViewModels;

namespace ResearchHub.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        // Don't intercept when typing in a TextBox
        if (e.Source is TextBox) return;

        // ? key (Shift+/) or F1 to toggle keyboard shortcuts
        if ((e.Key == Key.OemQuestion) ||
            (e.Key == Key.F1))
        {
            vm.ToggleKeyboardShortcutsCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && vm.IsKeyboardShortcutsVisible)
        {
            vm.DismissKeyboardShortcutsCommand.Execute(null);
            e.Handled = true;
        }
    }
}