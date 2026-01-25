using CsvHelper;
using CsvHelper.Configuration;
using ResearchHub.Core.Models;
using System.Globalization;
using System.Text;

namespace ResearchHub.Core.Parsers;

public class CsvReferenceParser : IReferenceParser
{
    public string Format => "CSV";
    public string[] SupportedExtensions => new[] { ".csv" };

    // Common column name mappings (case-insensitive)
    private static readonly Dictionary<string, string[]> ColumnMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Title"] = new[] { "title", "paper_title", "article_title" },
        ["Authors"] = new[] { "authors", "author", "author_names", "authors_list" },
        ["Abstract"] = new[] { "abstract", "summary", "description" },
        ["Journal"] = new[] { "journal", "source", "publication", "journal_name" },
        ["Year"] = new[] { "year", "publication_year", "pub_year", "date" },
        ["Volume"] = new[] { "volume", "vol" },
        ["Issue"] = new[] { "issue", "number", "issue_number" },
        ["Pages"] = new[] { "pages", "page", "page_range" },
        ["Doi"] = new[] { "doi", "digital_object_identifier" },
        ["Pmid"] = new[] { "pmid", "pubmed_id" },
        ["Url"] = new[] { "url", "link", "web_link" }
    };

    public IEnumerable<Reference> Parse(string content)
    {
        using var reader = new StringReader(content);
        return ParseFromReader(reader);
    }

    public IEnumerable<Reference> ParseFile(string filePath)
    {
        using var reader = new StreamReader(filePath, Encoding.UTF8);
        var references = ParseFromReader(reader).ToList();
        foreach (var reference in references)
        {
            reference.SourceFile = filePath;
        }
        return references;
    }

    private IEnumerable<Reference> ParseFromReader(TextReader reader)
    {
        var references = new List<Reference>();
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null,
            BadDataFound = null
        };

        using var csv = new CsvReader(reader, config);
        csv.Read();
        csv.ReadHeader();

        var headerRecord = csv.HeaderRecord;
        if (headerRecord == null)
            return references;

        var columnMap = MapColumns(headerRecord);

        while (csv.Read())
        {
            var reference = ParseRow(csv, columnMap, headerRecord);
            if (reference != null)
                references.Add(reference);
        }

        return references;
    }

    private static Dictionary<string, int> MapColumns(string[] headers)
    {
        var map = new Dictionary<string, int>();

        for (var i = 0; i < headers.Length; i++)
        {
            var header = headers[i].Trim();

            foreach (var (field, aliases) in ColumnMappings)
            {
                if (map.ContainsKey(field))
                    continue;

                if (aliases.Any(alias => string.Equals(header, alias, StringComparison.OrdinalIgnoreCase)))
                {
                    map[field] = i;
                    break;
                }
            }
        }

        return map;
    }

    private static Reference? ParseRow(CsvReader csv, Dictionary<string, int> columnMap, string[] headers)
    {
        var title = GetMappedField(csv, columnMap, "Title");
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var reference = new Reference
        {
            Title = title,
            Abstract = GetMappedField(csv, columnMap, "Abstract"),
            Journal = GetMappedField(csv, columnMap, "Journal"),
            Year = ParseYear(GetMappedField(csv, columnMap, "Year")),
            Volume = GetMappedField(csv, columnMap, "Volume"),
            Issue = GetMappedField(csv, columnMap, "Issue"),
            Pages = GetMappedField(csv, columnMap, "Pages"),
            Doi = GetMappedField(csv, columnMap, "Doi"),
            Pmid = GetMappedField(csv, columnMap, "Pmid"),
            Url = GetMappedField(csv, columnMap, "Url")
        };

        // Parse authors
        var authors = GetMappedField(csv, columnMap, "Authors");
        if (!string.IsNullOrWhiteSpace(authors))
        {
            reference.Authors = authors.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                                      .Select(a => a.Trim())
                                      .Where(a => !string.IsNullOrEmpty(a))
                                      .ToList();
        }

        // Store unmapped fields in CustomFields
        for (var i = 0; i < headers.Length; i++)
        {
            if (!columnMap.Values.Contains(i))
            {
                var value = csv.GetField(i);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    reference.CustomFields[headers[i]] = value;
                }
            }
        }

        return reference;
    }

    private static string? GetMappedField(CsvReader csv, Dictionary<string, int> columnMap, string field)
    {
        if (!columnMap.TryGetValue(field, out var index))
            return null;

        var value = csv.GetField(index);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int? ParseYear(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Try to extract a 4-digit year
        var match = System.Text.RegularExpressions.Regex.Match(value, @"\b(\d{4})\b");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var year))
            return year;

        return null;
    }
}
