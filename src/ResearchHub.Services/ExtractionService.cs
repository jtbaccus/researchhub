using CsvHelper;
using ResearchHub.Core.Models;
using ResearchHub.Data.Repositories;
using System.Globalization;

namespace ResearchHub.Services;

public class ExtractionService : IExtractionService
{
    private readonly IRepository<ExtractionSchema> _schemaRepository;
    private readonly IRepository<ExtractionRow> _rowRepository;
    private readonly IReferenceRepository _referenceRepository;

    public ExtractionService(
        IRepository<ExtractionSchema> schemaRepository,
        IRepository<ExtractionRow> rowRepository,
        IReferenceRepository referenceRepository)
    {
        _schemaRepository = schemaRepository;
        _rowRepository = rowRepository;
        _referenceRepository = referenceRepository;
    }

    public async Task<ExtractionSchema> CreateSchemaAsync(int projectId, string name, string? description, List<ExtractionColumn> columns)
    {
        var schema = new ExtractionSchema
        {
            ProjectId = projectId,
            Name = name,
            Description = description,
            Columns = columns,
            CreatedAt = DateTime.UtcNow
        };

        return await _schemaRepository.AddAsync(schema);
    }

    public async Task<ExtractionSchema?> GetSchemaAsync(int id)
    {
        return await _schemaRepository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<ExtractionSchema>> GetSchemasByProjectAsync(int projectId)
    {
        return await _schemaRepository.FindAsync(s => s.ProjectId == projectId);
    }

    public async Task UpdateSchemaAsync(ExtractionSchema schema)
    {
        await _schemaRepository.UpdateAsync(schema);
    }

    public async Task DeleteSchemaAsync(int id)
    {
        await _schemaRepository.DeleteByIdAsync(id);
    }

    public async Task<ExtractionRow> SaveExtractionAsync(int referenceId, int schemaId, Dictionary<string, string> values)
    {
        var existing = await GetExtractionAsync(referenceId, schemaId);

        if (existing != null)
        {
            existing.Values = values;
            existing.ModifiedAt = DateTime.UtcNow;
            await _rowRepository.UpdateAsync(existing);
            return existing;
        }

        var row = new ExtractionRow
        {
            ReferenceId = referenceId,
            SchemaId = schemaId,
            Values = values,
            CreatedAt = DateTime.UtcNow
        };

        return await _rowRepository.AddAsync(row);
    }

    public async Task<ExtractionRow?> GetExtractionAsync(int referenceId, int schemaId)
    {
        var rows = await _rowRepository.FindAsync(r => r.ReferenceId == referenceId && r.SchemaId == schemaId);
        return rows.FirstOrDefault();
    }

    public async Task<IEnumerable<ExtractionRow>> GetExtractionsForSchemaAsync(int schemaId)
    {
        return await _rowRepository.FindAsync(r => r.SchemaId == schemaId);
    }

    public async Task<int> ImportFromCsvAsync(int schemaId, string filePath, string referenceIdColumn)
    {
        var schema = await GetSchemaAsync(schemaId);
        if (schema == null)
            throw new ArgumentException("Schema not found");

        var imported = 0;

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
            var refIdStr = csv.GetField(referenceIdColumn);
            if (string.IsNullOrEmpty(refIdStr) || !int.TryParse(refIdStr, out var referenceId))
                continue;

            var values = new Dictionary<string, string>();
            foreach (var column in schema.Columns)
            {
                var value = csv.GetField(column.Name);
                if (!string.IsNullOrEmpty(value))
                {
                    values[column.Name] = value;
                }
            }

            if (values.Count > 0)
            {
                await SaveExtractionAsync(referenceId, schemaId, values);
                imported++;
            }
        }

        return imported;
    }

    public async Task ExportToCsvAsync(int schemaId, string filePath)
    {
        var schema = await GetSchemaAsync(schemaId);
        if (schema == null)
            throw new ArgumentException("Schema not found");

        var rows = await GetExtractionsForSchemaAsync(schemaId);

        using var writer = new StreamWriter(filePath);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        // Write header
        csv.WriteField("ReferenceId");
        csv.WriteField("Title");
        foreach (var column in schema.Columns)
        {
            csv.WriteField(column.Name);
        }
        csv.NextRecord();

        // Write rows
        foreach (var row in rows)
        {
            var reference = await _referenceRepository.GetByIdAsync(row.ReferenceId);

            csv.WriteField(row.ReferenceId);
            csv.WriteField(reference?.Title ?? "");

            foreach (var column in schema.Columns)
            {
                row.Values.TryGetValue(column.Name, out var value);
                csv.WriteField(value ?? "");
            }
            csv.NextRecord();
        }
    }
}
