using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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

        if (vm.IsDuplicateReviewMode)
            HandleDuplicateReviewKey(vm, e);
        else
            HandleScreeningKey(vm, e);
    }

    private static void HandleScreeningKey(ScreeningViewModel vm, KeyEventArgs e)
    {
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
            case Key.P:
                if (vm.TogglePdfPanelCommand.CanExecute(null))
                    vm.TogglePdfPanelCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.A:
                if (vm.RequestLlmSuggestionCommand.CanExecute(null))
                    vm.RequestLlmSuggestionCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    public async void AttachPdfButton_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null || DataContext is not ScreeningViewModel vm) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Attach PDF",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("PDF Files") { Patterns = new[] { "*.pdf" } }
            }
        });

        if (files.Count > 0)
        {
            await vm.AttachPdfToCurrentReferenceCommand.ExecuteAsync(files[0].Path.LocalPath);
        }
    }

    private static void HandleDuplicateReviewKey(ScreeningViewModel vm, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.L:
                if (vm.KeepLeftCommand.CanExecute(null))
                    vm.KeepLeftCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.R:
                if (vm.KeepRightCommand.CanExecute(null))
                    vm.KeepRightCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.S:
                if (vm.SkipPairCommand.CanExecute(null))
                    vm.SkipPairCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
                if (vm.ExitDuplicateReviewCommand.CanExecute(null))
                    vm.ExitDuplicateReviewCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
