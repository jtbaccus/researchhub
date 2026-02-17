using System.Linq.Expressions;
using ResearchHub.Core.Models;
using ResearchHub.Data.Repositories;

namespace ResearchHub.Services.Tests.Fakes;

public class FakeReferenceRepository : IReferenceRepository
{
    private readonly List<Reference> _references = new();
    private int _nextId = 1;

    public void Seed(IEnumerable<Reference> references)
    {
        foreach (var r in references)
        {
            if (r.Id == 0)
                r.Id = _nextId++;
            _references.Add(r);
        }
    }

    public Task<IEnumerable<Reference>> GetByProjectIdAsync(int projectId)
    {
        var result = _references.Where(r => r.ProjectId == projectId).AsEnumerable();
        return Task.FromResult(result);
    }

    public Task<Reference?> GetByIdAsync(int id)
    {
        return Task.FromResult(_references.FirstOrDefault(r => r.Id == id));
    }

    public Task<IEnumerable<Reference>> GetAllAsync()
    {
        return Task.FromResult(_references.AsEnumerable());
    }

    public Task<IEnumerable<Reference>> FindAsync(Expression<Func<Reference, bool>> predicate)
    {
        var compiled = predicate.Compile();
        return Task.FromResult(_references.Where(compiled));
    }

    public Task<Reference> AddAsync(Reference entity)
    {
        if (entity.Id == 0)
            entity.Id = _nextId++;
        _references.Add(entity);
        return Task.FromResult(entity);
    }

    public Task AddRangeAsync(IEnumerable<Reference> entities)
    {
        foreach (var e in entities)
        {
            if (e.Id == 0)
                e.Id = _nextId++;
            _references.Add(e);
        }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Reference entity) => Task.CompletedTask;
    public Task DeleteAsync(Reference entity)
    {
        _references.RemoveAll(r => r.Id == entity.Id);
        return Task.CompletedTask;
    }

    public Task DeleteByIdAsync(int id)
    {
        _references.RemoveAll(r => r.Id == id);
        return Task.CompletedTask;
    }

    public Task<int> CountAsync(Expression<Func<Reference, bool>>? predicate = null)
    {
        var count = predicate == null ? _references.Count : _references.Count(predicate.Compile());
        return Task.FromResult(count);
    }

    public Task<IEnumerable<Reference>> SearchAsync(int projectId, string searchTerm)
    {
        var term = searchTerm.ToLower();
        var result = _references.Where(r => r.ProjectId == projectId &&
            (r.Title.ToLower().Contains(term) ||
             (r.Abstract != null && r.Abstract.ToLower().Contains(term)) ||
             (r.Journal != null && r.Journal.ToLower().Contains(term))));
        return Task.FromResult(result);
    }

    public Task<Reference?> GetByDoiAsync(int projectId, string doi)
    {
        var result = _references.FirstOrDefault(r => r.ProjectId == projectId && r.Doi == doi);
        return Task.FromResult(result);
    }

    public Task<Reference?> GetByPmidAsync(int projectId, string pmid)
    {
        var result = _references.FirstOrDefault(r => r.ProjectId == projectId && r.Pmid == pmid);
        return Task.FromResult(result);
    }

    public Task<IEnumerable<Reference>> GetWithScreeningDecisionsAsync(int projectId)
    {
        var result = _references.Where(r => r.ProjectId == projectId).AsEnumerable();
        return Task.FromResult(result);
    }
}
