using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResearchHub.Core.Models;
using System.Collections.ObjectModel;
using System;
using System.Threading.Tasks;

namespace ResearchHub.App.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainViewModel;

    [ObservableProperty]
    private string _welcomeMessage = "Welcome to ResearchHub";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = "";

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public ObservableCollection<Project> RecentProjects => _mainViewModel.RecentProjects;

    public DashboardViewModel(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    [RelayCommand]
    private async Task CreateProject()
    {
        ErrorMessage = "";
        try
        {
            await _mainViewModel.NewProjectCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task OpenProject(Project project)
    {
        ErrorMessage = "";
        try
        {
            await _mainViewModel.OpenProjectCommand.ExecuteAsync(project);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
