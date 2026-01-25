using ResearchHub.Core.Models;

namespace ResearchHub.Services;

public class ImportResult
{
    public int TotalParsed { get; set; }
    public int Imported { get; set; }
    public int Duplicates { get; set; }
    public int Failed { get; set; }
    public List<string> Errors { get; set; } = new();
}

public interface ILibraryService
{
    Task<ImportResult> ImportFromFileAsync(int projectId, string filePath);
    Task<ImportResult> ImportReferencesAsync(int projectId, IEnumerable<Reference> references);
    Task<IEnumerable<Reference>> GetReferencesAsync(int projectId);
    Task<IEnumerable<Reference>> SearchReferencesAsync(int projectId, string searchTerm);
    Task<Reference?> GetReferenceAsync(int id);
    Task UpdateReferenceAsync(Reference reference);
    Task DeleteReferenceAsync(int id);
    Task ExportToFileAsync(int projectId, string filePath, string format);
    Task<string> ExportToStringAsync(int projectId, string format);
}
