using Microsoft.EntityFrameworkCore;
using ResearchHub.Core.Models;

namespace ResearchHub.Data.Repositories;

public interface IProjectRepository : IRepository<Project>
{
    Task<Project?> GetWithReferencesAsync(int id);
    Task<Project?> GetWithAllRelatedDataAsync(int id);
}

public class ProjectRepository : Repository<Project>, IProjectRepository
{
    public ProjectRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Project?> GetWithReferencesAsync(int id)
    {
        return await DbSet
            .Include(p => p.References)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Project?> GetWithAllRelatedDataAsync(int id)
    {
        return await DbSet
            .Include(p => p.References)
                .ThenInclude(r => r.ScreeningDecisions)
            .Include(p => p.ExtractionSchemas)
                .ThenInclude(s => s.Rows)
            .FirstOrDefaultAsync(p => p.Id == id);
    }
}
