using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResearchHub.Core.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ResearchHub.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase? _currentView;

    [ObservableProperty]
    private Project? _currentProject;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _hasProject;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDashboardActive))]
    [NotifyPropertyChangedFor(nameof(IsLibraryActive))]
    [NotifyPropertyChangedFor(nameof(IsScreeningActive))]
    [NotifyPropertyChangedFor(nameof(IsExtractionActive))]
    [NotifyPropertyChangedFor(nameof(IsPrismaActive))]
    private string _activeNavSection = "Dashboard";

    public bool IsDashboardActive => ActiveNavSection == "Dashboard";
    public bool IsLibraryActive => ActiveNavSection == "Library";
    public bool IsScreeningActive => ActiveNavSection == "Screening";
    public bool IsExtractionActive => ActiveNavSection == "Extraction";
    public bool IsPrismaActive => ActiveNavSection == "PRISMA";

    public ObservableCollection<Project> RecentProjects { get; } = new();

    private DashboardViewModel? _dashboardViewModel;
    private LibraryViewModel? _libraryViewModel;
    private ScreeningViewModel? _screeningViewModel;
    private ExtractionViewModel? _extractionViewModel;
    private PrismaViewModel? _prismaViewModel;

    public MainWindowViewModel()
    {
        _dashboardViewModel = new DashboardViewModel(this);
        CurrentView = _dashboardViewModel;
        _ = LoadRecentProjectsAsync();
    }

    private async Task LoadRecentProjectsAsync()
    {
        if (App.ProjectService == null) return;

        var projects = await App.ProjectService.GetAllProjectsAsync();
        RecentProjects.Clear();
        foreach (var project in projects)
        {
            RecentProjects.Add(project);
        }
    }

    [RelayCommand]
    private async Task NewProject()
    {
        if (App.ProjectService == null) return;

        var project = await App.ProjectService.CreateProjectAsync("New Research Project");
        CurrentProject = project;
        HasProject = true;
        await LoadRecentProjectsAsync();
        NavigateToLibrary();
        StatusMessage = $"Created project: {project.Title}";
    }

    [RelayCommand]
    private async Task OpenProject(Project project)
    {
        if (App.ProjectService == null) return;

        CurrentProject = await App.ProjectService.GetProjectWithReferencesAsync(project.Id);
        HasProject = CurrentProject != null;
        if (HasProject)
        {
            NavigateToLibrary();
            StatusMessage = $"Opened project: {CurrentProject!.Title}";
        }
    }

    [RelayCommand]
    private void NavigateToDashboard()
    {
        _dashboardViewModel ??= new DashboardViewModel(this);
        CurrentView = _dashboardViewModel;
        ActiveNavSection = "Dashboard";
    }

    [RelayCommand]
    private void NavigateToLibrary()
    {
        if (!HasProject) return;
        _libraryViewModel = new LibraryViewModel(this);
        CurrentView = _libraryViewModel;
        ActiveNavSection = "Library";
    }

    [RelayCommand]
    private void NavigateToScreening()
    {
        if (!HasProject) return;
        _screeningViewModel = new ScreeningViewModel(this);
        CurrentView = _screeningViewModel;
        ActiveNavSection = "Screening";
    }

    [RelayCommand]
    public void NavigateToExtraction()
    {
        if (!HasProject) return;
        _extractionViewModel = new ExtractionViewModel(this);
        CurrentView = _extractionViewModel;
        ActiveNavSection = "Extraction";
    }

    [RelayCommand]
    private void NavigateToPrisma()
    {
        if (!HasProject) return;
        _prismaViewModel = new PrismaViewModel(this);
        CurrentView = _prismaViewModel;
        ActiveNavSection = "PRISMA";
    }
}
