namespace ResearchHub.Core.Models;

public class Project
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public string? ResearchQuestion { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAt { get; set; }

    public ICollection<Reference> References { get; set; } = new List<Reference>();
    public ICollection<ExtractionSchema> ExtractionSchemas { get; set; } = new List<ExtractionSchema>();
}
