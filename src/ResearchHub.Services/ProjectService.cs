using ResearchHub.Core.Models;
using ResearchHub.Data.Repositories;

namespace ResearchHub.Services;

public class ProjectService : IProjectService
{
    private readonly IProjectRepository _projectRepository;

    public ProjectService(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task<Project> CreateProjectAsync(string title, string? researchQuestion = null, string? description = null)
    {
        var project = new Project
        {
            Title = title,
            ResearchQuestion = researchQuestion,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };

        return await _projectRepository.AddAsync(project);
    }

    public async Task<Project?> GetProjectAsync(int id)
    {
        return await _projectRepository.GetByIdAsync(id);
    }

    public async Task<Project?> GetProjectWithReferencesAsync(int id)
    {
        return await _projectRepository.GetWithReferencesAsync(id);
    }

    public async Task<IEnumerable<Project>> GetAllProjectsAsync()
    {
        return await _projectRepository.GetAllAsync();
    }

    public async Task UpdateProjectAsync(Project project)
    {
        project.ModifiedAt = DateTime.UtcNow;
        await _projectRepository.UpdateAsync(project);
    }

    public async Task DeleteProjectAsync(int id)
    {
        await _projectRepository.DeleteByIdAsync(id);
    }
}
