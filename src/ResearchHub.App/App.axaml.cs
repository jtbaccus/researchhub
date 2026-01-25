using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using ResearchHub.App.ViewModels;
using ResearchHub.App.Views;
using ResearchHub.Data;
using ResearchHub.Data.Repositories;
using ResearchHub.Services;
using System;
using System.IO;

namespace ResearchHub.App;

public partial class App : Application
{
    public static AppDbContext? DbContext { get; private set; }
    public static IProjectService? ProjectService { get; private set; }
    public static ILibraryService? LibraryService { get; private set; }
    public static IScreeningService? ScreeningService { get; private set; }
    public static IExtractionService? ExtractionService { get; private set; }

    private static string GetDatabasePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appDir = Path.Combine(appData, "ResearchHub");
        Directory.CreateDirectory(appDir);
        return Path.Combine(appDir, "researchhub.db");
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            // Initialize database and services
            InitializeServices();

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };

            desktop.Exit += (_, _) => DbContext?.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void InitializeServices()
    {
        var dbPath = GetDatabasePath();
        DbContext = new AppDbContext(dbPath);
        DbContext.Database.EnsureCreated();

        // Initialize repositories
        var projectRepo = new ProjectRepository(DbContext);
        var referenceRepo = new ReferenceRepository(DbContext);
        var screeningRepo = new ScreeningDecisionRepository(DbContext);
        var schemaRepo = new Repository<ResearchHub.Core.Models.ExtractionSchema>(DbContext);
        var rowRepo = new Repository<ResearchHub.Core.Models.ExtractionRow>(DbContext);

        // Initialize services
        ProjectService = new ProjectService(projectRepo);
        LibraryService = new LibraryService(referenceRepo);
        ScreeningService = new ScreeningService(referenceRepo, screeningRepo);
        ExtractionService = new ExtractionService(schemaRepo, rowRepo, referenceRepo);
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
