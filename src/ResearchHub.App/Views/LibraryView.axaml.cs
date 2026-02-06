using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ResearchHub.App.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ResearchHub.App.Views;

public partial class LibraryView : UserControl
{
    public LibraryView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // Set up file drop handling if needed
    }

    public async void ImportButton_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import References",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Reference Files")
                {
                    Patterns = new[] { "*.ris", "*.bib", "*.bibtex", "*.csv" }
                },
                new FilePickerFileType("RIS Files") { Patterns = new[] { "*.ris" } },
                new FilePickerFileType("BibTeX Files") { Patterns = new[] { "*.bib", "*.bibtex" } },
                new FilePickerFileType("CSV Files") { Patterns = new[] { "*.csv" } }
            }
        });

        if (files.Count > 0 && DataContext is LibraryViewModel vm)
        {
            await vm.ImportFileCommand.ExecuteAsync(files[0]);
        }
    }

    public async void AttachPdfButton_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null || DataContext is not LibraryViewModel vm) return;

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
            await vm.AttachPdfCommand.ExecuteAsync(files[0].Path.LocalPath);
        }
    }
}

public static class AuthorsConverterHelper
{
    public static string ConvertAuthors(List<string>? authors)
    {
        if (authors == null || authors.Count == 0)
            return "No authors";
        return string.Join("; ", authors);
    }
}
