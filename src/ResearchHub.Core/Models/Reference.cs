namespace ResearchHub.Core.Models;

public class Reference
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public required string Title { get; set; }
    public List<string> Authors { get; set; } = new();
    public string? Abstract { get; set; }
    public string? Journal { get; set; }
    public int? Year { get; set; }
    public string? Volume { get; set; }
    public string? Issue { get; set; }
    public string? Pages { get; set; }
    public string? Doi { get; set; }
    public string? Pmid { get; set; }
    public string? Url { get; set; }
    public string? PdfPath { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? Folder { get; set; }
    public Dictionary<string, string> CustomFields { get; set; } = new();
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public string? SourceFile { get; set; }

    public Project? Project { get; set; }
    public ICollection<ScreeningDecision> ScreeningDecisions { get; set; } = new List<ScreeningDecision>();
    public ICollection<ExtractionRow> ExtractionRows { get; set; } = new List<ExtractionRow>();
}
