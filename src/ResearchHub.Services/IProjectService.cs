using ResearchHub.Core.Models;

namespace ResearchHub.Services;

public interface IProjectService
{
    Task<Project> CreateProjectAsync(string title, string? researchQuestion = null, string? description = null);
    Task<Project?> GetProjectAsync(int id);
    Task<Project?> GetProjectWithReferencesAsync(int id);
    Task<IEnumerable<Project>> GetAllProjectsAsync();
    Task UpdateProjectAsync(Project project);
    Task DeleteProjectAsync(int id);
}
