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
        @"@(\w+)\s*[\{\(]\s*([^,]*),",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Matches field names; values are parsed with a small state machine to support nesting and concatenation.
    private static readonly Regex FieldNamePattern = new(
        @"([A-Za-z][A-Za-z0-9_:-]*)",
        RegexOptions.Compiled);

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
        var braceDepth = 0;
        var parenDepth = 0;
        var currentEntry = new StringBuilder();
        var inEntry = false;
        var seenOpening = false;

        foreach (var ch in content)
        {
            if (ch == '@' && !inEntry && braceDepth == 0 && parenDepth == 0)
            {
                inEntry = true;
                seenOpening = false;
            }

            if (inEntry)
            {
                currentEntry.Append(ch);
                if (ch == '{')
                {
                    braceDepth++;
                    seenOpening = true;
                }
                else if (ch == '}')
                    braceDepth = Math.Max(0, braceDepth - 1);
                else if (ch == '(')
                {
                    parenDepth++;
                    seenOpening = true;
                }
                else if (ch == ')')
                    parenDepth = Math.Max(0, parenDepth - 1);

                if (seenOpening && braceDepth == 0 && parenDepth == 0)
                {
                    entries.Add(currentEntry.ToString());
                    currentEntry.Clear();
                    inEntry = false;
                    seenOpening = false;
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

        var fields = ParseFields(entry);

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

    private static Dictionary<string, string> ParseFields(string entry)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var openIndex = entry.IndexOf('{');
        var openParenIndex = entry.IndexOf('(');
        var startIndex = openIndex >= 0 && (openParenIndex < 0 || openIndex < openParenIndex) ? openIndex : openParenIndex;
        if (startIndex < 0)
            return fields;

        // Skip entry key: find first comma after the opening brace/paren.
        var index = entry.IndexOf(',', startIndex);
        if (index < 0)
            return fields;
        index++;

        while (index < entry.Length)
        {
            index = SkipWhitespace(entry, index);
            if (index >= entry.Length)
                break;

            var ch = entry[index];
            if (ch == '}' || ch == ')')
                break;

            var nameMatch = FieldNamePattern.Match(entry, index);
            if (!nameMatch.Success || nameMatch.Index != index)
                break;

            var fieldName = nameMatch.Groups[1].Value;
            index = nameMatch.Index + nameMatch.Length;

            index = SkipWhitespace(entry, index);
            if (index >= entry.Length || entry[index] != '=')
                break;
            index++;

            var value = ParseValue(entry, ref index);
            if (!string.IsNullOrWhiteSpace(fieldName) && value != null)
                fields[fieldName] = CleanLatex(value);

            // Move past trailing comma if present.
            index = SkipWhitespace(entry, index);
            if (index < entry.Length && entry[index] == ',')
                index++;
        }

        return fields;
    }

    private static int SkipWhitespace(string input, int index)
    {
        while (index < input.Length && char.IsWhiteSpace(input[index]))
            index++;
        return index;
    }

    private static string? ParseValue(string input, ref int index)
    {
        index = SkipWhitespace(input, index);
        if (index >= input.Length)
            return null;

        var parts = new List<string>();
        while (index < input.Length)
        {
            index = SkipWhitespace(input, index);
            if (index >= input.Length)
                break;

            var ch = input[index];
            if (ch == '{')
            {
                parts.Add(ParseBraced(input, ref index));
            }
            else if (ch == '"')
            {
                parts.Add(ParseQuoted(input, ref index));
            }
            else
            {
                parts.Add(ParseBare(input, ref index));
            }

            index = SkipWhitespace(input, index);
            if (index < input.Length && input[index] == '#')
            {
                index++;
                continue;
            }
            break;
        }

        return parts.Count == 0 ? null : string.Concat(parts);
    }

    private static string ParseBraced(string input, ref int index)
    {
        var depth = 0;
        var start = index;
        while (index < input.Length)
        {
            var ch = input[index];
            if (ch == '{')
                depth++;
            else if (ch == '}')
            {
                depth--;
                if (depth == 0)
                {
                    var value = input.Substring(start + 1, index - start - 1);
                    index++;
                    return value;
                }
            }
            index++;
        }

        return input.Substring(start + 1);
    }

    private static string ParseQuoted(string input, ref int index)
    {
        var start = ++index;
        var escaped = false;
        while (index < input.Length)
        {
            var ch = input[index];
            if (!escaped && ch == '"')
            {
                var value = input.Substring(start, index - start);
                index++;
                return value;
            }
            escaped = !escaped && ch == '\\';
            index++;
        }

        return input.Substring(start);
    }

    private static string ParseBare(string input, ref int index)
    {
        var start = index;
        while (index < input.Length)
        {
            var ch = input[index];
            if (ch == ',' || ch == '}' || ch == ')' || ch == '#')
                break;
            index++;
        }

        return input.Substring(start, index - start).Trim();
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
        return authors.Split(new[] { " and ", " AND ", "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries)
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

        // Common escaped symbols
        result = result.Replace(@"\&", "&");
        result = result.Replace(@"\%", "%");

        // Remove remaining braces (used for case preservation)
        result = result.Replace("{", "").Replace("}", "");

        // Clean up multiple spaces
        result = Regex.Replace(result, @"\s+", " ");

        return result.Trim();
    }
}
