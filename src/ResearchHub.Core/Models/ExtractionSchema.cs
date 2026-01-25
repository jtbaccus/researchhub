namespace ResearchHub.Core.Models;

public class ExtractionColumn
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public ExtractionColumnType Type { get; set; } = ExtractionColumnType.Text;
    public bool IsRequired { get; set; }
    public List<string>? Options { get; set; } // For dropdown columns
}

public enum ExtractionColumnType
{
    Text,
    Number,
    Boolean,
    Date,
    Dropdown,
    MultiSelect
}

public class ExtractionSchema
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public List<ExtractionColumn> Columns { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Project? Project { get; set; }
    public ICollection<ExtractionRow> Rows { get; set; } = new List<ExtractionRow>();
}
