using ResearchHub.Core.Models;

namespace ResearchHub.Services;

public enum DuplicateReason
{
    Doi,
    Pmid,
    TitleYear
}

public class DeduplicationOptions
{
    public double TitleSimilarityThreshold { get; set; } = 0.88;
    public bool RequireYearMatch { get; set; } = true;
    public int YearTolerance { get; set; } = 0;
}

public class DuplicateMatch
{
    public Reference Primary { get; init; } = null!;
    public Reference Duplicate { get; init; } = null!;
    public HashSet<DuplicateReason> Reasons { get; init; } = new();
    public double? TitleSimilarity { get; set; }
}

public interface IDeduplicationService
{
    Task<IReadOnlyList<DuplicateMatch>> FindPotentialDuplicatesAsync(
        int projectId,
        DeduplicationOptions? options = null);
}
