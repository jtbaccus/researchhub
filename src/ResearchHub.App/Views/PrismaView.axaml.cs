using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ResearchHub.App.ViewModels;

namespace ResearchHub.App.Views;

public partial class PrismaView : UserControl
{
    public PrismaView()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not PrismaViewModel vm) return;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            switch (e.Key)
            {
                case Key.E:
                    ExportSvgButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
            }
        }
        else
        {
            switch (e.Key)
            {
                case Key.R:
                    if (vm.RefreshCommand.CanExecute(null))
                        vm.RefreshCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
        }
    }

    public async void ExportSvgButton_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null || DataContext is not PrismaViewModel vm) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export PRISMA Diagram as SVG",
            DefaultExtension = "svg",
            SuggestedFileName = "prisma-flow-diagram",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("SVG Files") { Patterns = new[] { "*.svg" } }
            }
        });

        if (file != null)
        {
            await vm.ExportSvgCommand.ExecuteAsync(file.Path.LocalPath);
        }
    }
}
