using Microsoft.EntityFrameworkCore;
using ResearchHub.Core.Models;

namespace ResearchHub.Data.Repositories;

public interface IReferenceRepository : IRepository<Reference>
{
    Task<IEnumerable<Reference>> GetByProjectIdAsync(int projectId);
    Task<IEnumerable<Reference>> SearchAsync(int projectId, string searchTerm);
    Task<Reference?> GetByDoiAsync(int projectId, string doi);
    Task<Reference?> GetByPmidAsync(int projectId, string pmid);
    Task<IEnumerable<Reference>> GetWithScreeningDecisionsAsync(int projectId);
}

public class ReferenceRepository : Repository<Reference>, IReferenceRepository
{
    public ReferenceRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Reference>> GetByProjectIdAsync(int projectId)
    {
        return await DbSet
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.ImportedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Reference>> SearchAsync(int projectId, string searchTerm)
    {
        var term = searchTerm.ToLower();
        return await DbSet
            .Where(r => r.ProjectId == projectId &&
                        (r.Title.ToLower().Contains(term) ||
                         (r.Abstract != null && r.Abstract.ToLower().Contains(term)) ||
                         (r.Journal != null && r.Journal.ToLower().Contains(term))))
            .OrderByDescending(r => r.ImportedAt)
            .ToListAsync();
    }

    public async Task<Reference?> GetByDoiAsync(int projectId, string doi)
    {
        return await DbSet
            .FirstOrDefaultAsync(r => r.ProjectId == projectId && r.Doi == doi);
    }

    public async Task<Reference?> GetByPmidAsync(int projectId, string pmid)
    {
        return await DbSet
            .FirstOrDefaultAsync(r => r.ProjectId == projectId && r.Pmid == pmid);
    }

    public async Task<IEnumerable<Reference>> GetWithScreeningDecisionsAsync(int projectId)
    {
        return await DbSet
            .Include(r => r.ScreeningDecisions)
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.ImportedAt)
            .ToListAsync();
    }
}
