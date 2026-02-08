using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ResearchHub.App.ViewModels;

namespace ResearchHub.App.Views;

public partial class ExtractionView : UserControl
{
    public ExtractionView()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
        ExportButton.Click += ExportButton_Click;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ExtractionViewModel vm) return;

        // Don't intercept shortcuts when typing in a TextBox
        if (e.Source is TextBox) return;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            switch (e.Key)
            {
                case Key.S:
                    if (vm.SaveExtractionCommand.CanExecute(null))
                        vm.SaveExtractionCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.E:
                    ExportButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.Left:
                    if (vm.PreviousReferenceCommand.CanExecute(null))
                        vm.PreviousReferenceCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.Right:
                    if (vm.NextReferenceCommand.CanExecute(null))
                        vm.NextReferenceCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
        }
        else
        {
            switch (e.Key)
            {
                case Key.P:
                    if (vm.TogglePdfPanelCommand.CanExecute(null))
                        vm.TogglePdfPanelCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
        }
    }

    private async void ExportButton_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null || DataContext is not ExtractionViewModel vm) return;
        if (vm.SelectedSchema == null) return;

        var defaultName = $"extraction_{vm.SelectedSchema.Name}_{System.DateTime.Now:yyyyMMdd}";

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Extraction Data",
            SuggestedFileName = defaultName,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Excel Workbook") { Patterns = new[] { "*.xlsx" } },
                new FilePickerFileType("CSV File") { Patterns = new[] { "*.csv" } }
            }
        });

        if (file != null)
        {
            await vm.ExportDataToFileAsync(file.Path.LocalPath);
        }
    }
}
