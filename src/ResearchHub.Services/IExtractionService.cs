using ResearchHub.Core.Models;

namespace ResearchHub.Services;

public interface IExtractionService
{
    Task<ExtractionSchema> CreateSchemaAsync(int projectId, string name, string? description, List<ExtractionColumn> columns);
    Task<ExtractionSchema?> GetSchemaAsync(int id);
    Task<IEnumerable<ExtractionSchema>> GetSchemasByProjectAsync(int projectId);
    Task UpdateSchemaAsync(ExtractionSchema schema);
    Task DeleteSchemaAsync(int id);

    Task<ExtractionRow> SaveExtractionAsync(int referenceId, int schemaId, Dictionary<string, string> values);
    Task<ExtractionRow?> GetExtractionAsync(int referenceId, int schemaId);
    Task<IEnumerable<ExtractionRow>> GetExtractionsForSchemaAsync(int schemaId);

    Task<int> ImportFromCsvAsync(int schemaId, string filePath, string referenceIdColumn);
    Task ExportToCsvAsync(int schemaId, string filePath);
    Task ExportToExcelAsync(int schemaId, string filePath);
}
