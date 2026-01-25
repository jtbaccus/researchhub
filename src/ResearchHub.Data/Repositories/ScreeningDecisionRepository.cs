using Microsoft.EntityFrameworkCore;
using ResearchHub.Core.Models;

namespace ResearchHub.Data.Repositories;

public interface IScreeningDecisionRepository : IRepository<ScreeningDecision>
{
    Task<ScreeningDecision?> GetByReferenceAndPhaseAsync(int referenceId, ScreeningPhase phase);
    Task<IEnumerable<ScreeningDecision>> GetByProjectAndPhaseAsync(int projectId, ScreeningPhase phase);
    Task<ScreeningStats> GetStatsAsync(int projectId, ScreeningPhase phase);
}

public class ScreeningStats
{
    public int Total { get; set; }
    public int Pending { get; set; }
    public int Included { get; set; }
    public int Excluded { get; set; }
    public int Maybe { get; set; }
}

public class ScreeningDecisionRepository : Repository<ScreeningDecision>, IScreeningDecisionRepository
{
    public ScreeningDecisionRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<ScreeningDecision?> GetByReferenceAndPhaseAsync(int referenceId, ScreeningPhase phase)
    {
        return await DbSet
            .FirstOrDefaultAsync(d => d.ReferenceId == referenceId && d.Phase == phase);
    }

    public async Task<IEnumerable<ScreeningDecision>> GetByProjectAndPhaseAsync(int projectId, ScreeningPhase phase)
    {
        return await DbSet
            .Include(d => d.Reference)
            .Where(d => d.Reference!.ProjectId == projectId && d.Phase == phase)
            .ToListAsync();
    }

    public async Task<ScreeningStats> GetStatsAsync(int projectId, ScreeningPhase phase)
    {
        var decisions = await DbSet
            .Include(d => d.Reference)
            .Where(d => d.Reference!.ProjectId == projectId && d.Phase == phase)
            .ToListAsync();

        return new ScreeningStats
        {
            Total = decisions.Count,
            Pending = decisions.Count(d => d.Verdict == ScreeningVerdict.Pending),
            Included = decisions.Count(d => d.Verdict == ScreeningVerdict.Include),
            Excluded = decisions.Count(d => d.Verdict == ScreeningVerdict.Exclude),
            Maybe = decisions.Count(d => d.Verdict == ScreeningVerdict.Maybe)
        };
    }
}
