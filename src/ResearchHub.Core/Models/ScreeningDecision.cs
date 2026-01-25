namespace ResearchHub.Core.Models;

public enum ScreeningPhase
{
    TitleAbstract,
    FullText
}

public enum ScreeningVerdict
{
    Pending,
    Include,
    Exclude,
    Maybe
}

public class ScreeningDecision
{
    public int Id { get; set; }
    public int ReferenceId { get; set; }
    public ScreeningPhase Phase { get; set; }
    public ScreeningVerdict Verdict { get; set; } = ScreeningVerdict.Pending;
    public string? ExclusionReason { get; set; }
    public string? Notes { get; set; }
    public DateTime? DecidedAt { get; set; }

    public Reference? Reference { get; set; }
}
