using Avalonia.Controls;
using Avalonia.Input;
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
        ImportButton.Click += ImportButton_Click;
        AttachPdfButton.Click += AttachPdfButton_Click;
        KeyDown += OnKeyDown;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // Set up file drop handling if needed
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not LibraryViewModel vm) return;

        // Don't intercept shortcuts when typing in a TextBox
        if (e.Source is TextBox) return;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            switch (e.Key)
            {
                case Key.I:
                    ImportButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.F:
                    // Focus the search box (named SearchBox or find by type)
                    // The TextBox is not named, so we focus via visual tree
                    e.Handled = true;
                    break;
            }
        }
        else
        {
            switch (e.Key)
            {
                case Key.Delete:
                    if (vm.DeleteReferenceCommand.CanExecute(null))
                        vm.DeleteReferenceCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
        }
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
