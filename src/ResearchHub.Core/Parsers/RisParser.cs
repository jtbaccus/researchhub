using ResearchHub.Core.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace ResearchHub.Core.Parsers;

public class RisParser : IReferenceParser
{
    public string Format => "RIS";
    public string[] SupportedExtensions => new[] { ".ris" };

    private static readonly Regex TagPattern = new(@"^([A-Z][A-Z0-9])\s*-\s?(.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public IEnumerable<Reference> Parse(string content)
    {
        var references = new List<Reference>();
        var currentRef = new Dictionary<string, List<string>>();
        string? lastTag = null;

        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var match = TagPattern.Match(line);
            if (match.Success)
            {
                var tag = match.Groups[1].Value.ToUpperInvariant();
                var value = match.Groups[2].Value;

                if (tag == "ER")
                {
                    if (currentRef.Count > 0)
                    {
                        try
                        {
                            var reference = CreateReferenceFromRis(currentRef);
                            if (reference != null)
                                references.Add(reference);
                        }
                        catch (Exception)
                        {
                            // Skip malformed entry and continue parsing
                        }
                        currentRef.Clear();
                    }
                    lastTag = null;
                }
                else
                {
                    var trimmed = value.Trim();
                    if (!currentRef.ContainsKey(tag))
                        currentRef[tag] = new List<string>();
                    if (trimmed.Length > 0)
                        currentRef[tag].Add(trimmed);
                    lastTag = tag;
                }
            }
            else if (lastTag != null && char.IsWhiteSpace(line[0]))
            {
                // RIS continuation lines begin with whitespace and extend the previous tag value.
                var continuation = line.Trim();
                if (continuation.Length > 0)
                {
                    var list = currentRef[lastTag];
                    list[list.Count - 1] = $"{list[list.Count - 1]} {continuation}".Trim();
                }
            }
        }

        // Handle case where file doesn't end with ER
        if (currentRef.Count > 0)
        {
            try
            {
                var reference = CreateReferenceFromRis(currentRef);
                if (reference != null)
                    references.Add(reference);
            }
            catch (Exception)
            {
                // Skip malformed trailing entry
            }
        }

        return references;
    }

    public IEnumerable<Reference> ParseFile(string filePath)
    {
        // Encoding detection: check for BOM, try UTF-8, fall back to Latin-1
        string content;
        var rawBytes = File.ReadAllBytes(filePath);
        if (rawBytes.Length >= 3 && rawBytes[0] == 0xEF && rawBytes[1] == 0xBB && rawBytes[2] == 0xBF)
        {
            // UTF-8 BOM
            content = Encoding.UTF8.GetString(rawBytes);
        }
        else if (rawBytes.Length >= 2 && ((rawBytes[0] == 0xFF && rawBytes[1] == 0xFE) || (rawBytes[0] == 0xFE && rawBytes[1] == 0xFF)))
        {
            // UTF-16 BOM (LE or BE)
            content = Encoding.Unicode.GetString(rawBytes);
        }
        else
        {
            try
            {
                content = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                    .GetString(rawBytes);
            }
            catch (DecoderFallbackException)
            {
                // Not valid UTF-8, fall back to Latin-1
                content = Encoding.Latin1.GetString(rawBytes);
            }
        }

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
            Pages = GetPages(tags),
            Doi = GetFirstValue(tags, "DO"),
            Pmid = GetFirstValue(tags, "PM", "AN"),
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

    private static string? GetPages(Dictionary<string, List<string>> tags)
    {
        var pages = GetFirstValue(tags, "PG");
        if (!string.IsNullOrWhiteSpace(pages))
            return pages;

        var start = GetFirstValue(tags, "SP");
        var end = GetFirstValue(tags, "EP");
        return FormatPages(start, end);
    }
}
