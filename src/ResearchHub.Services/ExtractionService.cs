using ClosedXML.Excel;
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
        csv.WriteField("Authors");
        csv.WriteField("Journal");
        csv.WriteField("Year");
        csv.WriteField("DOI");
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
            csv.WriteField(reference != null ? string.Join("; ", reference.Authors) : "");
            csv.WriteField(reference?.Journal ?? "");
            csv.WriteField(reference?.Year?.ToString() ?? "");
            csv.WriteField(reference?.Doi ?? "");

            foreach (var column in schema.Columns)
            {
                row.Values.TryGetValue(column.Name, out var value);
                csv.WriteField(value ?? "");
            }
            csv.NextRecord();
        }
    }

    public async Task ExportToExcelAsync(int schemaId, string filePath)
    {
        var schema = await GetSchemaAsync(schemaId);
        if (schema == null)
            throw new ArgumentException("Schema not found");

        var rows = await GetExtractionsForSchemaAsync(schemaId);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Extraction Data");

        // Write header row
        var headers = new List<string> { "ReferenceId", "Title", "Authors", "Journal", "Year", "DOI" };
        headers.AddRange(schema.Columns.Select(c => c.Name));

        for (int i = 0; i < headers.Count; i++)
        {
            var cell = worksheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
        }

        // Write data rows
        int rowNum = 2;
        foreach (var row in rows)
        {
            var reference = await _referenceRepository.GetByIdAsync(row.ReferenceId);

            worksheet.Cell(rowNum, 1).Value = row.ReferenceId;
            worksheet.Cell(rowNum, 2).Value = reference?.Title ?? "";
            worksheet.Cell(rowNum, 3).Value = reference != null ? string.Join("; ", reference.Authors) : "";
            worksheet.Cell(rowNum, 4).Value = reference?.Journal ?? "";
            worksheet.Cell(rowNum, 5).Value = reference?.Year?.ToString() ?? "";
            worksheet.Cell(rowNum, 6).Value = reference?.Doi ?? "";

            for (int i = 0; i < schema.Columns.Count; i++)
            {
                row.Values.TryGetValue(schema.Columns[i].Name, out var value);
                worksheet.Cell(rowNum, 7 + i).Value = value ?? "";
            }

            rowNum++;
        }

        worksheet.Columns().AdjustToContents();
        workbook.SaveAs(filePath);
    }
}
