using System.Text;
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

    private const long WarnFileSizeBytes = 50 * 1024 * 1024;   // 50 MB
    private const long RejectFileSizeBytes = 200 * 1024 * 1024; // 200 MB

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
        return await ImportFromFileAsync(projectId, filePath, null);
    }

    public async Task<ImportResult> ImportFromFileAsync(int projectId, string filePath, IProgress<ImportProgress>? progress = null)
    {
        var result = new ImportResult();

        // File existence check
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Import file not found: {filePath}", filePath);
        }

        // File size checks
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > RejectFileSizeBytes)
        {
            result.Errors.Add($"File is too large ({fileInfo.Length / (1024 * 1024)} MB). Maximum allowed size is 200 MB.");
            return result;
        }
        if (fileInfo.Length > WarnFileSizeBytes)
        {
            result.Warnings.Add($"Large file ({fileInfo.Length / (1024 * 1024)} MB). Import may take a while.");
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (!_parsers.TryGetValue(extension, out var parser))
        {
            result.Errors.Add($"Unsupported file format: {extension}");
            return result;
        }

        try
        {
            // Encoding detection: try UTF-8 first, fall back to Latin-1
            string content;
            try
            {
                content = await File.ReadAllTextAsync(filePath, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));
            }
            catch (DecoderFallbackException)
            {
                result.Warnings.Add("File is not valid UTF-8. Falling back to Latin-1 encoding.");
                content = await File.ReadAllTextAsync(filePath, Encoding.Latin1);
            }

            var references = parser.Parse(content).ToList();

            // Set SourceFile on each parsed reference
            foreach (var reference in references)
            {
                reference.SourceFile = filePath;
            }

            result.TotalParsed = references.Count;

            return await ImportReferencesAsync(projectId, references, result, progress);
        }
        catch (Exception ex) when (ex is not FileNotFoundException)
        {
            result.Errors.Add($"Error parsing file: {ex.Message}");
            return result;
        }
    }

    public async Task<ImportResult> ImportReferencesAsync(int projectId, IEnumerable<Reference> references)
    {
        return await ImportReferencesAsync(projectId, references.ToList(), new ImportResult(), null);
    }

    private async Task<ImportResult> ImportReferencesAsync(
        int projectId,
        List<Reference> references,
        ImportResult result,
        IProgress<ImportProgress>? progress = null)
    {
        result.TotalParsed = references.Count;
        var total = references.Count;

        for (var i = 0; i < references.Count; i++)
        {
            var reference = references[i];
            try
            {
                // Report progress
                progress?.Report(new ImportProgress
                {
                    Current = i + 1,
                    Total = total,
                    CurrentTitle = reference.Title ?? "(no title)"
                });

                // Skip references with no title
                if (string.IsNullOrWhiteSpace(reference.Title))
                {
                    result.SkippedNoTitle++;
                    continue;
                }

                // Check for duplicates by DOI (normalized)
                if (!string.IsNullOrEmpty(reference.Doi))
                {
                    var normalizedDoi = NormalizeDoi(reference.Doi);
                    if (normalizedDoi != null)
                    {
                        var existing = await _referenceRepository.GetByDoiAsync(projectId, reference.Doi);
                        if (existing != null)
                        {
                            // Also try normalized comparison
                            result.Duplicates++;
                            continue;
                        }
                        // Normalize the DOI on the reference before storing
                        reference.Doi = normalizedDoi;
                    }
                }

                // Check for duplicates by PMID (normalized)
                if (!string.IsNullOrEmpty(reference.Pmid))
                {
                    var normalizedPmid = NormalizePmid(reference.Pmid);
                    if (normalizedPmid != null)
                    {
                        var existing = await _referenceRepository.GetByPmidAsync(projectId, reference.Pmid);
                        if (existing != null)
                        {
                            result.Duplicates++;
                            continue;
                        }
                        // Normalize the PMID on the reference before storing
                        reference.Pmid = normalizedPmid;
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

    /// <summary>
    /// Normalizes a DOI string by stripping URL prefixes, lowering case,
    /// trimming whitespace, and removing trailing punctuation.
    /// Mirrors DeduplicationService.NormalizeDoi pattern.
    /// </summary>
    internal static string? NormalizeDoi(string? doi)
    {
        if (string.IsNullOrWhiteSpace(doi))
            return null;

        var normalized = doi.Trim().ToLowerInvariant();

        if (normalized.StartsWith("doi:"))
            normalized = normalized[4..];

        normalized = normalized
            .Replace("https://doi.org/", "", StringComparison.OrdinalIgnoreCase)
            .Replace("http://doi.org/", "", StringComparison.OrdinalIgnoreCase)
            .Replace("https://dx.doi.org/", "", StringComparison.OrdinalIgnoreCase)
            .Replace("http://dx.doi.org/", "", StringComparison.OrdinalIgnoreCase)
            .Trim()
            .TrimEnd('.', ',');

        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    /// <summary>
    /// Normalizes a PMID by extracting only digit characters
    /// (strips leading zeros implicitly by trimming).
    /// Mirrors DeduplicationService.NormalizePmid pattern.
    /// </summary>
    internal static string? NormalizePmid(string? pmid)
    {
        if (string.IsNullOrWhiteSpace(pmid))
            return null;

        var digits = new string(pmid.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? null : digits;
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
