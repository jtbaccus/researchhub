using ResearchHub.Core.Models;
using ResearchHub.Data.Repositories;

namespace ResearchHub.Services;

public interface IScreeningService
{
    Task InitializeScreeningAsync(int projectId, ScreeningPhase phase);
    Task<Reference?> GetNextForScreeningAsync(int projectId, ScreeningPhase phase);
    Task<IEnumerable<Reference>> GetScreeningQueueAsync(int projectId, ScreeningPhase phase);
    Task RecordDecisionAsync(int referenceId, ScreeningPhase phase, ScreeningVerdict verdict, string? exclusionReason = null, string? notes = null);
    Task<ScreeningStats> GetStatsAsync(int projectId, ScreeningPhase phase);
    Task<IEnumerable<Reference>> GetByVerdictAsync(int projectId, ScreeningPhase phase, ScreeningVerdict verdict);
}
