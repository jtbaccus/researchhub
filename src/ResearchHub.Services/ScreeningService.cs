using ResearchHub.Core.Models;
using ResearchHub.Data.Repositories;

namespace ResearchHub.Services;

public class ScreeningService : IScreeningService
{
    private readonly IReferenceRepository _referenceRepository;
    private readonly IScreeningDecisionRepository _screeningRepository;

    public ScreeningService(
        IReferenceRepository referenceRepository,
        IScreeningDecisionRepository screeningRepository)
    {
        _referenceRepository = referenceRepository;
        _screeningRepository = screeningRepository;
    }

    public async Task InitializeScreeningAsync(int projectId, ScreeningPhase phase)
    {
        var references = await _referenceRepository.GetByProjectIdAsync(projectId);

        foreach (var reference in references)
        {
            var existing = await _screeningRepository.GetByReferenceAndPhaseAsync(reference.Id, phase);
            if (existing == null)
            {
                var decision = new ScreeningDecision
                {
                    ReferenceId = reference.Id,
                    Phase = phase,
                    Verdict = ScreeningVerdict.Pending
                };
                await _screeningRepository.AddAsync(decision);
            }
        }
    }

    public async Task<Reference?> GetNextForScreeningAsync(int projectId, ScreeningPhase phase)
    {
        var decisions = await _screeningRepository.GetByProjectAndPhaseAsync(projectId, phase);
        var pending = decisions.FirstOrDefault(d => d.Verdict == ScreeningVerdict.Pending);
        return pending?.Reference;
    }

    public async Task<IEnumerable<Reference>> GetScreeningQueueAsync(int projectId, ScreeningPhase phase)
    {
        var decisions = await _screeningRepository.GetByProjectAndPhaseAsync(projectId, phase);
        return decisions
            .Where(d => d.Verdict == ScreeningVerdict.Pending)
            .Select(d => d.Reference!)
            .ToList();
    }

    public async Task RecordDecisionAsync(int referenceId, ScreeningPhase phase, ScreeningVerdict verdict, string? exclusionReason = null, string? notes = null)
    {
        var decision = await _screeningRepository.GetByReferenceAndPhaseAsync(referenceId, phase);

        if (decision == null)
        {
            decision = new ScreeningDecision
            {
                ReferenceId = referenceId,
                Phase = phase,
                Verdict = verdict,
                ExclusionReason = exclusionReason,
                Notes = notes,
                DecidedAt = DateTime.UtcNow
            };
            await _screeningRepository.AddAsync(decision);
        }
        else
        {
            decision.Verdict = verdict;
            decision.ExclusionReason = exclusionReason;
            decision.Notes = notes;
            decision.DecidedAt = DateTime.UtcNow;
            await _screeningRepository.UpdateAsync(decision);
        }
    }

    public async Task<ScreeningStats> GetStatsAsync(int projectId, ScreeningPhase phase)
    {
        return await _screeningRepository.GetStatsAsync(projectId, phase);
    }

    public async Task<IEnumerable<Reference>> GetByVerdictAsync(int projectId, ScreeningPhase phase, ScreeningVerdict verdict)
    {
        var decisions = await _screeningRepository.GetByProjectAndPhaseAsync(projectId, phase);
        return decisions
            .Where(d => d.Verdict == verdict)
            .Select(d => d.Reference!)
            .ToList();
    }
}
