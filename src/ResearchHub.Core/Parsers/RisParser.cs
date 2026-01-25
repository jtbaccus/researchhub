using ResearchHub.Core.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace ResearchHub.Core.Parsers;

public class RisParser : IReferenceParser
{
    public string Format => "RIS";
    public string[] SupportedExtensions => new[] { ".ris" };

    private static readonly Regex TagPattern = new(@"^([A-Z][A-Z0-9])  - ?(.*)$", RegexOptions.Compiled);

    public IEnumerable<Reference> Parse(string content)
    {
        var references = new List<Reference>();
        var currentRef = new Dictionary<string, List<string>>();

        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var match = TagPattern.Match(line);
            if (match.Success)
            {
                var tag = match.Groups[1].Value;
                var value = match.Groups[2].Value;

                if (tag == "ER")
                {
                    if (currentRef.Count > 0)
                    {
                        var reference = CreateReferenceFromRis(currentRef);
                        if (reference != null)
                            references.Add(reference);
                        currentRef.Clear();
                    }
                }
                else
                {
                    if (!currentRef.ContainsKey(tag))
                        currentRef[tag] = new List<string>();
                    currentRef[tag].Add(value);
                }
            }
        }

        // Handle case where file doesn't end with ER
        if (currentRef.Count > 0)
        {
            var reference = CreateReferenceFromRis(currentRef);
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

    private static Reference? CreateReferenceFromRis(Dictionary<string, List<string>> tags)
    {
        // TI or T1 = Title
        var title = GetFirstValue(tags, "TI", "T1");
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var reference = new Reference
        {
            Title = title,
            Abstract = GetFirstValue(tags, "AB", "N2"),
            Journal = GetFirstValue(tags, "JO", "JF", "T2", "JA"),
            Year = ParseYear(GetFirstValue(tags, "PY", "Y1", "DA")),
            Volume = GetFirstValue(tags, "VL"),
            Issue = GetFirstValue(tags, "IS"),
            Pages = FormatPages(GetFirstValue(tags, "SP"), GetFirstValue(tags, "EP")),
            Doi = GetFirstValue(tags, "DO", "DOI"),
            Pmid = GetFirstValue(tags, "PMID", "AN"),
            Url = GetFirstValue(tags, "UR", "L1", "L2")
        };

        // Authors: AU or A1
        if (tags.TryGetValue("AU", out var authors))
            reference.Authors = authors.ToList();
        else if (tags.TryGetValue("A1", out var a1Authors))
            reference.Authors = a1Authors.ToList();

        // Keywords as tags: KW
        if (tags.TryGetValue("KW", out var keywords))
            reference.Tags = keywords.ToList();

        return reference;
    }

    private static string? GetFirstValue(Dictionary<string, List<string>> tags, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (tags.TryGetValue(key, out var values) && values.Count > 0)
            {
                var value = values[0];
                return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            }
        }
        return null;
    }

    private static int? ParseYear(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Try to extract a 4-digit year from the beginning
        var yearMatch = Regex.Match(value, @"^(\d{4})");
        if (yearMatch.Success && int.TryParse(yearMatch.Groups[1].Value, out var year))
            return year;

        return null;
    }

    private static string? FormatPages(string? startPage, string? endPage)
    {
        if (string.IsNullOrWhiteSpace(startPage))
            return null;

        if (string.IsNullOrWhiteSpace(endPage))
            return startPage;

        return $"{startPage}-{endPage}";
    }
}
