using Avalonia.Data.Converters;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResearchHub.Core.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    public ObservableCollection<Reference> References { get; } = new();
    public ObservableCollection<Reference> FilteredReferences { get; } = new();

    public int TotalReferences => References.Count;
    public int FilteredCount => FilteredReferences.Count;

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
    }

    [RelayCommand]
    private async Task ImportFile(IStorageFile? file)
    {
        if (file == null || App.LibraryService == null || _mainViewModel.CurrentProject == null) return;

        IsLoading = true;
        ImportStatus = "Importing...";
        try
        {
            var path = file.Path.LocalPath;
            var result = await App.LibraryService.ImportFromFileAsync(_mainViewModel.CurrentProject.Id, path);

            ImportStatus = $"Imported {result.Imported} references ({result.Duplicates} duplicates, {result.Failed} failed)";
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
        }
    }

    [RelayCommand]
    private async Task ExportReferences(string format)
    {
        if (App.LibraryService == null || _mainViewModel.CurrentProject == null) return;

        // This would need file picker integration - placeholder for now
        var fileName = $"export_{DateTime.Now:yyyyMMdd_HHmmss}.{format}";
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var filePath = System.IO.Path.Combine(desktopPath, fileName);

        await App.LibraryService.ExportToFileAsync(_mainViewModel.CurrentProject.Id, filePath, format);
        _mainViewModel.StatusMessage = $"Exported to {filePath}";
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

    public static FuncValueConverter<List<string>?, string> AuthorsConverter { get; } =
        new(authors => authors == null || authors.Count == 0
            ? "No authors"
            : string.Join("; ", authors));
}
