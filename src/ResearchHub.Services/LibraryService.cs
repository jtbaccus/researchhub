using ResearchHub.Core.Exporters;
using ResearchHub.Core.Models;
using ResearchHub.Core.Parsers;
using ResearchHub.Data.Repositories;

namespace ResearchHub.Services;

public class LibraryService : ILibraryService
{
    private readonly IReferenceRepository _referenceRepository;
    private readonly Dictionary<string, IReferenceParser> _parsers;
    private readonly Dictionary<string, IReferenceExporter> _exporters;

    public LibraryService(IReferenceRepository referenceRepository)
    {
        _referenceRepository = referenceRepository;

        // Register parsers
        var risParser = new RisParser();
        var bibtexParser = new BibTexParser();
        var csvParser = new CsvReferenceParser();

        _parsers = new Dictionary<string, IReferenceParser>(StringComparer.OrdinalIgnoreCase)
        {
            [".ris"] = risParser,
            [".bib"] = bibtexParser,
            [".bibtex"] = bibtexParser,
            [".csv"] = csvParser
        };

        // Register exporters
        _exporters = new Dictionary<string, IReferenceExporter>(StringComparer.OrdinalIgnoreCase)
        {
            ["ris"] = new RisExporter(),
            ["bibtex"] = new BibTexExporter(),
            ["bib"] = new BibTexExporter(),
            ["csv"] = new CsvExporter()
        };
    }

    public async Task<ImportResult> ImportFromFileAsync(int projectId, string filePath)
    {
        var result = new ImportResult();

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (!_parsers.TryGetValue(extension, out var parser))
        {
            result.Errors.Add($"Unsupported file format: {extension}");
            return result;
        }

        try
        {
            var references = parser.ParseFile(filePath).ToList();
            result.TotalParsed = references.Count;

            return await ImportReferencesAsync(projectId, references, result);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error parsing file: {ex.Message}");
            return result;
        }
    }

    public async Task<ImportResult> ImportReferencesAsync(int projectId, IEnumerable<Reference> references)
    {
        return await ImportReferencesAsync(projectId, references.ToList(), new ImportResult());
    }

    private async Task<ImportResult> ImportReferencesAsync(int projectId, List<Reference> references, ImportResult result)
    {
        result.TotalParsed = references.Count;

        foreach (var reference in references)
        {
            try
            {
                // Check for duplicates by DOI or PMID
                if (!string.IsNullOrEmpty(reference.Doi))
                {
                    var existing = await _referenceRepository.GetByDoiAsync(projectId, reference.Doi);
                    if (existing != null)
                    {
                        result.Duplicates++;
                        continue;
                    }
                }

                if (!string.IsNullOrEmpty(reference.Pmid))
                {
                    var existing = await _referenceRepository.GetByPmidAsync(projectId, reference.Pmid);
                    if (existing != null)
                    {
                        result.Duplicates++;
                        continue;
                    }
                }

                reference.ProjectId = projectId;
                reference.ImportedAt = DateTime.UtcNow;
                await _referenceRepository.AddAsync(reference);
                result.Imported++;
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"Failed to import '{reference.Title}': {ex.Message}");
            }
        }

        return result;
    }

    public async Task<IEnumerable<Reference>> GetReferencesAsync(int projectId)
    {
        return await _referenceRepository.GetByProjectIdAsync(projectId);
    }

    public async Task<IEnumerable<Reference>> SearchReferencesAsync(int projectId, string searchTerm)
    {
        return await _referenceRepository.SearchAsync(projectId, searchTerm);
    }

    public async Task<Reference?> GetReferenceAsync(int id)
    {
        return await _referenceRepository.GetByIdAsync(id);
    }

    public async Task UpdateReferenceAsync(Reference reference)
    {
        await _referenceRepository.UpdateAsync(reference);
    }

    public async Task DeleteReferenceAsync(int id)
    {
        await _referenceRepository.DeleteByIdAsync(id);
    }

    public async Task ExportToFileAsync(int projectId, string filePath, string format)
    {
        var references = await GetReferencesAsync(projectId);

        if (!_exporters.TryGetValue(format, out var exporter))
        {
            throw new ArgumentException($"Unsupported export format: {format}");
        }

        exporter.ExportToFile(references, filePath);
    }

    public async Task<string> ExportToStringAsync(int projectId, string format)
    {
        var references = await GetReferencesAsync(projectId);

        if (!_exporters.TryGetValue(format, out var exporter))
        {
            throw new ArgumentException($"Unsupported export format: {format}");
        }

        return exporter.Export(references);
    }
}
