using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResearchHub.Core.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ResearchHub.App.ViewModels;

public partial class ExtractionReferenceItem : ObservableObject
{
    public Reference Reference { get; }

    [ObservableProperty]
    private bool _isExtracted;

    public ExtractionReferenceItem(Reference reference, bool isExtracted)
    {
        Reference = reference;
        _isExtracted = isExtracted;
    }
}

public partial class ExtractionViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainViewModel;

    [ObservableProperty]
    private ExtractionSchema? _selectedSchema;

    [ObservableProperty]
    private ExtractionReferenceItem? _selectedReferenceItem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCurrentRow))]
    private ExtractionRowViewModel? _currentRowViewModel;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFormMode))]
    [NotifyPropertyChangedFor(nameof(IsFormWithPdf))]
    [NotifyPropertyChangedFor(nameof(IsFormWithoutPdf))]
    [NotifyPropertyChangedFor(nameof(IsSchemaEditorMode))]
    private bool _isEditingSchema;

    [ObservableProperty]
    private int _extractedCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExtractionProgressPercentage))]
    private string _extractionProgressText = "";

    // PDF panel properties
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFormWithPdf))]
    [NotifyPropertyChangedFor(nameof(IsFormWithoutPdf))]
    private bool _isPdfPanelVisible;

    [ObservableProperty]
    private string? _currentPdfPath;

    // Schema editor fields
    [ObservableProperty]
    private string _editSchemaName = "";

    [ObservableProperty]
    private string _editSchemaDescription = "";

    private ExtractionSchema? _editingExistingSchema;
    private HashSet<int> _extractedReferenceIds = new();

    public bool HasCurrentRow => CurrentRowViewModel != null;
    public bool IsFormMode => SelectedSchema != null && !IsEditingSchema;
    public bool IsSchemaEditorMode => IsEditingSchema;
    public bool IsFormWithPdf => IsFormMode && IsPdfPanelVisible;
    public bool IsFormWithoutPdf => IsFormMode && !IsPdfPanelVisible;

    public Reference? SelectedReference => SelectedReferenceItem?.Reference;

    public double ExtractionProgressPercentage =>
        ReferenceItems.Count > 0
            ? (double)ExtractedCount / ReferenceItems.Count * 100
            : 0;

    public ObservableCollection<ExtractionSchema> Schemas { get; } = new();
    public ObservableCollection<ExtractionReferenceItem> ReferenceItems { get; } = new();
    public ObservableCollection<SchemaColumnEditorViewModel> EditColumns { get; } = new();

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
            var schemas = await App.ExtractionService.GetSchemasByProjectAsync(_mainViewModel.CurrentProject.Id);
            Schemas.Clear();
            foreach (var schema in schemas)
            {
                Schemas.Add(schema);
            }

            var included = await App.ScreeningService.GetByVerdictAsync(
                _mainViewModel.CurrentProject.Id,
                ScreeningPhase.TitleAbstract,
                ScreeningVerdict.Include);

            ReferenceItems.Clear();
            foreach (var reference in included)
            {
                ReferenceItems.Add(new ExtractionReferenceItem(reference, _extractedReferenceIds.Contains(reference.Id)));
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedSchemaChanged(ExtractionSchema? value)
    {
        OnPropertyChanged(nameof(IsFormMode));
        OnPropertyChanged(nameof(IsFormWithPdf));
        OnPropertyChanged(nameof(IsFormWithoutPdf));
        if (value != null)
        {
            _ = LoadCurrentReferenceRowAsync();
            _ = LoadExtractionProgressAsync();
        }
        else
        {
            CurrentRowViewModel = null;
        }
    }

    partial void OnSelectedReferenceItemChanged(ExtractionReferenceItem? value)
    {
        OnPropertyChanged(nameof(SelectedReference));
        if (value != null && SelectedSchema != null)
        {
            _ = LoadCurrentReferenceRowAsync();
            _ = LoadPdfForSelectedReferenceAsync();
        }
        else
        {
            CurrentRowViewModel = null;
            CurrentPdfPath = null;
        }
    }

    private async Task LoadCurrentReferenceRowAsync()
    {
        var reference = SelectedReference;
        if (reference == null || SelectedSchema == null || App.ExtractionService == null)
        {
            CurrentRowViewModel = null;
            return;
        }

        var existingRow = await App.ExtractionService.GetExtractionAsync(reference.Id, SelectedSchema.Id);

        var row = existingRow ?? new ExtractionRow
        {
            ReferenceId = reference.Id,
            SchemaId = SelectedSchema.Id,
            Values = new Dictionary<string, string>()
        };

        CurrentRowViewModel = new ExtractionRowViewModel(reference, row, SelectedSchema.Columns);
    }

    private async Task LoadExtractionProgressAsync()
    {
        if (SelectedSchema == null || App.ExtractionService == null) return;

        var rows = await App.ExtractionService.GetExtractionsForSchemaAsync(SelectedSchema.Id);
        _extractedReferenceIds = new HashSet<int>(rows.Select(r => r.ReferenceId));
        ExtractedCount = _extractedReferenceIds.Count;
        var total = ReferenceItems.Count;
        ExtractionProgressText = $"{ExtractedCount} / {total} extracted";
        OnPropertyChanged(nameof(ExtractionProgressPercentage));

        // Update extracted status on each reference item
        foreach (var item in ReferenceItems)
        {
            item.IsExtracted = _extractedReferenceIds.Contains(item.Reference.Id);
        }
    }

    // --- Navigation ---

    [RelayCommand]
    private void NextReference()
    {
        if (SelectedReferenceItem == null || ReferenceItems.Count == 0) return;
        var idx = ReferenceItems.IndexOf(SelectedReferenceItem);
        if (idx < ReferenceItems.Count - 1)
            SelectedReferenceItem = ReferenceItems[idx + 1];
    }

    [RelayCommand]
    private void PreviousReference()
    {
        if (SelectedReferenceItem == null || ReferenceItems.Count == 0) return;
        var idx = ReferenceItems.IndexOf(SelectedReferenceItem);
        if (idx > 0)
            SelectedReferenceItem = ReferenceItems[idx - 1];
    }

    // --- Save ---

    [RelayCommand]
    private async Task SaveExtraction()
    {
        var reference = SelectedReference;
        if (reference == null || SelectedSchema == null || App.ExtractionService == null) return;
        if (CurrentRowViewModel == null) return;

        await App.ExtractionService.SaveExtractionAsync(
            reference.Id,
            SelectedSchema.Id,
            CurrentRowViewModel.GetValues());

        _mainViewModel.StatusMessage = "Extraction saved";
        await LoadExtractionProgressAsync();
    }

    // --- Export ---

    public async Task ExportDataToFileAsync(string filePath)
    {
        if (SelectedSchema == null || App.ExtractionService == null) return;

        var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        if (ext == ".xlsx")
        {
            await App.ExtractionService.ExportToExcelAsync(SelectedSchema.Id, filePath);
        }
        else
        {
            await App.ExtractionService.ExportToCsvAsync(SelectedSchema.Id, filePath);
        }

        _mainViewModel.StatusMessage = $"Exported to {filePath}";
    }

    // --- PDF ---

    [RelayCommand]
    private void TogglePdfPanel()
    {
        IsPdfPanelVisible = !IsPdfPanelVisible;
    }

    private async Task LoadPdfForSelectedReferenceAsync()
    {
        var reference = SelectedReference;
        if (reference == null || App.PdfAttachmentService == null)
        {
            CurrentPdfPath = null;
            return;
        }

        var pdfs = (await App.PdfAttachmentService.GetPdfsAsync(reference.Id)).ToList();
        CurrentPdfPath = pdfs.Count > 0 ? App.PdfAttachmentService.GetAbsolutePath(pdfs[0]) : null;
    }

    // --- Schema Editor ---

    [RelayCommand]
    private void OpenSchemaEditor()
    {
        _editingExistingSchema = null;
        EditSchemaName = "";
        EditSchemaDescription = "";
        EditColumns.Clear();
        IsEditingSchema = true;
    }

    [RelayCommand]
    private void EditCurrentSchema()
    {
        if (SelectedSchema == null) return;

        _editingExistingSchema = SelectedSchema;
        EditSchemaName = SelectedSchema.Name;
        EditSchemaDescription = SelectedSchema.Description ?? "";
        EditColumns.Clear();
        foreach (var col in SelectedSchema.Columns)
        {
            EditColumns.Add(new SchemaColumnEditorViewModel
            {
                Name = col.Name,
                Description = col.Description ?? "",
                Type = col.Type,
                IsRequired = col.IsRequired,
                OptionsText = col.Options != null ? string.Join(", ", col.Options) : ""
            });
        }
        IsEditingSchema = true;
    }

    [RelayCommand]
    private async Task SaveSchema()
    {
        if (App.ExtractionService == null || _mainViewModel.CurrentProject == null) return;
        if (string.IsNullOrWhiteSpace(EditSchemaName)) return;

        var columns = EditColumns.Select(ec => ec.ToColumn()).ToList();

        if (_editingExistingSchema != null)
        {
            _editingExistingSchema.Name = EditSchemaName.Trim();
            _editingExistingSchema.Description = string.IsNullOrWhiteSpace(EditSchemaDescription) ? null : EditSchemaDescription.Trim();
            _editingExistingSchema.Columns = columns;
            await App.ExtractionService.UpdateSchemaAsync(_editingExistingSchema);

            // Refresh in list
            var idx = Schemas.IndexOf(_editingExistingSchema);
            if (idx >= 0)
            {
                Schemas[idx] = _editingExistingSchema;
            }
            SelectedSchema = _editingExistingSchema;
            _mainViewModel.StatusMessage = "Schema updated";
        }
        else
        {
            var schema = await App.ExtractionService.CreateSchemaAsync(
                _mainViewModel.CurrentProject.Id,
                EditSchemaName.Trim(),
                string.IsNullOrWhiteSpace(EditSchemaDescription) ? null : EditSchemaDescription.Trim(),
                columns);

            Schemas.Add(schema);
            SelectedSchema = schema;
            _mainViewModel.StatusMessage = "Schema created";
        }

        IsEditingSchema = false;
    }

    [RelayCommand]
    private void CancelSchemaEditor()
    {
        IsEditingSchema = false;
    }

    [RelayCommand]
    private async Task DeleteSchema()
    {
        if (SelectedSchema == null || App.ExtractionService == null) return;

        var toDelete = SelectedSchema;
        SelectedSchema = null;
        Schemas.Remove(toDelete);
        await App.ExtractionService.DeleteSchemaAsync(toDelete.Id);
        _mainViewModel.StatusMessage = "Schema deleted";
    }

    [RelayCommand]
    private void AddColumnToEditor()
    {
        EditColumns.Add(new SchemaColumnEditorViewModel
        {
            Name = $"Field {EditColumns.Count + 1}",
            Type = ExtractionColumnType.Text
        });
    }

    [RelayCommand]
    private void RemoveColumnFromEditor(SchemaColumnEditorViewModel column)
    {
        EditColumns.Remove(column);
    }

    [RelayCommand]
    private void MoveColumnUp(SchemaColumnEditorViewModel column)
    {
        var idx = EditColumns.IndexOf(column);
        if (idx > 0)
            EditColumns.Move(idx, idx - 1);
    }

    [RelayCommand]
    private void MoveColumnDown(SchemaColumnEditorViewModel column)
    {
        var idx = EditColumns.IndexOf(column);
        if (idx < EditColumns.Count - 1)
            EditColumns.Move(idx, idx + 1);
    }
}

