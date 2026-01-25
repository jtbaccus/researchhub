namespace ResearchHub.Core.Models;

public class ExtractionRow
{
    public int Id { get; set; }
    public int ReferenceId { get; set; }
    public int SchemaId { get; set; }
    public Dictionary<string, string> Values { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAt { get; set; }

    public Reference? Reference { get; set; }
    public ExtractionSchema? Schema { get; set; }
}
