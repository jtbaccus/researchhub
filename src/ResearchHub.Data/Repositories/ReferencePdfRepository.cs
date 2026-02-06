using Microsoft.EntityFrameworkCore;
using ResearchHub.Core.Models;

namespace ResearchHub.Data.Repositories;

public interface IReferencePdfRepository : IRepository<ReferencePdf>
{
    Task<IEnumerable<ReferencePdf>> GetByReferenceIdAsync(int referenceId);
}

public class ReferencePdfRepository : Repository<ReferencePdf>, IReferencePdfRepository
{
    public ReferencePdfRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<ReferencePdf>> GetByReferenceIdAsync(int referenceId)
    {
        return await DbSet
            .Where(p => p.ReferenceId == referenceId)
            .OrderByDescending(p => p.AddedAt)
            .ToListAsync();
    }
}
