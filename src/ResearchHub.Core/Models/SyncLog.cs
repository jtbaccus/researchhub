namespace ResearchHub.Core.Models;

public enum SyncType
{
    Import,
    Export
}

public class SyncLog
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public SyncType Type { get; set; }
    public required string Source { get; set; } // File path or source name
    public string? Format { get; set; } // RIS, BibTeX, CSV, etc.
    public int RecordCount { get; set; }
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }

    public Project? Project { get; set; }
}
