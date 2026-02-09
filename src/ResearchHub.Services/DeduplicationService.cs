using System.Globalization;
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

    private static readonly Dictionary<string, string> BritishToAmerican = new(StringComparer.OrdinalIgnoreCase)
    {
        // -our → -or
        { "behavioural", "behavioral" }, { "behaviour", "behavior" },
        { "colour", "color" }, { "tumour", "tumor" }, { "favour", "favor" },
        { "honour", "honor" }, { "labour", "labor" }, { "neighbourhood", "neighborhood" },
        // -ise → -ize
        { "randomised", "randomized" }, { "standardised", "standardized" },
        { "characterised", "characterized" }, { "recognised", "recognized" },
        { "organised", "organized" }, { "specialised", "specialized" },
        { "utilised", "utilized" }, { "minimised", "minimized" },
        { "optimised", "optimized" }, { "summarised", "summarized" },
        { "analysed", "analyzed" },
        // -re → -er
        { "centre", "center" }, { "fibre", "fiber" }, { "litre", "liter" },
        // -ae/oe → e
        { "anaemia", "anemia" }, { "anaesthesia", "anesthesia" },
        { "oedema", "edema" }, { "oesophagus", "esophagus" },
        { "haemorrhage", "hemorrhage" }, { "haemoglobin", "hemoglobin" },
        { "leukaemia", "leukemia" }, { "paediatric", "pediatric" },
        { "gynaecology", "gynecology" }, { "orthopaedic", "orthopedic" },
        { "foetal", "fetal" }, { "foetus", "fetus" }, { "diarrhoea", "diarrhea" },
        // double-l → single-l
        { "modelling", "modeling" }, { "labelling", "labeling" },
        { "counselling", "counseling" }, { "signalling", "signaling" },
        { "travelling", "traveling" }, { "cancelling", "canceling" },
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
            .Select(r => BuildTitleSignature(r, options.NormalizeSpelling))
            .Where(signature => !string.IsNullOrWhiteSpace(signature.NormalizedTitle))
            .ToList();

        if (!options.RequireYearMatch)
        {
            // All signatures in one group — compare every pair
            var items = signatures;
            ComparePairs(items, items, 0, matches, options);
            return;
        }

        // Group by year (exclude null years when RequireYearMatch is true)
        var yearGroups = new Dictionary<int, List<TitleSignature>>();
        foreach (var sig in signatures)
        {
            if (!sig.Reference.Year.HasValue)
                continue;
            var year = sig.Reference.Year.Value;
            if (!yearGroups.TryGetValue(year, out var list))
            {
                list = new List<TitleSignature>();
                yearGroups[year] = list;
            }
            list.Add(sig);
        }

        var years = yearGroups.Keys.OrderBy(y => y).ToList();

        // Within-group comparisons
        foreach (var year in years)
        {
            var items = yearGroups[year];
            ComparePairs(items, items, 0, matches, options);
        }

        // Cross-group comparisons for YearTolerance > 0
        if (options.YearTolerance > 0)
        {
            foreach (var year in years)
            {
                for (var d = 1; d <= options.YearTolerance; d++)
                {
                    if (yearGroups.TryGetValue(year + d, out var adjacentItems))
                    {
                        ComparePairs(yearGroups[year], adjacentItems, -1, matches, options);
                    }
                }
            }
        }
    }

    private static void ComparePairs(
        List<TitleSignature> leftGroup,
        List<TitleSignature> rightGroup,
        int startJ,
        Dictionary<(int, int), DuplicateMatch> matches,
        DeduplicationOptions options)
    {
        // startJ: 0 means same-group (use i+1 to avoid self-pairs), -1 means cross-group (compare all)
        var crossGroup = startJ < 0;

        for (var i = 0; i < leftGroup.Count; i++)
        {
            var jStart = crossGroup ? 0 : i + 1;
            var jList = crossGroup ? rightGroup : leftGroup;

            for (var j = jStart; j < jList.Count; j++)
            {
                var left = leftGroup[i];
                var right = jList[j];

                if (!IsYearMatch(left.Reference.Year, right.Reference.Year, options))
                    continue;

                var similarity = ComputeTitleSimilarity(left, right);
                if (similarity < options.TitleSimilarityThreshold)
                    continue;

                AddMatch(matches, left.Reference, right.Reference, DuplicateReason.TitleYear, similarity);
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
            .Trim()
            .TrimEnd('.', ',');

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
        var fullSimilarity = (jaccard * 0.6) + (dice * 0.4);

        // Subtitle-aware secondary check: if either title has a colon/dash separator,
        // compare only the pre-separator portions. If that similarity is very high (>0.95),
        // use it to catch duplicates where one database truncated the subtitle.
        if (left.PreColonTokens != null && right.PreColonTokens != null
            && left.PreColonCompact != null && right.PreColonCompact != null)
        {
            var preJaccard = Jaccard(left.PreColonTokens, right.PreColonTokens);
            var preDice = DiceCoefficient(left.PreColonCompact, right.PreColonCompact);
            var preSimilarity = (preJaccard * 0.6) + (preDice * 0.4);
            if (preSimilarity > 0.95)
                return Math.Max(fullSimilarity, preSimilarity);
        }
        // Also handle: one title has the subtitle, the other doesn't (truncated).
        // Compare the full short title against the pre-colon portion of the long title.
        else if (left.PreColonTokens != null && left.PreColonCompact != null
                 && right.PreColonTokens == null)
        {
            var preJaccard = Jaccard(left.PreColonTokens, right.Tokens);
            var preDice = DiceCoefficient(left.PreColonCompact, right.CompactTitle);
            var preSimilarity = (preJaccard * 0.6) + (preDice * 0.4);
            if (preSimilarity > 0.95)
                return Math.Max(fullSimilarity, preSimilarity);
        }
        else if (right.PreColonTokens != null && right.PreColonCompact != null
                 && left.PreColonTokens == null)
        {
            var preJaccard = Jaccard(right.PreColonTokens, left.Tokens);
            var preDice = DiceCoefficient(right.PreColonCompact, left.CompactTitle);
            var preSimilarity = (preJaccard * 0.6) + (preDice * 0.4);
            if (preSimilarity > 0.95)
                return Math.Max(fullSimilarity, preSimilarity);
        }

        return fullSimilarity;
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

    private static TitleSignature BuildTitleSignature(Reference reference, bool normalizeSpelling)
    {
        var title = reference.Title ?? string.Empty;

        // Decompose accented characters (é → e + combining accent) then strip combining marks
        var decomposed = title.Normalize(NormalizationForm.FormD);
        var stripped = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                stripped.Append(ch);
        }
        title = stripped.ToString();

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

        // Normalize British → American spelling variants
        var words = normalizedTitle.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (normalizeSpelling)
        {
            for (var i = 0; i < words.Length; i++)
            {
                if (BritishToAmerican.TryGetValue(words[i], out var american))
                    words[i] = american;
            }
        }
        normalizedTitle = string.Join(' ', words);

        var tokens = words
            .Where(token => token.Length > 2)
            .Where(token => !StopWords.Contains(token))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var compact = string.Concat(normalizedTitle.Where(char.IsLetterOrDigit));

        // Pre-colon data for subtitle-aware matching
        string? preColonCompact = null;
        HashSet<string>? preColonTokens = null;
        var colonIdx = normalizedTitle.IndexOf(':');
        if (colonIdx < 0) colonIdx = normalizedTitle.IndexOf(" - ");
        if (colonIdx > 10)
        {
            var preColon = normalizedTitle[..colonIdx].Trim();
            var preColonWords = preColon.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            preColonCompact = string.Concat(preColon.Where(char.IsLetterOrDigit));
            preColonTokens = preColonWords
                .Where(token => token.Length > 2)
                .Where(token => !StopWords.Contains(token))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        return new TitleSignature(reference, title, normalizedTitle, compact, tokens, preColonCompact, preColonTokens);
    }

    private sealed record TitleSignature(Reference Reference, string OriginalTitle, string NormalizedTitle, string CompactTitle, HashSet<string> Tokens, string? PreColonCompact, HashSet<string>? PreColonTokens);
}
