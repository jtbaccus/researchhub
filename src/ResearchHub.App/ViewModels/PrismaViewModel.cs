using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResearchHub.Core.Models;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ResearchHub.App.ViewModels;

public partial class PrismaViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainViewModel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IdentificationRecordsText))]
    [NotifyPropertyChangedFor(nameof(DuplicatesRemovedText))]
    [NotifyPropertyChangedFor(nameof(RecordsAfterDuplicatesText))]
    [NotifyPropertyChangedFor(nameof(RecordsScreenedText))]
    [NotifyPropertyChangedFor(nameof(RecordsExcludedText))]
    [NotifyPropertyChangedFor(nameof(FullTextAssessedText))]
    [NotifyPropertyChangedFor(nameof(FullTextExcludedText))]
    [NotifyPropertyChangedFor(nameof(StudiesIncludedText))]
    [NotifyPropertyChangedFor(nameof(HasCounts))]
    private PrismaFlowCounts? _counts;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = "";

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasCounts => Counts != null;

    public string IdentificationRecordsText =>
        $"Records identified through\ndatabase searching\n(n = {Counts?.Identification.RecordsIdentified ?? 0})";

    public string DuplicatesRemovedText =>
        $"Duplicates removed\n(n = {Counts?.Identification.DuplicatesRemoved ?? 0})";

    public string RecordsAfterDuplicatesText =>
        $"Records after duplicates removed\n(n = {Counts?.Identification.RecordsAfterDuplicates ?? 0})";

    public string RecordsScreenedText =>
        $"Records screened\n(n = {Counts?.Screening.RecordsScreened ?? 0})";

    public string RecordsExcludedText =>
        $"Records excluded\n(n = {Counts?.Screening.RecordsExcluded ?? 0})";

    public string FullTextAssessedText =>
        $"Full-text articles assessed\nfor eligibility\n(n = {Counts?.Eligibility.FullTextAssessed ?? 0})";

    public string FullTextExcludedText =>
        $"Full-text articles excluded\n(n = {Counts?.Eligibility.FullTextExcluded ?? 0})";

    public string StudiesIncludedText =>
        $"Studies included in\nqualitative synthesis\n(n = {Counts?.Inclusion.StudiesIncluded ?? 0})";

    public PrismaViewModel(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (App.PrismaService == null || _mainViewModel.CurrentProject == null) return;

        IsLoading = true;
        ErrorMessage = "";
        try
        {
            Counts = await App.PrismaService.GetFlowCountsAsync(_mainViewModel.CurrentProject.Id);
            _mainViewModel.StatusMessage = "PRISMA flow diagram updated.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ExportSvg(string filePath)
    {
        if (Counts == null) return;

        ErrorMessage = "";
        try
        {
            var svg = GenerateSvg(Counts);
            await File.WriteAllTextAsync(filePath, svg);
            _mainViewModel.StatusMessage = $"PRISMA diagram exported to {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    public static string GenerateSvg(PrismaFlowCounts counts)
    {
        var sb = new StringBuilder();

        // SVG header
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 780 720\" width=\"780\" height=\"720\" font-family=\"Arial, Helvetica, sans-serif\" font-size=\"12\">");

        // Arrowhead marker definition
        sb.AppendLine("  <defs>");
        sb.AppendLine("    <marker id=\"arrowhead\" markerWidth=\"10\" markerHeight=\"7\" refX=\"10\" refY=\"3.5\" orient=\"auto\">");
        sb.AppendLine("      <polygon points=\"0 0, 10 3.5, 0 7\" fill=\"#333\" />");
        sb.AppendLine("    </marker>");
        sb.AppendLine("  </defs>");

        // Layout constants
        const int phaseX = 10, phaseW = 90;
        const int boxX = 120, boxW = 280;
        const int sideX = 500, sideW = 260;
        const int boxH = 70;
        const int arrowGap = 40;

        // Row Y positions
        int row1Y = 20;                              // Identification: Records identified
        int row2Y = row1Y + boxH + arrowGap;         // Records after duplicates
        int row3Y = row2Y + boxH + arrowGap;         // Screening: Records screened
        int row4Y = row3Y + boxH + arrowGap;         // Eligibility: Full-text assessed
        int row5Y = row4Y + boxH + arrowGap;         // Included: Studies included

        // Phase labels (vertical text in colored bands)
        DrawPhaseLabel(sb, phaseX, row1Y, phaseW, row2Y + boxH - row1Y, "#4A90D9", "Identification");
        DrawPhaseLabel(sb, phaseX, row3Y, phaseW, boxH, "#5CB85C", "Screening");
        DrawPhaseLabel(sb, phaseX, row4Y, phaseW, boxH, "#F0AD4E", "Eligibility");
        DrawPhaseLabel(sb, phaseX, row5Y, phaseW, boxH, "#9B59B6", "Included");

        // Main flow boxes
        DrawBox(sb, boxX, row1Y, boxW, boxH, "#4A90D9",
            "Records identified through", "database searching",
            $"(n = {counts.Identification.RecordsIdentified})");

        DrawBox(sb, boxX, row2Y, boxW, boxH, "#4A90D9",
            "Records after duplicates removed", "",
            $"(n = {counts.Identification.RecordsAfterDuplicates})");

        DrawBox(sb, boxX, row3Y, boxW, boxH, "#5CB85C",
            "Records screened", "",
            $"(n = {counts.Screening.RecordsScreened})");

        DrawBox(sb, boxX, row4Y, boxW, boxH, "#F0AD4E",
            "Full-text articles assessed", "for eligibility",
            $"(n = {counts.Eligibility.FullTextAssessed})");

        DrawBox(sb, boxX, row5Y, boxW, boxH, "#9B59B6",
            "Studies included in", "qualitative synthesis",
            $"(n = {counts.Inclusion.StudiesIncluded})");

        // Side boxes (exclusion/removal)
        DrawBox(sb, sideX, row1Y, sideW, boxH, "#D9534F",
            "Duplicates removed", "",
            $"(n = {counts.Identification.DuplicatesRemoved})");

        DrawBox(sb, sideX, row3Y, sideW, boxH, "#D9534F",
            "Records excluded", "",
            $"(n = {counts.Screening.RecordsExcluded})");

        DrawBox(sb, sideX, row4Y, sideW, boxH, "#D9534F",
            "Full-text articles excluded", "",
            $"(n = {counts.Eligibility.FullTextExcluded})");

        // Vertical arrows (main flow)
        int mainCenterX = boxX + boxW / 2;
        DrawArrow(sb, mainCenterX, row1Y + boxH, mainCenterX, row2Y);
        DrawArrow(sb, mainCenterX, row2Y + boxH, mainCenterX, row3Y);
        DrawArrow(sb, mainCenterX, row3Y + boxH, mainCenterX, row4Y);
        DrawArrow(sb, mainCenterX, row4Y + boxH, mainCenterX, row5Y);

        // Horizontal arrows (to side boxes)
        int sideCenterY1 = row1Y + boxH / 2;
        int sideCenterY3 = row3Y + boxH / 2;
        int sideCenterY4 = row4Y + boxH / 2;
        DrawArrow(sb, boxX + boxW, sideCenterY1, sideX, sideCenterY1);
        DrawArrow(sb, boxX + boxW, sideCenterY3, sideX, sideCenterY3);
        DrawArrow(sb, boxX + boxW, sideCenterY4, sideX, sideCenterY4);

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static void DrawPhaseLabel(StringBuilder sb, int x, int y, int w, int h, string color, string label)
    {
        sb.AppendLine($"  <rect x=\"{x}\" y=\"{y}\" width=\"{w}\" height=\"{h}\" rx=\"4\" fill=\"{color}\" opacity=\"0.15\" stroke=\"{color}\" stroke-width=\"1.5\" />");
        int cx = x + w / 2;
        int cy = y + h / 2;
        sb.AppendLine($"  <text x=\"{cx}\" y=\"{cy}\" text-anchor=\"middle\" dominant-baseline=\"central\" transform=\"rotate(-90,{cx},{cy})\" font-weight=\"bold\" font-size=\"13\" fill=\"{color}\">{label}</text>");
    }

    private static void DrawBox(StringBuilder sb, int x, int y, int w, int h, string borderColor,
        string line1, string line2, string line3)
    {
        sb.AppendLine($"  <rect x=\"{x}\" y=\"{y}\" width=\"{w}\" height=\"{h}\" rx=\"6\" fill=\"white\" stroke=\"{borderColor}\" stroke-width=\"2\" />");

        int cx = x + w / 2;
        if (string.IsNullOrEmpty(line2))
        {
            sb.AppendLine($"  <text x=\"{cx}\" y=\"{y + h / 2 - 8}\" text-anchor=\"middle\" font-weight=\"bold\">{EscapeXml(line1)}</text>");
            sb.AppendLine($"  <text x=\"{cx}\" y=\"{y + h / 2 + 10}\" text-anchor=\"middle\">{EscapeXml(line3)}</text>");
        }
        else
        {
            sb.AppendLine($"  <text x=\"{cx}\" y=\"{y + h / 2 - 16}\" text-anchor=\"middle\" font-weight=\"bold\">{EscapeXml(line1)}</text>");
            sb.AppendLine($"  <text x=\"{cx}\" y=\"{y + h / 2}\" text-anchor=\"middle\" font-weight=\"bold\">{EscapeXml(line2)}</text>");
            sb.AppendLine($"  <text x=\"{cx}\" y=\"{y + h / 2 + 18}\" text-anchor=\"middle\">{EscapeXml(line3)}</text>");
        }
    }

    private static void DrawArrow(StringBuilder sb, int x1, int y1, int x2, int y2)
    {
        sb.AppendLine($"  <line x1=\"{x1}\" y1=\"{y1}\" x2=\"{x2}\" y2=\"{y2}\" stroke=\"#333\" stroke-width=\"1.5\" marker-end=\"url(#arrowhead)\" />");
    }

    private static string EscapeXml(string text)
    {
        return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