// --- Nested ViewModels ---

public partial class ExtractionRowViewModel : ObservableObject
{
    public Reference Reference { get; }
    private readonly ExtractionRow _row;

    public ObservableCollection<ColumnValueViewModel> ColumnValues { get; } = new();

    public ExtractionRowViewModel(Reference reference, ExtractionRow row, List<ExtractionColumn> columns)
    {
        Reference = reference;
        _row = row;

        foreach (var column in columns)
        {
            row.Values.TryGetValue(column.Name, out var value);
            ColumnValues.Add(new ColumnValueViewModel(column, value ?? ""));
        }
    }

    public Dictionary<string, string> GetValues()
    {
        var values = new Dictionary<string, string>();
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
    [NotifyPropertyChangedFor(nameof(BooleanValue))]
    private string _value;

    public ColumnValueViewModel(ExtractionColumn column, string value)
    {
        Column = column;
        _value = value;

        // Initialize MultiSelectOptions for MultiSelect columns
        if (column.Type == ExtractionColumnType.MultiSelect && column.Options != null)
        {
            var selectedValues = value.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var option in column.Options)
            {
                MultiSelectOptions.Add(new MultiSelectOptionViewModel(option, selectedValues.Contains(option), this));
            }
        }
    }

    // Type checks
    public bool IsText => Column.Type == ExtractionColumnType.Text;
    public bool IsNumber => Column.Type == ExtractionColumnType.Number;
    public bool IsBoolean => Column.Type == ExtractionColumnType.Boolean;
    public bool IsDate => Column.Type == ExtractionColumnType.Date;
    public bool IsDropdown => Column.Type == ExtractionColumnType.Dropdown;
    public bool IsMultiSelect => Column.Type == ExtractionColumnType.MultiSelect;

