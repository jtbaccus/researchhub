using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResearchHub.Core.Models;
using ResearchHub.Data.Repositories;
using ResearchHub.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
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

    // PDF panel properties
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsScreeningWithPdf))]
    [NotifyPropertyChangedFor(nameof(IsScreeningWithoutPdf))]
    private bool _isPdfPanelVisible;

    [ObservableProperty]
    private string? _currentPdfPath;

    [ObservableProperty]
    private bool _hasPdf;

    public bool IsScreeningWithPdf => IsScreeningMode && IsPdfPanelVisible;
    public bool IsScreeningWithoutPdf => IsScreeningMode && !IsPdfPanelVisible;

    // Duplicate review mode properties
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsScreeningMode))]
    [NotifyPropertyChangedFor(nameof(IsScreeningWithPdf))]
    [NotifyPropertyChangedFor(nameof(IsScreeningWithoutPdf))]
    [NotifyPropertyChangedFor(nameof(DuplicateProgressText))]
    [NotifyPropertyChangedFor(nameof(DuplicateProgressPercentage))]
    private bool _isDuplicateReviewMode;

    [ObservableProperty]
    private bool _isDuplicateCheckRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DuplicateReasonSummary))]
    private DuplicateMatch? _currentDuplicatePair;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DuplicateProgressText))]
    [NotifyPropertyChangedFor(nameof(DuplicateProgressPercentage))]
    private int _duplicatePairTotal;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DuplicateProgressText))]
    [NotifyPropertyChangedFor(nameof(DuplicateProgressPercentage))]
    private int _duplicatesResolvedCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DuplicateProgressText))]
    [NotifyPropertyChangedFor(nameof(DuplicateProgressPercentage))]
    private int _duplicatesSkippedCount;

    private List<DuplicateMatch> _duplicatePairs = new();
    private int _currentDuplicateIndex;

    public bool IsScreeningMode => !IsDuplicateReviewMode;

    public string DuplicateProgressText
    {
        get
        {
            var reviewed = DuplicatesResolvedCount + DuplicatesSkippedCount;
            var remaining = DuplicatePairTotal - reviewed;
            return $"Reviewed {reviewed} of {DuplicatePairTotal} ({DuplicatesResolvedCount} excluded, {DuplicatesSkippedCount} skipped, {remaining} remaining)";
        }
    }

    public double DuplicateProgressPercentage => DuplicatePairTotal > 0
        ? (double)(DuplicatesResolvedCount + DuplicatesSkippedCount) / DuplicatePairTotal * 100
        : 0;

    public string? DuplicateReasonSummary
    {
        get
        {
            if (CurrentDuplicatePair == null) return null;
            var parts = new List<string>();
            foreach (var reason in CurrentDuplicatePair.Reasons)
            {
                switch (reason)
                {
                    case DuplicateReason.Doi:
                        parts.Add("Matching DOI");
                        break;
                    case DuplicateReason.Pmid:
                        parts.Add("Matching PMID");
                        break;
                    case DuplicateReason.TitleYear:
                        var pct = CurrentDuplicatePair.TitleSimilarity.HasValue
                            ? $" ({CurrentDuplicatePair.TitleSimilarity.Value:P0} similar)"
                            : "";
                        parts.Add($"Similar title/year{pct}");
                        break;
                }
            }
            return string.Join(" | ", parts);
        }
    }

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
        await LoadPdfForCurrentReferenceAsync();

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

    // PDF commands

    [RelayCommand]
    private void TogglePdfPanel()
    {
        IsPdfPanelVisible = !IsPdfPanelVisible;
    }

    [RelayCommand]
    private async Task AttachPdfToCurrentReference(string filePath)
    {
        if (CurrentReference == null || App.PdfAttachmentService == null) return;

        await App.PdfAttachmentService.AddPdfAsync(CurrentReference.Id, filePath);
        await LoadPdfForCurrentReferenceAsync();
        _mainViewModel.StatusMessage = "PDF attached.";
    }

    [RelayCommand]
    private void OpenPdfExternal()
    {
        if (string.IsNullOrEmpty(CurrentPdfPath)) return;

        try
        {
            Process.Start(new ProcessStartInfo(CurrentPdfPath) { UseShellExecute = true });
        }
        catch { }
    }

    private async Task LoadPdfForCurrentReferenceAsync()
    {
        if (CurrentReference == null || App.PdfAttachmentService == null)
        {
            CurrentPdfPath = null;
            HasPdf = false;
            return;
        }

        var pdfs = (await App.PdfAttachmentService.GetPdfsAsync(CurrentReference.Id)).ToList();
        if (pdfs.Count > 0)
        {
            CurrentPdfPath = App.PdfAttachmentService.GetAbsolutePath(pdfs[0]);
            HasPdf = true;
        }
        else
        {
            CurrentPdfPath = null;
            HasPdf = false;
        }
    }

    // Duplicate review commands

    [RelayCommand]
    private async Task CheckForDuplicates()
    {
        if (App.DeduplicationService == null || _mainViewModel.CurrentProject == null) return;

        IsDuplicateCheckRunning = true;
        try
        {
            var matches = await App.DeduplicationService.FindPotentialDuplicatesAsync(
                _mainViewModel.CurrentProject.Id);

            if (matches.Count == 0)
            {
                _mainViewModel.StatusMessage = "No duplicates found.";
                return;
            }

            _duplicatePairs = new List<DuplicateMatch>(matches);
            _currentDuplicateIndex = 0;
            DuplicatePairTotal = _duplicatePairs.Count;
            DuplicatesResolvedCount = 0;
            DuplicatesSkippedCount = 0;
            IsDuplicateReviewMode = true;
            CurrentDuplicatePair = _duplicatePairs[0];

            _mainViewModel.StatusMessage = $"Found {matches.Count} potential duplicate pair(s).";
        }
        finally
        {
            IsDuplicateCheckRunning = false;
        }
    }

    [RelayCommand]
    private async Task KeepLeft()
    {
        if (CurrentDuplicatePair == null) return;
        await ResolveDuplicatePairAsync(CurrentDuplicatePair.Duplicate);
    }

    [RelayCommand]
    private async Task KeepRight()
    {
        if (CurrentDuplicatePair == null) return;
        await ResolveDuplicatePairAsync(CurrentDuplicatePair.Primary);
    }

    [RelayCommand]
    private void SkipPair()
    {
        DuplicatesSkippedCount++;
        AdvanceToNextPair();
    }

    [RelayCommand]
    private async Task ExitDuplicateReview()
    {
        IsDuplicateReviewMode = false;
        CurrentDuplicatePair = null;
        _duplicatePairs.Clear();
        _currentDuplicateIndex = 0;
        DuplicatePairTotal = 0;
        DuplicatesResolvedCount = 0;
        DuplicatesSkippedCount = 0;
        await RefreshStatsAsync();
    }

    private async Task ResolveDuplicatePairAsync(Reference toExclude)
    {
        if (App.ScreeningService == null) return;

        await App.ScreeningService.RecordDecisionAsync(
            toExclude.Id,
            CurrentPhase,
            ScreeningVerdict.Exclude,
            "Duplicate");

        DuplicatesResolvedCount++;

        // Remove transitive pairs involving the excluded reference
        _duplicatePairs = _duplicatePairs
            .Where(p => p.Primary.Id != toExclude.Id && p.Duplicate.Id != toExclude.Id)
            .ToList();

        await RefreshStatsAsync();
        AdvanceToNextPair();
    }

    private void AdvanceToNextPair()
    {
        // After filtering, _currentDuplicateIndex may be past the end or at the next valid pair.
        // Find the next unreviewed pair starting from current position.
        if (_currentDuplicateIndex < _duplicatePairs.Count)
        {
            CurrentDuplicatePair = _duplicatePairs[_currentDuplicateIndex];
            _currentDuplicateIndex++;
        }
        else
        {
            // No more pairs
            CurrentDuplicatePair = null;
            _mainViewModel.StatusMessage = $"Duplicate review complete! {DuplicatesResolvedCount} excluded, {DuplicatesSkippedCount} skipped.";
        }
    }
}
