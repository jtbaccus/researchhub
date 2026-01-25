using ResearchHub.Core.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace ResearchHub.Core.Exporters;

public class BibTexExporter : IReferenceExporter
{
    public string Format => "BibTeX";
    public string FileExtension => ".bib";

    public string Export(IEnumerable<Reference> references)
    {
        var sb = new StringBuilder();
        var keyCounter = new Dictionary<string, int>();

        foreach (var reference in references)
        {
            ExportReference(reference, sb, keyCounter);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public void ExportToFile(IEnumerable<Reference> references, string filePath)
    {
        var content = Export(references);
        File.WriteAllText(filePath, content, Encoding.UTF8);
    }

    private static void ExportReference(Reference reference, StringBuilder sb, Dictionary<string, int> keyCounter)
    {
        var key = GenerateCiteKey(reference, keyCounter);

        sb.AppendLine("@article{" + key + ",");

        // Title
        sb.AppendLine($"  title = {{{EscapeBibTeX(reference.Title)}}},");

        // Authors
        if (reference.Authors.Count > 0)
        {
            var authors = string.Join(" and ", reference.Authors);
            sb.AppendLine($"  author = {{{EscapeBibTeX(authors)}}},");
        }

        // Abstract
        if (!string.IsNullOrWhiteSpace(reference.Abstract))
            sb.AppendLine($"  abstract = {{{EscapeBibTeX(reference.Abstract)}}},");

        // Journal
        if (!string.IsNullOrWhiteSpace(reference.Journal))
            sb.AppendLine($"  journal = {{{EscapeBibTeX(reference.Journal)}}},");

        // Year
        if (reference.Year.HasValue)
            sb.AppendLine($"  year = {{{reference.Year}}},");

        // Volume
        if (!string.IsNullOrWhiteSpace(reference.Volume))
            sb.AppendLine($"  volume = {{{reference.Volume}}},");

        // Issue/Number
        if (!string.IsNullOrWhiteSpace(reference.Issue))
            sb.AppendLine($"  number = {{{reference.Issue}}},");

        // Pages
        if (!string.IsNullOrWhiteSpace(reference.Pages))
            sb.AppendLine($"  pages = {{{reference.Pages.Replace("-", "--")}}},");

        // DOI
        if (!string.IsNullOrWhiteSpace(reference.Doi))
            sb.AppendLine($"  doi = {{{reference.Doi}}},");

        // PMID
        if (!string.IsNullOrWhiteSpace(reference.Pmid))
            sb.AppendLine($"  pmid = {{{reference.Pmid}}},");

        // URL
        if (!string.IsNullOrWhiteSpace(reference.Url))
            sb.AppendLine($"  url = {{{reference.Url}}},");

        // Keywords
        if (reference.Tags.Count > 0)
        {
            var keywords = string.Join(", ", reference.Tags);
            sb.AppendLine($"  keywords = {{{keywords}}},");
        }

        sb.AppendLine("}");
    }

    private static string GenerateCiteKey(Reference reference, Dictionary<string, int> keyCounter)
    {
        var baseKey = new StringBuilder();

        // First author's last name
        if (reference.Authors.Count > 0)
        {
            var firstAuthor = reference.Authors[0];
            var lastName = ExtractLastName(firstAuthor);
            baseKey.Append(SanitizeForKey(lastName));
        }
        else
        {
            baseKey.Append("unknown");
        }

        // Year
        if (reference.Year.HasValue)
            baseKey.Append(reference.Year);

        // First significant word from title
        var titleWord = GetFirstSignificantWord(reference.Title);
        if (!string.IsNullOrEmpty(titleWord))
            baseKey.Append(SanitizeForKey(titleWord));

        var key = baseKey.ToString();

        // Handle duplicates
        if (keyCounter.TryGetValue(key, out var count))
        {
            keyCounter[key] = count + 1;
            key = $"{key}{(char)('a' + count)}";
        }
        else
        {
            keyCounter[key] = 1;
        }

        return key;
    }

    private static string ExtractLastName(string author)
    {
        // Handle "Last, First" format
        if (author.Contains(','))
            return author.Split(',')[0].Trim();

        // Handle "First Last" format
        var parts = author.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[^1] : author;
    }

    private static string GetFirstSignificantWord(string title)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "the", "of", "in", "on", "at", "to", "for", "and", "or", "but", "is", "are", "was", "were"
        };

        var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            var cleaned = Regex.Replace(word, @"[^\w]", "");
            if (!string.IsNullOrEmpty(cleaned) && !stopWords.Contains(cleaned))
                return cleaned;
        }

        return words.Length > 0 ? Regex.Replace(words[0], @"[^\w]", "") : "";
    }

    private static string SanitizeForKey(string value)
    {
        // Remove non-alphanumeric characters and convert to lowercase
        return Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]", "");
    }

    private static string EscapeBibTeX(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        // Escape special BibTeX characters
        return value
            .Replace("&", @"\&")
            .Replace("%", @"\%")
            .Replace("$", @"\$")
            .Replace("#", @"\#")
            .Replace("_", @"\_")
            .Replace("~", @"\~{}")
            .Replace("^", @"\^{}");
    }
}
