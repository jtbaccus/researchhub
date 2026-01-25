using ResearchHub.Core.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace ResearchHub.Core.Parsers;

public class BibTexParser : IReferenceParser
{
    public string Format => "BibTeX";
    public string[] SupportedExtensions => new[] { ".bib", ".bibtex" };

    // Matches @type{key, ... }
    private static readonly Regex EntryPattern = new(
        @"@(\w+)\s*\{\s*([^,]*),",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Matches field = {value} or field = "value" or field = value
    private static readonly Regex FieldPattern = new(
        @"(\w+)\s*=\s*(?:\{([^{}]*(?:\{[^{}]*\}[^{}]*)*)\}|""([^""]*)""|(\d+))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public IEnumerable<Reference> Parse(string content)
    {
        var references = new List<Reference>();

        // Split content into entries
        var entries = SplitIntoEntries(content);

        foreach (var entry in entries)
        {
            var reference = ParseEntry(entry);
            if (reference != null)
                references.Add(reference);
        }

        return references;
    }

    public IEnumerable<Reference> ParseFile(string filePath)
    {
        var content = File.ReadAllText(filePath, Encoding.UTF8);
        var references = Parse(content).ToList();
        foreach (var reference in references)
        {
            reference.SourceFile = filePath;
        }
        return references;
    }

    private static IEnumerable<string> SplitIntoEntries(string content)
    {
        var entries = new List<string>();
        var depth = 0;
        var currentEntry = new StringBuilder();
        var inEntry = false;

        foreach (var ch in content)
        {
            if (ch == '@' && depth == 0)
            {
                if (inEntry && currentEntry.Length > 0)
                {
                    entries.Add(currentEntry.ToString());
                    currentEntry.Clear();
                }
                inEntry = true;
            }

            if (inEntry)
            {
                currentEntry.Append(ch);
                if (ch == '{')
                    depth++;
                else if (ch == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        entries.Add(currentEntry.ToString());
                        currentEntry.Clear();
                        inEntry = false;
                    }
                }
            }
        }

        if (currentEntry.Length > 0)
            entries.Add(currentEntry.ToString());

        return entries;
    }

    private static Reference? ParseEntry(string entry)
    {
        var entryMatch = EntryPattern.Match(entry);
        if (!entryMatch.Success)
            return null;

        var entryType = entryMatch.Groups[1].Value.ToLowerInvariant();

        // Skip non-reference entries like @string, @comment, @preamble
        if (entryType is "string" or "comment" or "preamble")
            return null;

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var fieldMatches = FieldPattern.Matches(entry);

        foreach (Match match in fieldMatches)
        {
            var fieldName = match.Groups[1].Value;
            var value = match.Groups[2].Success ? match.Groups[2].Value
                      : match.Groups[3].Success ? match.Groups[3].Value
                      : match.Groups[4].Value;

            fields[fieldName] = CleanLatex(value);
        }

        var title = GetField(fields, "title");
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var reference = new Reference
        {
            Title = title,
            Abstract = GetField(fields, "abstract"),
            Journal = GetField(fields, "journal", "booktitle"),
            Year = ParseYear(GetField(fields, "year")),
            Volume = GetField(fields, "volume"),
            Issue = GetField(fields, "number"),
            Pages = GetField(fields, "pages")?.Replace("--", "-"),
            Doi = GetField(fields, "doi"),
            Pmid = GetField(fields, "pmid"),
            Url = GetField(fields, "url")
        };

        // Parse authors
        var authors = GetField(fields, "author");
        if (!string.IsNullOrWhiteSpace(authors))
        {
            reference.Authors = ParseAuthors(authors);
        }

        // Parse keywords
        var keywords = GetField(fields, "keywords");
        if (!string.IsNullOrWhiteSpace(keywords))
        {
            reference.Tags = keywords.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(k => k.Trim())
                                    .Where(k => !string.IsNullOrEmpty(k))
                                    .ToList();
        }

        return reference;
    }

    private static string? GetField(Dictionary<string, string> fields, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }
        return null;
    }

    private static int? ParseYear(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (int.TryParse(value.Trim(), out var year))
            return year;

        return null;
    }

    private static List<string> ParseAuthors(string authors)
    {
        // BibTeX uses "and" to separate authors
        return authors.Split(new[] { " and " }, StringSplitOptions.RemoveEmptyEntries)
                     .Select(a => a.Trim())
                     .Where(a => !string.IsNullOrEmpty(a))
                     .ToList();
    }

    private static string CleanLatex(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        // Remove common LaTeX commands
        var result = value;

        // Remove \textit{}, \textbf{}, \emph{}, etc.
        result = Regex.Replace(result, @"\\(?:textit|textbf|emph|textrm|textsc)\{([^}]*)\}", "$1");

        // Remove \'{e} style accents -> just the letter
        result = Regex.Replace(result, @"\\['`^""~=.][{]?([a-zA-Z])[}]?", "$1");

        // Remove remaining braces (used for case preservation)
        result = result.Replace("{", "").Replace("}", "");

        // Clean up multiple spaces
        result = Regex.Replace(result, @"\s+", " ");

        return result.Trim();
    }
}
