using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ResearchHub.App.ViewModels;

namespace ResearchHub.App.Views;

public partial class PrismaView : UserControl
{
    public PrismaView()
    {
        InitializeComponent();
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
