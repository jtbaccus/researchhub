using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResearchHub.Core.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ResearchHub.App.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainViewModel;

    [ObservableProperty]
    private string _welcomeMessage = "Welcome to ResearchHub";

    public ObservableCollection<Project> RecentProjects => _mainViewModel.RecentProjects;

    public DashboardViewModel(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    [RelayCommand]
    private async Task CreateProject()
    {
        await _mainViewModel.NewProjectCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task OpenProject(Project project)
    {
        await _mainViewModel.OpenProjectCommand.ExecuteAsync(project);
    }
}