    public List<string> Options => Column.Options ?? new List<string>();

    // Boolean binding
    public bool BooleanValue
    {
        get => string.Equals(Value, "true", StringComparison.OrdinalIgnoreCase);
        set
        {
            Value = value ? "true" : "false";
            OnPropertyChanged();
        }
    }

    // Dropdown binding
    public string? SelectedOption
    {
        get => string.IsNullOrEmpty(Value) ? null : Value;
        set
        {
            Value = value ?? "";
            OnPropertyChanged();
        }
    }

    // MultiSelect
    public ObservableCollection<MultiSelectOptionViewModel> MultiSelectOptions { get; } = new();

    public void ToggleOption(string option, bool isSelected)
    {
        var selected = Value.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (isSelected)
            selected.Add(option);
        else
            selected.Remove(option);

        Value = string.Join("; ", selected.OrderBy(s => s));
    }
}

public partial class MultiSelectOptionViewModel : ObservableObject
{
    private readonly ColumnValueViewModel _parent;

    public string Label { get; }

    [ObservableProperty]
    private bool _isSelected;

    public MultiSelectOptionViewModel(string label, bool isSelected, ColumnValueViewModel parent)
    {
        Label = label;
        _isSelected = isSelected;
        _parent = parent;
    }

    partial void OnIsSelectedChanged(bool value)
    {
        _parent.ToggleOption(Label, value);
    }
}

public partial class SchemaColumnEditorViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _description = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowOptions))]
    private ExtractionColumnType _type = ExtractionColumnType.Text;

    [ObservableProperty]
    private bool _isRequired;

    [ObservableProperty]
    private string _optionsText = "";

    public bool ShowOptions => Type == ExtractionColumnType.Dropdown || Type == ExtractionColumnType.MultiSelect;

    public static List<ExtractionColumnType> AllColumnTypes { get; } = Enum.GetValues<ExtractionColumnType>().ToList();

    public ExtractionColumn ToColumn()
    {
        var col = new ExtractionColumn
        {
            Name = Name.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            Type = Type,
            IsRequired = IsRequired
        };

        if (ShowOptions && !string.IsNullOrWhiteSpace(OptionsText))
        {
            col.Options = OptionsText.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(o => o.Trim())
                .Where(o => o.Length > 0)
                .ToList();
        }

        return col;
    }
}
