using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResearchHub.Core.Models;
using ResearchHub.Data.Repositories;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ResearchHub.App.ViewModels;

public partial class ScreeningViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainViewModel;

    [ObservableProperty]
    private Reference? _currentReference;

    [ObservableProperty]
    private ScreeningPhase _currentPhase = ScreeningPhase.TitleAbstract;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _pendingCount;

    [ObservableProperty]
    private int _includedCount;

    [ObservableProperty]
    private int _excludedCount;

    [ObservableProperty]
    private int _maybeCount;

    [ObservableProperty]
    private string? _exclusionReason;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<string> CommonExclusionReasons { get; } = new()
    {
        "Not relevant to research question",
        "Wrong study design",
        "Wrong population",
        "Wrong intervention",
        "Wrong outcome",
        "Wrong setting",
        "Duplicate",
        "Not available in English",
        "Conference abstract only",
        "Other"
    };

    public ScreeningViewModel(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        if (App.ScreeningService == null || _mainViewModel.CurrentProject == null) return;

        IsLoading = true;
        try
        {
            await App.ScreeningService.InitializeScreeningAsync(_mainViewModel.CurrentProject.Id, CurrentPhase);
            await RefreshStatsAsync();
            await LoadNextReferenceAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RefreshStatsAsync()
    {
        if (App.ScreeningService == null || _mainViewModel.CurrentProject == null) return;

        var stats = await App.ScreeningService.GetStatsAsync(_mainViewModel.CurrentProject.Id, CurrentPhase);
        TotalCount = stats.Total;
        PendingCount = stats.Pending;
        IncludedCount = stats.Included;
        ExcludedCount = stats.Excluded;
        MaybeCount = stats.Maybe;
    }

    private async Task LoadNextReferenceAsync()
    {
        if (App.ScreeningService == null || _mainViewModel.CurrentProject == null) return;

        CurrentReference = await App.ScreeningService.GetNextForScreeningAsync(_mainViewModel.CurrentProject.Id, CurrentPhase);
        ExclusionReason = null;

        if (CurrentReference == null)
        {
            _mainViewModel.StatusMessage = "Screening complete! No more references to screen.";
        }
    }

    [RelayCommand]
    private async Task Include()
    {
        await RecordDecisionAsync(ScreeningVerdict.Include);
    }

    [RelayCommand]
    private async Task Exclude()
    {
        await RecordDecisionAsync(ScreeningVerdict.Exclude);
    }

    [RelayCommand]
    private async Task Maybe()
    {
        await RecordDecisionAsync(ScreeningVerdict.Maybe);
    }

    private async Task RecordDecisionAsync(ScreeningVerdict verdict)
    {
        if (CurrentReference == null || App.ScreeningService == null) return;

        await App.ScreeningService.RecordDecisionAsync(
            CurrentReference.Id,
            CurrentPhase,
            verdict,
            verdict == ScreeningVerdict.Exclude ? ExclusionReason : null);

        await RefreshStatsAsync();
        await LoadNextReferenceAsync();

        _mainViewModel.StatusMessage = $"Recorded: {verdict}";
    }

    [RelayCommand]
    private async Task SwitchPhase(ScreeningPhase phase)
    {
        CurrentPhase = phase;
        await InitializeAsync();
    }

    public double ProgressPercentage => TotalCount > 0
        ? (double)(TotalCount - PendingCount) / TotalCount * 100
        : 0;
}
