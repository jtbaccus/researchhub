using System.Linq.Expressions;
using ResearchHub.Core.Models;
using ResearchHub.Data.Repositories;

namespace ResearchHub.Services.Tests.Fakes;

public class FakeScreeningDecisionRepository : IScreeningDecisionRepository
{
    private readonly List<ScreeningDecision> _decisions = new();
    private readonly FakeReferenceRepository? _refRepo;
    private int _nextId = 1;

    public FakeScreeningDecisionRepository()
    {
    }

    /// <summary>
    /// Creates a fake that resolves Reference navigation properties from the given reference repository.
    /// This simulates EF Core's Include behavior.
    /// </summary>
    public FakeScreeningDecisionRepository(FakeReferenceRepository refRepo)
    {
        _refRepo = refRepo;
    }

    private void ResolveReference(ScreeningDecision decision)
    {
        if (decision.Reference == null && _refRepo != null)
        {
            decision.Reference = _refRepo.GetByIdAsync(decision.ReferenceId).Result;
        }
    }

    public Task<ScreeningDecision?> GetByIdAsync(int id)
    {
        return Task.FromResult(_decisions.FirstOrDefault(d => d.Id == id));
    }

    public Task<IEnumerable<ScreeningDecision>> GetAllAsync()
    {
        return Task.FromResult(_decisions.AsEnumerable());
    }

    public Task<IEnumerable<ScreeningDecision>> FindAsync(Expression<Func<ScreeningDecision, bool>> predicate)
    {
        var compiled = predicate.Compile();
        return Task.FromResult(_decisions.Where(compiled));
    }

    public Task<ScreeningDecision> AddAsync(ScreeningDecision entity)
    {
        if (entity.Id == 0)
            entity.Id = _nextId++;
        ResolveReference(entity);
        _decisions.Add(entity);
        return Task.FromResult(entity);
    }

    public Task AddRangeAsync(IEnumerable<ScreeningDecision> entities)
    {
        foreach (var e in entities)
        {
            if (e.Id == 0)
                e.Id = _nextId++;
            ResolveReference(e);
            _decisions.Add(e);
        }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ScreeningDecision entity)
    {
        var idx = _decisions.FindIndex(d => d.Id == entity.Id);
        if (idx >= 0)
            _decisions[idx] = entity;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(ScreeningDecision entity)
    {
        _decisions.RemoveAll(d => d.Id == entity.Id);
        return Task.CompletedTask;
    }

    public Task DeleteByIdAsync(int id)
    {
        _decisions.RemoveAll(d => d.Id == id);
        return Task.CompletedTask;
    }

    public Task<int> CountAsync(Expression<Func<ScreeningDecision, bool>>? predicate = null)
    {
        var count = predicate == null ? _decisions.Count : _decisions.Count(predicate.Compile());
        return Task.FromResult(count);
    }

    public Task<ScreeningDecision?> GetByReferenceAndPhaseAsync(int referenceId, ScreeningPhase phase)
    {
        var result = _decisions.FirstOrDefault(d => d.ReferenceId == referenceId && d.Phase == phase);
        return Task.FromResult(result);
    }

    public Task<IEnumerable<ScreeningDecision>> GetByProjectAndPhaseAsync(int projectId, ScreeningPhase phase)
    {
        foreach (var d in _decisions) ResolveReference(d);
        var result = _decisions
            .Where(d => d.Phase == phase && d.Reference != null && d.Reference.ProjectId == projectId)
            .AsEnumerable();
        return Task.FromResult(result);
    }

    public Task<ScreeningStats> GetStatsAsync(int projectId, ScreeningPhase phase)
    {
        foreach (var d in _decisions) ResolveReference(d);
        var decisions = _decisions
            .Where(d => d.Phase == phase && d.Reference != null && d.Reference.ProjectId == projectId)
            .ToList();

        var stats = new ScreeningStats
        {
            Total = decisions.Count,
            Pending = decisions.Count(d => d.Verdict == ScreeningVerdict.Pending),
            Included = decisions.Count(d => d.Verdict == ScreeningVerdict.Include),
            Excluded = decisions.Count(d => d.Verdict == ScreeningVerdict.Exclude),
            Maybe = decisions.Count(d => d.Verdict == ScreeningVerdict.Maybe)
        };
        return Task.FromResult(stats);
    }
}
