using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResearchHub.Core.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ResearchHub.App.ViewModels;

public partial class ExtractionViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainViewModel;

    [ObservableProperty]
    private ExtractionSchema? _selectedSchema;

    [ObservableProperty]
    private Reference? _selectedReference;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<ExtractionSchema> Schemas { get; } = new();
    public ObservableCollection<Reference> IncludedReferences { get; } = new();
    public ObservableCollection<ExtractionRowViewModel> ExtractionData { get; } = new();

    public ExtractionViewModel(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _ = LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        if (App.ExtractionService == null || App.ScreeningService == null || _mainViewModel.CurrentProject == null) return;

        IsLoading = true;
        try
        {
            // Load schemas
            var schemas = await App.ExtractionService.GetSchemasByProjectAsync(_mainViewModel.CurrentProject.Id);
            Schemas.Clear();
            foreach (var schema in schemas)
            {
                Schemas.Add(schema);
            }

            // Load included references (from screening)
            var included = await App.ScreeningService.GetByVerdictAsync(
                _mainViewModel.CurrentProject.Id,
                ScreeningPhase.TitleAbstract,
                ScreeningVerdict.Include);

            IncludedReferences.Clear();
            foreach (var reference in included)
            {
                IncludedReferences.Add(reference);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedSchemaChanged(ExtractionSchema? value)
    {
        if (value != null)
        {
            _ = LoadExtractionDataAsync();
        }
    }

    private async Task LoadExtractionDataAsync()
    {
        if (SelectedSchema == null || App.ExtractionService == null) return;

        ExtractionData.Clear();
        var rows = await App.ExtractionService.GetExtractionsForSchemaAsync(SelectedSchema.Id);

        foreach (var row in rows)
        {
            var reference = IncludedReferences.FirstOrDefault(r => r.Id == row.ReferenceId);
            if (reference != null)
            {
                ExtractionData.Add(new ExtractionRowViewModel(reference, row, SelectedSchema.Columns));
            }
        }
    }

    [RelayCommand]
    private async Task CreateSchema()
    {
        if (App.ExtractionService == null || _mainViewModel.CurrentProject == null) return;

        var columns = new System.Collections.Generic.List<ExtractionColumn>
        {
            new() { Name = "Study Design", Type = ExtractionColumnType.Dropdown,
                    Options = new() { "RCT", "Cohort", "Case-Control", "Cross-sectional", "Case Report", "Other" } },
            new() { Name = "Sample Size", Type = ExtractionColumnType.Number },
            new() { Name = "Population", Type = ExtractionColumnType.Text },
            new() { Name = "Intervention", Type = ExtractionColumnType.Text },
            new() { Name = "Comparator", Type = ExtractionColumnType.Text },
            new() { Name = "Primary Outcome", Type = ExtractionColumnType.Text },
            new() { Name = "Key Findings", Type = ExtractionColumnType.Text },
            new() { Name = "Quality Score", Type = ExtractionColumnType.Number },
            new() { Name = "Notes", Type = ExtractionColumnType.Text }
        };

        var schema = await App.ExtractionService.CreateSchemaAsync(
            _mainViewModel.CurrentProject.Id,
            "Data Extraction Form",
            "Standard extraction form for systematic review",
            columns);

        Schemas.Add(schema);
        SelectedSchema = schema;
        _mainViewModel.StatusMessage = "Created extraction schema";
    }

    [RelayCommand]
    private async Task SaveExtraction()
    {
        if (SelectedReference == null || SelectedSchema == null || App.ExtractionService == null) return;

        // Find the row for this reference
        var rowVm = ExtractionData.FirstOrDefault(r => r.Reference.Id == SelectedReference.Id);
        if (rowVm != null)
        {
            await App.ExtractionService.SaveExtractionAsync(
                SelectedReference.Id,
                SelectedSchema.Id,
                rowVm.GetValues());

            _mainViewModel.StatusMessage = "Extraction saved";
        }
    }

    [RelayCommand]
    private async Task ExportData()
    {
        if (SelectedSchema == null || App.ExtractionService == null) return;

        var fileName = $"extraction_{SelectedSchema.Name}_{System.DateTime.Now:yyyyMMdd}.csv";
        var desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
        var filePath = System.IO.Path.Combine(desktopPath, fileName);

        await App.ExtractionService.ExportToCsvAsync(SelectedSchema.Id, filePath);
        _mainViewModel.StatusMessage = $"Exported to {filePath}";
    }
}

public partial class ExtractionRowViewModel : ObservableObject
{
    public Reference Reference { get; }
    private readonly ExtractionRow _row;
    private readonly System.Collections.Generic.List<ExtractionColumn> _columns;

    public ObservableCollection<ColumnValueViewModel> ColumnValues { get; } = new();

    public ExtractionRowViewModel(Reference reference, ExtractionRow row, System.Collections.Generic.List<ExtractionColumn> columns)
    {
        Reference = reference;
        _row = row;
        _columns = columns;

        foreach (var column in columns)
        {
            row.Values.TryGetValue(column.Name, out var value);
            ColumnValues.Add(new ColumnValueViewModel(column, value ?? ""));
        }
    }

    public System.Collections.Generic.Dictionary<string, string> GetValues()
    {
        var values = new System.Collections.Generic.Dictionary<string, string>();
        foreach (var cv in ColumnValues)
        {
            values[cv.Column.Name] = cv.Value;
        }
        return values;
    }
}

public partial class ColumnValueViewModel : ObservableObject
{
    public ExtractionColumn Column { get; }

    [ObservableProperty]
    private string _value;

    public ColumnValueViewModel(ExtractionColumn column, string value)
    {
        Column = column;
        _value = value;
    }
}
