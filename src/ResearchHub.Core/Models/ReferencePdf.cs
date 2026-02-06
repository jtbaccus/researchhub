namespace ResearchHub.Core.Models;

public class ReferencePdf
{
    public int Id { get; set; }
    public int ReferenceId { get; set; }
    public required string StoredPath { get; set; }
    public string? OriginalFileName { get; set; }
    public long FileSizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public Reference? Reference { get; set; }
}
