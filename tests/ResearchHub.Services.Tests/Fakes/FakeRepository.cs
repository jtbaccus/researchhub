using System.Linq.Expressions;
using ResearchHub.Data.Repositories;

namespace ResearchHub.Services.Tests.Fakes;

public class FakeRepository<T> : IRepository<T> where T : class
{
    private readonly List<T> _items = new();
    private readonly Func<T, int> _getId;
    private readonly Action<T, int> _setId;
    private int _nextId = 1;

    public FakeRepository(Func<T, int> getId, Action<T, int> setId)
    {
        _getId = getId;
        _setId = setId;
    }

    public Task<T?> GetByIdAsync(int id)
    {
        return Task.FromResult(_items.FirstOrDefault(x => _getId(x) == id));
    }

    public Task<IEnumerable<T>> GetAllAsync()
    {
        return Task.FromResult(_items.AsEnumerable());
    }

    public Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        var compiled = predicate.Compile();
        return Task.FromResult(_items.Where(compiled));
    }

    public Task<T> AddAsync(T entity)
    {
        if (_getId(entity) == 0)
            _setId(entity, _nextId++);
        _items.Add(entity);
        return Task.FromResult(entity);
    }

    public Task AddRangeAsync(IEnumerable<T> entities)
    {
        foreach (var e in entities)
        {
            if (_getId(e) == 0)
                _setId(e, _nextId++);
            _items.Add(e);
        }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(T entity)
    {
        var id = _getId(entity);
        var idx = _items.FindIndex(x => _getId(x) == id);
        if (idx >= 0)
            _items[idx] = entity;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(T entity)
    {
        _items.RemoveAll(x => _getId(x) == _getId(entity));
        return Task.CompletedTask;
    }

    public Task DeleteByIdAsync(int id)
    {
        _items.RemoveAll(x => _getId(x) == id);
        return Task.CompletedTask;
    }

    public Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
    {
        var count = predicate == null ? _items.Count : _items.Count(predicate.Compile());
        return Task.FromResult(count);
    }
}
