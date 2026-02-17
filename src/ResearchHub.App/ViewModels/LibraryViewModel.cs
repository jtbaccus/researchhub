using Avalonia.Data.Converters;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResearchHub.Core.Models;
using ResearchHub.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ResearchHub.App.ViewModels;

public partial class LibraryViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainViewModel;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private Reference? _selectedReference;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _importStatus = string.Empty;

    [ObservableProperty]
    private string _importProgressText = "";

    [ObservableProperty]
    private double _importProgressPercentage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExportError))]
    private string _exportErrorMessage = "";

    public bool HasExportError => !string.IsNullOrEmpty(ExportErrorMessage);

    public ObservableCollection<ReferencePdf> PdfAttachments { get; } = new();

    public bool HasPdfAttachments => PdfAttachments.Count > 0;

    public ObservableCollection<Reference> References { get; } = new();
    public ObservableCollection<Reference> FilteredReferences { get; } = new();

    public int TotalReferences => References.Count;
    public int FilteredCount => FilteredReferences.Count;
    public bool HasReferences => References.Count > 0;
    public bool HasNoSearchResults => HasReferences && FilteredReferences.Count == 0;

    public LibraryViewModel(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _ = LoadReferencesAsync();
    }

    private async Task LoadReferencesAsync()
    {
        if (App.LibraryService == null || _mainViewModel.CurrentProject == null) return;

        IsLoading = true;
        try
        {
            var references = await App.LibraryService.GetReferencesAsync(_mainViewModel.CurrentProject.Id);
            References.Clear();
            FilteredReferences.Clear();
            foreach (var reference in references)
            {
                References.Add(reference);
                FilteredReferences.Add(reference);
            }
            OnPropertyChanged(nameof(TotalReferences));
            OnPropertyChanged(nameof(FilteredCount));
            OnPropertyChanged(nameof(HasReferences));
            OnPropertyChanged(nameof(HasNoSearchResults));
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredReferences.Clear();

        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? References
            : References.Where(r =>
                r.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                (r.Abstract?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                r.Authors.Any(a => a.Contains(SearchText, StringComparison.OrdinalIgnoreCase)));

        foreach (var reference in filtered)
        {
            FilteredReferences.Add(reference);
        }
        OnPropertyChanged(nameof(FilteredCount));
        OnPropertyChanged(nameof(HasNoSearchResults));
    }

    [RelayCommand]
    private async Task ImportFile(IStorageFile? file)
    {
        if (file == null || App.LibraryService == null || _mainViewModel.CurrentProject == null) return;

        IsLoading = true;
        ImportStatus = "Importing...";
        ImportProgressText = "";
        ImportProgressPercentage = 0;
        try
        {
            var path = file.Path.LocalPath;

            var progress = new Progress<ImportProgress>(p =>
            {
                ImportProgressPercentage = p.Total > 0 ? (double)p.Current / p.Total * 100.0 : 0;
                ImportProgressText = $"Processing {p.Current}/{p.Total}: {Truncate(p.CurrentTitle, 60)}";
            });

            var result = await App.LibraryService.ImportFromFileAsync(_mainViewModel.CurrentProject.Id, path, progress);

            var parts = new List<string>
            {
                $"Imported {result.Imported} references",
                $"{result.Duplicates} duplicates"
            };
            if (result.Failed > 0) parts.Add($"{result.Failed} failed");
            if (result.SkippedNoTitle > 0) parts.Add($"{result.SkippedNoTitle} skipped (no title)");

            ImportStatus = string.Join(", ", parts);
            if (result.Warnings.Count > 0)
                ImportStatus += $" | Warnings: {string.Join("; ", result.Warnings)}";

            _mainViewModel.StatusMessage = ImportStatus;

            await LoadReferencesAsync();
        }
        catch (Exception ex)
        {
            ImportStatus = $"Import failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            ImportProgressText = "";
            ImportProgressPercentage = 0;
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }

    [RelayCommand]
    private async Task ExportReferences(string format)
    {
        if (App.LibraryService == null || _mainViewModel.CurrentProject == null) return;

        ExportErrorMessage = "";
        try
        {
            // This would need file picker integration - placeholder for now
            var fileName = $"export_{DateTime.Now:yyyyMMdd_HHmmss}.{format}";
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var filePath = System.IO.Path.Combine(desktopPath, fileName);

            await App.LibraryService.ExportToFileAsync(_mainViewModel.CurrentProject.Id, filePath, format);
            _mainViewModel.StatusMessage = $"Exported to {filePath}";
        }
        catch (Exception ex)
        {
            ExportErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DeleteReference()
    {
        if (SelectedReference == null || App.LibraryService == null) return;

        await App.LibraryService.DeleteReferenceAsync(SelectedReference.Id);
        await LoadReferencesAsync();
        SelectedReference = null;
    }

    [RelayCommand]
    private async Task RefreshLibrary()
    {
        await LoadReferencesAsync();
    }

    partial void OnSelectedReferenceChanged(Reference? value)
    {
        _ = LoadPdfsForSelectedReferenceAsync();
    }

    private async Task LoadPdfsForSelectedReferenceAsync()
    {
        PdfAttachments.Clear();
        OnPropertyChanged(nameof(HasPdfAttachments));

        if (SelectedReference == null || App.PdfAttachmentService == null) return;

        var pdfs = await App.PdfAttachmentService.GetPdfsAsync(SelectedReference.Id);
        foreach (var pdf in pdfs)
        {
            PdfAttachments.Add(pdf);
        }
        OnPropertyChanged(nameof(HasPdfAttachments));
    }

    [RelayCommand]
    private async Task AttachPdf(string filePath)
    {
        if (SelectedReference == null || App.PdfAttachmentService == null) return;

        await App.PdfAttachmentService.AddPdfAsync(SelectedReference.Id, filePath);
        await LoadPdfsForSelectedReferenceAsync();
        _mainViewModel.StatusMessage = "PDF attached.";
    }

    [RelayCommand]
    private async Task RemovePdf(ReferencePdf? pdf)
    {
        if (pdf == null || App.PdfAttachmentService == null) return;

        await App.PdfAttachmentService.RemovePdfAsync(pdf.Id);
        await LoadPdfsForSelectedReferenceAsync();
        _mainViewModel.StatusMessage = "PDF removed.";
    }

    [RelayCommand]
    private void OpenPdf(ReferencePdf? pdf)
    {
        if (pdf == null || App.PdfAttachmentService == null) return;

        var path = App.PdfAttachmentService.GetAbsolutePath(pdf);
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch { }
    }

    public static FuncValueConverter<List<string>?, string> AuthorsConverter { get; } =
        new(authors => authors == null || authors.Count == 0
            ? "No authors"
            : string.Join("; ", authors));
}
