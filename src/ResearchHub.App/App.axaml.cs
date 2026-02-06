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
using System.Net.Http;

namespace ResearchHub.App;

public partial class App : Application
{
    public static AppDbContext? DbContext { get; private set; }
    public static IProjectService? ProjectService { get; private set; }
    public static ILibraryService? LibraryService { get; private set; }
    public static IScreeningService? ScreeningService { get; private set; }
    public static IExtractionService? ExtractionService { get; private set; }
    public static IDeduplicationService? DeduplicationService { get; private set; }
    public static IPdfAttachmentService? PdfAttachmentService { get; private set; }
    public static IPrismaService? PrismaService { get; private set; }
    public static ILlmScreeningService? LlmScreeningService { get; private set; }

    private static string GetAppDataDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appDir = Path.Combine(appData, "ResearchHub");
        Directory.CreateDirectory(appDir);
        return appDir;
    }

    private static string GetDatabasePath()
    {
        var appDir = GetAppDataDirectory();
        return Path.Combine(appDir, "researchhub.db");
    }

    private static string GetAttachmentRoot()
    {
        var appDir = GetAppDataDirectory();
        var attachmentRoot = Path.Combine(appDir, "attachments");
        Directory.CreateDirectory(attachmentRoot);
        return attachmentRoot;
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
        EnsurePdfSchema(DbContext);

        // Initialize repositories
        var projectRepo = new ProjectRepository(DbContext);
        var referenceRepo = new ReferenceRepository(DbContext);
        var screeningRepo = new ScreeningDecisionRepository(DbContext);
        var schemaRepo = new Repository<ResearchHub.Core.Models.ExtractionSchema>(DbContext);
        var rowRepo = new Repository<ResearchHub.Core.Models.ExtractionRow>(DbContext);
        var pdfRepo = new ReferencePdfRepository(DbContext);

        // Initialize services
        ProjectService = new ProjectService(projectRepo);
        LibraryService = new LibraryService(referenceRepo);
        ScreeningService = new ScreeningService(referenceRepo, screeningRepo);
        ExtractionService = new ExtractionService(schemaRepo, rowRepo, referenceRepo);
        DeduplicationService = new DeduplicationService(referenceRepo);
        PdfAttachmentService = new PdfAttachmentService(referenceRepo, pdfRepo, GetAttachmentRoot());
        PrismaService = new PrismaService(referenceRepo, screeningRepo);

        var llmSettings = LlmScreeningSettings.FromEnvironment();
        var llmHttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(llmSettings.TimeoutSeconds)
        };
        LlmScreeningService = new LlmScreeningService(llmHttpClient, llmSettings);
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

    private static void EnsurePdfSchema(AppDbContext dbContext)
    {
        dbContext.Database.ExecuteSqlRaw(
            "CREATE TABLE IF NOT EXISTS ReferencePdfs (" +
            "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
            "ReferenceId INTEGER NOT NULL, " +
            "StoredPath TEXT NOT NULL, " +
            "OriginalFileName TEXT NULL, " +
            "FileSizeBytes INTEGER NOT NULL, " +
            "Sha256 TEXT NULL, " +
            "AddedAt TEXT NOT NULL, " +
            "FOREIGN KEY(ReferenceId) REFERENCES References(Id) ON DELETE CASCADE" +
            ")");

        dbContext.Database.ExecuteSqlRaw(
            "CREATE INDEX IF NOT EXISTS IX_ReferencePdfs_ReferenceId ON ReferencePdfs(ReferenceId)");

        dbContext.Database.ExecuteSqlRaw(
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_ReferencePdfs_ReferenceId_StoredPath ON ReferencePdfs(ReferenceId, StoredPath)");
    }
}
