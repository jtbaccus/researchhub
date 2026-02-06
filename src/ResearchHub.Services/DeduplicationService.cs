using System.Text;
using ResearchHub.Core.Models;
using ResearchHub.Data.Repositories;

namespace ResearchHub.Services;

public class DeduplicationService : IDeduplicationService
{
    private readonly IReferenceRepository _referenceRepository;

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "of", "in", "on", "at", "to", "for", "and", "or", "but",
        "is", "are", "was", "were", "with", "by", "from", "into", "using", "via"
    };

    public DeduplicationService(IReferenceRepository referenceRepository)
    {
        _referenceRepository = referenceRepository;
    }

    public async Task<IReadOnlyList<DuplicateMatch>> FindPotentialDuplicatesAsync(
        int projectId,
        DeduplicationOptions? options = null)
    {
        options ??= new DeduplicationOptions();

        var references = (await _referenceRepository.GetByProjectIdAsync(projectId)).ToList();
        var matches = new Dictionary<(int, int), DuplicateMatch>();

        AddIdentifierMatches(references, matches, r => NormalizeDoi(r.Doi), DuplicateReason.Doi);
        AddIdentifierMatches(references, matches, r => NormalizePmid(r.Pmid), DuplicateReason.Pmid);

        AddTitleYearMatches(references, matches, options);

        return matches.Values
            .OrderBy(match => match.Primary.Id)
            .ThenBy(match => match.Duplicate.Id)
            .ToList();
    }

    private static void AddIdentifierMatches(
        IReadOnlyList<Reference> references,
        Dictionary<(int, int), DuplicateMatch> matches,
        Func<Reference, string?> normalizer,
        DuplicateReason reason)
    {
        var groups = references
            .Select(reference => new { Reference = reference, Value = normalizer(reference) })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Value))
            .GroupBy(entry => entry.Value!);

        foreach (var group in groups)
        {
            var grouped = group.Select(entry => entry.Reference).ToList();
            if (grouped.Count < 2)
                continue;

            for (var i = 0; i < grouped.Count; i++)
            {
                for (var j = i + 1; j < grouped.Count; j++)
                {
                    AddMatch(matches, grouped[i], grouped[j], reason, null);
                }
            }
        }
    }

    private static void AddTitleYearMatches(
        IReadOnlyList<Reference> references,
        Dictionary<(int, int), DuplicateMatch> matches,
        DeduplicationOptions options)
    {
        var signatures = references
            .Where(reference => !string.IsNullOrWhiteSpace(reference.Title))
            .Select(BuildTitleSignature)
            .Where(signature => !string.IsNullOrWhiteSpace(signature.NormalizedTitle))
            .ToList();

        IEnumerable<IGrouping<int, TitleSignature>> groups;

        if (options.RequireYearMatch)
        {
            groups = signatures
                .Where(signature => signature.Reference.Year.HasValue)
                .GroupBy(signature => signature.Reference.Year!.Value);
        }
        else
        {
            groups = signatures.GroupBy(signature => signature.Reference.Year ?? -1);
        }

        foreach (var group in groups)
        {
            var items = group.ToList();
            for (var i = 0; i < items.Count; i++)
            {
                for (var j = i + 1; j < items.Count; j++)
                {
                    var left = items[i];
                    var right = items[j];

                    if (!IsYearMatch(left.Reference.Year, right.Reference.Year, options))
                        continue;

                    var similarity = ComputeTitleSimilarity(left, right);
                    if (similarity < options.TitleSimilarityThreshold)
                        continue;

                    AddMatch(matches, left.Reference, right.Reference, DuplicateReason.TitleYear, similarity);
                }
            }
        }
    }

    private static void AddMatch(
        Dictionary<(int, int), DuplicateMatch> matches,
        Reference first,
        Reference second,
        DuplicateReason reason,
        double? similarity)
    {
        var primary = first.Id <= second.Id ? first : second;
        var duplicate = first.Id <= second.Id ? second : first;
        var key = (primary.Id, duplicate.Id);

        if (!matches.TryGetValue(key, out var match))
        {
            match = new DuplicateMatch
            {
                Primary = primary,
                Duplicate = duplicate
            };
            matches[key] = match;
        }

        match.Reasons.Add(reason);

        if (similarity.HasValue)
        {
            match.TitleSimilarity = match.TitleSimilarity.HasValue
                ? Math.Max(match.TitleSimilarity.Value, similarity.Value)
                : similarity.Value;
        }
    }

    private static string? NormalizeDoi(string? doi)
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
            .Trim();

        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? NormalizePmid(string? pmid)
    {
        if (string.IsNullOrWhiteSpace(pmid))
            return null;

        var digits = new string(pmid.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? null : digits;
    }

    private static bool IsYearMatch(int? left, int? right, DeduplicationOptions options)
    {
        if (left.HasValue && right.HasValue)
            return Math.Abs(left.Value - right.Value) <= options.YearTolerance;

        return !options.RequireYearMatch;
    }

    private static double ComputeTitleSimilarity(TitleSignature left, TitleSignature right)
    {
        var jaccard = Jaccard(left.Tokens, right.Tokens);
        var dice = DiceCoefficient(left.CompactTitle, right.CompactTitle);
        return (jaccard * 0.6) + (dice * 0.4);
    }

    private static double Jaccard(HashSet<string> left, HashSet<string> right)
    {
        if (left.Count == 0 && right.Count == 0)
            return 0d;

        var intersection = left.Intersect(right).Count();
        var union = left.Count + right.Count - intersection;
        return union == 0 ? 0d : (double)intersection / union;
    }

    private static double DiceCoefficient(string left, string right)
    {
        if (left.Length < 2 || right.Length < 2)
            return 0d;

        var leftBigrams = BuildBigrams(left);
        var rightBigrams = BuildBigrams(right);

        if (leftBigrams.Count == 0 || rightBigrams.Count == 0)
            return 0d;

        var intersection = leftBigrams.Intersect(rightBigrams).Count();
        return (2d * intersection) / (leftBigrams.Count + rightBigrams.Count);
    }

    private static HashSet<string> BuildBigrams(string value)
    {
        var bigrams = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < value.Length - 1; i++)
        {
            bigrams.Add(value.Substring(i, 2));
        }
        return bigrams;
    }

    private static TitleSignature BuildTitleSignature(Reference reference)
    {
        var title = reference.Title ?? string.Empty;
        var normalized = new StringBuilder(title.Length);
        var lastWasSpace = false;

        foreach (var ch in title.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                normalized.Append(ch);
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                normalized.Append(' ');
                lastWasSpace = true;
            }
        }

        var normalizedTitle = normalized.ToString().Trim();
        var tokens = normalizedTitle
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length > 2)
            .Where(token => !StopWords.Contains(token))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var compact = string.Concat(normalizedTitle.Where(char.IsLetterOrDigit));

        return new TitleSignature(reference, title, normalizedTitle, compact, tokens);
    }

    private sealed record TitleSignature(Reference Reference, string OriginalTitle, string NormalizedTitle, string CompactTitle, HashSet<string> Tokens);
}
