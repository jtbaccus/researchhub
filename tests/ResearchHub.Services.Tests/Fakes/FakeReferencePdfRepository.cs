using System.Linq.Expressions;
using ResearchHub.Core.Models;
using ResearchHub.Data.Repositories;

namespace ResearchHub.Services.Tests.Fakes;

public class FakeReferencePdfRepository : IReferencePdfRepository
{
    private readonly List<ReferencePdf> _pdfs = new();
    private int _nextId = 1;

    public Task<ReferencePdf?> GetByIdAsync(int id)
    {
        return Task.FromResult(_pdfs.FirstOrDefault(p => p.Id == id));
    }

    public Task<IEnumerable<ReferencePdf>> GetAllAsync()
    {
        return Task.FromResult(_pdfs.AsEnumerable());
    }

    public Task<IEnumerable<ReferencePdf>> FindAsync(Expression<Func<ReferencePdf, bool>> predicate)
    {
        var compiled = predicate.Compile();
        return Task.FromResult(_pdfs.Where(compiled));
    }

    public Task<ReferencePdf> AddAsync(ReferencePdf entity)
    {
        if (entity.Id == 0)
            entity.Id = _nextId++;
        _pdfs.Add(entity);
        return Task.FromResult(entity);
    }

    public Task AddRangeAsync(IEnumerable<ReferencePdf> entities)
    {
        foreach (var e in entities)
        {
            if (e.Id == 0)
                e.Id = _nextId++;
            _pdfs.Add(e);
        }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ReferencePdf entity)
    {
        var idx = _pdfs.FindIndex(p => p.Id == entity.Id);
        if (idx >= 0)
            _pdfs[idx] = entity;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(ReferencePdf entity)
    {
        _pdfs.RemoveAll(p => p.Id == entity.Id);
        return Task.CompletedTask;
    }

    public Task DeleteByIdAsync(int id)
    {
        _pdfs.RemoveAll(p => p.Id == id);
        return Task.CompletedTask;
    }

    public Task<int> CountAsync(Expression<Func<ReferencePdf, bool>>? predicate = null)
    {
        var count = predicate == null ? _pdfs.Count : _pdfs.Count(predicate.Compile());
        return Task.FromResult(count);
    }

    public Task<IEnumerable<ReferencePdf>> GetByReferenceIdAsync(int referenceId)
    {
        var result = _pdfs
            .Where(p => p.ReferenceId == referenceId)
            .OrderByDescending(p => p.AddedAt)
            .AsEnumerable();
        return Task.FromResult(result);
    }
}
