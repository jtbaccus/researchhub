using FluentAssertions;
using ResearchHub.Core.Models;
using ResearchHub.Services.Tests.Fakes;
using Xunit.Abstractions;

namespace ResearchHub.Services.Tests.Deduplication;

public class ThresholdTuningTests
{
    private const int ProjectId = 1;
    private readonly ITestOutputHelper _output;
    private static readonly Random Rng = new(42); // Fixed seed for reproducibility

    public ThresholdTuningTests(ITestOutputHelper output) => _output = output;

    // --- Title variant generator ---

    private static readonly Dictionary<string, string> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["randomized"] = "RCT",
        ["randomised"] = "RCT",
        ["systematic review"] = "SR",
        ["meta-analysis"] = "MA",
        ["controlled trial"] = "CT",
        ["versus"] = "vs",
        ["compared with"] = "vs",
        ["magnetic resonance imaging"] = "MRI",
        ["computed tomography"] = "CT scan",
        ["quality of life"] = "QOL",
        ["body mass index"] = "BMI",
    };

    private static string RemoveArticles(string title)
    {
        var words = title.Split(' ');
        var articles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a", "an", "the" };
        var filtered = words.Where(w => !articles.Contains(w));
        return string.Join(' ', filtered);
    }

    private static string AddLeadingArticle(string title)
    {
        return $"A {title}";
    }

    private static string ApplyAbbreviation(string title)
    {
        foreach (var (full, abbr) in Abbreviations)
        {
            if (title.Contains(full, StringComparison.OrdinalIgnoreCase))
            {
                var idx = title.IndexOf(full, StringComparison.OrdinalIgnoreCase);
                return string.Concat(title.AsSpan(0, idx), abbr, title.AsSpan(idx + full.Length));
            }
        }
        return title;
    }

    private static string SwapAdjacentChars(string title)
    {
        if (title.Length < 10) return title;
        var chars = title.ToCharArray();
        // Swap two adjacent chars near the middle
        var pos = title.Length / 2;
        (chars[pos], chars[pos + 1]) = (chars[pos + 1], chars[pos]);
        return new string(chars);
    }

    private static string RemoveSubtitle(string title)
    {
        var colonIdx = title.IndexOf(':');
        return colonIdx > 10 ? title[..colonIdx].Trim() : title;
    }

    private static string ChangePunctuation(string title)
    {
        var result = title
            .Replace("vs.", "versus")
            .Replace(" - ", ": ")
            .Replace("–", "-")
            .Replace("'", "'");

        // If nothing changed, apply a generic punctuation transformation
        if (result == title)
            result = title.Replace(":", " -");

        return result;
    }

    private static string MakeTitleVariant(string baseTitle, int variantIndex)
    {
        // Apply 1-3 transformations based on variant index
        var result = baseTitle;
        var transforms = new Func<string, string>[]
        {
            RemoveArticles,
            AddLeadingArticle,
            ApplyAbbreviation,
            SwapAdjacentChars,
            RemoveSubtitle,
            ChangePunctuation,
        };

        // Always apply at least one transform
        result = transforms[variantIndex % transforms.Length](result);

        // Sometimes apply a second
        if (variantIndex % 3 == 0)
            result = transforms[(variantIndex + 2) % transforms.Length](result);

        return result;
    }

    // --- Corpus of realistic base titles ---

    private static readonly string[] BaseTitles =
    {
        "Effectiveness of cognitive behavioral therapy for the treatment of major depressive disorder: A systematic review",
        "A randomized controlled trial of mindfulness-based stress reduction versus active control",
        "Long-term outcomes of bariatric surgery compared with nonsurgical treatment for obesity",
        "Association between physical activity and all-cause mortality in older adults: A meta-analysis",
        "Statin therapy for the primary prevention of cardiovascular disease: A systematic review and meta-analysis",
        "Effect of vitamin D supplementation on bone mineral density in postmenopausal women",
        "Machine learning approaches for predicting response to immunotherapy in non-small cell lung cancer",
        "The impact of sleep deprivation on cognitive performance and emotional regulation",
        "Efficacy and safety of novel oral anticoagulants versus warfarin for atrial fibrillation",
        "A systematic review of interventions to reduce hospital readmissions in heart failure patients",
        "Comparative effectiveness of antihypertensive drug classes for blood pressure control",
        "Neural correlates of decision making under uncertainty: A functional magnetic resonance imaging study",
        "The role of gut microbiome in inflammatory bowel disease pathogenesis and treatment",
        "Effects of Mediterranean diet on cardiovascular risk factors: A randomized controlled trial",
        "Prevalence and risk factors for post-traumatic stress disorder in emergency medical workers",
        "Gene therapy approaches for inherited retinal dystrophies: A systematic review",
        "Impact of telehealth interventions on chronic disease management during the pandemic",
        "Biomarkers for early detection of Alzheimer disease: A comprehensive review",
        "Surgical versus conservative management of rotator cuff tears: A meta-analysis",
        "Social determinants of health and their impact on cancer screening disparities",
    };

    // Word pools for generating unique non-duplicate titles with minimal overlap
    private static readonly string[] NdAdjectives = { "novel", "emerging", "advanced", "preliminary", "comprehensive", "innovative", "retrospective", "prospective", "multicenter", "international" };
    private static readonly string[] NdTopics = { "nanotechnology", "CRISPR gene editing", "blockchain health records", "quantum biosensors", "optogenetics", "organoid cultures", "microfluidics", "radiomics", "metabolomics", "proteomics", "epigenetics", "exosome therapies", "chimeric antigen receptor", "clustered amplification", "holographic microscopy", "cryo-electron tomography", "spatial transcriptomics", "single cell sequencing", "adaptive optics", "photoacoustic imaging" };
    private static readonly string[] NdContexts = { "zebrafish models", "Drosophila larvae", "Antarctic ecosystems", "Martian soil analogues", "deep ocean vents", "permafrost samples", "volcanic sediments", "coral reef biomes", "alpine glaciers", "tropical canopy layers" };

    [Fact]
    public async Task ThresholdSweep_ComputesPrecisionRecallF1()
    {
        var refs = new List<Reference>();
        var trueDuplicatePairs = new HashSet<(int, int)>();
        var id = 1;

        // Generate 200 true duplicate pairs, each with a unique distinguishing suffix
        for (var i = 0; i < 200; i++)
        {
            var baseTitle = BaseTitles[i % BaseTitles.Length];
            // Append a unique identifier so pairs from the same base title don't cross-match
            var title1 = $"{baseTitle} protocol gamma{i} site{i * 3 + 7}";
            var title2 = MakeTitleVariant(title1, i);
            // Spread across many unique years to minimize same-year grouping across pairs
            var year = 3000 + i;

            var id1 = id++;
            var id2 = id++;

            refs.Add(new Reference { Id = id1, ProjectId = ProjectId, Title = title1, Year = year });
            refs.Add(new Reference { Id = id2, ProjectId = ProjectId, Title = title2, Year = year });

            var key = id1 < id2 ? (id1, id2) : (id2, id1);
            trueDuplicatePairs.Add(key);
        }

        // Generate 200 true non-duplicate refs with fully unique titles
        // Use different year range (2080+) to prevent cross-matching with duplicate pairs
        for (var i = 0; i < 200; i++)
        {
            var adj = NdAdjectives[i % NdAdjectives.Length];
            var topic = NdTopics[i % NdTopics.Length];
            var context = NdContexts[i % NdContexts.Length];
            var title = $"{adj} applications of {topic} in {context} experiment{i}";
            var year = 2080 + (i % 8);

            refs.Add(new Reference { Id = id++, ProjectId = ProjectId, Title = title, Year = year });
        }

        var thresholds = new[] { 0.70, 0.75, 0.80, 0.82, 0.84, 0.86, 0.88, 0.90, 0.92, 0.95 };

        _output.WriteLine("Threshold | TP  | FP  | FN  | Precision | Recall | F1");
        _output.WriteLine("----------|-----|-----|-----|-----------|--------|------");

        double bestF1 = 0;
        double bestThreshold = 0;

        foreach (var threshold in thresholds)
        {
            var repo = new FakeReferenceRepository();
            repo.Seed(refs);
            var svc = new DeduplicationService(repo);

            var options = new DeduplicationOptions
            {
                TitleSimilarityThreshold = threshold,
                RequireYearMatch = true,
                YearTolerance = 0
            };

            var matches = await svc.FindPotentialDuplicatesAsync(ProjectId, options);
            var titleMatches = matches
                .Where(m => m.Reasons.Contains(DuplicateReason.TitleYear))
                .ToList();

            var foundPairs = titleMatches
                .Select(m => (Math.Min(m.Primary.Id, m.Duplicate.Id), Math.Max(m.Primary.Id, m.Duplicate.Id)))
                .ToHashSet();

            var tp = foundPairs.Intersect(trueDuplicatePairs).Count();
            var fp = foundPairs.Count - tp;
            var fn = trueDuplicatePairs.Count - tp;

            var precision = foundPairs.Count > 0 ? (double)tp / foundPairs.Count : 1.0;
            var recall = trueDuplicatePairs.Count > 0 ? (double)tp / trueDuplicatePairs.Count : 1.0;
            var f1 = (precision + recall) > 0 ? 2 * precision * recall / (precision + recall) : 0;

            _output.WriteLine($"  {threshold:F2}    | {tp,3} | {fp,3} | {fn,3} |   {precision:F3}   | {recall:F3}  | {f1:F3}");

            if (f1 > bestF1)
            {
                bestF1 = f1;
                bestThreshold = threshold;
            }
        }

        _output.WriteLine($"\nBest threshold: {bestThreshold:F2} (F1 = {bestF1:F3})");
        _output.WriteLine($"Current default: 0.88");

        // The sweep should produce meaningful results (high recall expected since
        // each pair is in its own unique year, eliminating cross-pair false positives)
        bestF1.Should().BeGreaterThan(0.5, "threshold sweep should achieve reasonable F1");
    }

    [Fact]
    public void VariantGenerator_ProducesSimilarButDistinctTitles()
    {
        var baseTitle = "Effectiveness of cognitive behavioral therapy for major depressive disorder: A systematic review";

        for (var i = 0; i < 6; i++)
        {
            var variant = MakeTitleVariant(baseTitle, i);
            _output.WriteLine($"Variant {i}: {variant}");
            variant.Should().NotBe(baseTitle, $"variant {i} should differ from original");
        }
    }

    [Fact]
    public async Task HighSimilarityPairs_AlwaysDetected()
    {
        // Pairs with only case/punctuation changes should always be found at 0.88
        var pairs = new[]
        {
            ("Cognitive behavioral therapy for depression", "cognitive behavioral therapy for depression"),
            ("A systematic review and meta-analysis of statin therapy", "Systematic review and meta-analysis of statin therapy"),
            ("Randomized controlled trial of Drug X compared with placebo treatment", "Randomized controlled trial of Drug X compared to placebo treatment"),
        };

        var refs = new List<Reference>();
        var id = 1;

        foreach (var (t1, t2) in pairs)
        {
            refs.Add(new Reference { Id = id++, ProjectId = ProjectId, Title = t1, Year = 2022 });
            refs.Add(new Reference { Id = id++, ProjectId = ProjectId, Title = t2, Year = 2022 });
        }

        var repo = new FakeReferenceRepository();
        repo.Seed(refs);
        var svc = new DeduplicationService(repo);

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);
        var titleMatches = matches.Where(m => m.Reasons.Contains(DuplicateReason.TitleYear)).ToList();

        _output.WriteLine($"High-similarity pairs found: {titleMatches.Count} / {pairs.Length}");
        foreach (var m in titleMatches)
            _output.WriteLine($"  [{m.Primary.Id},{m.Duplicate.Id}] sim={m.TitleSimilarity:F3}");

        titleMatches.Should().HaveCount(pairs.Length, "all high-similarity pairs should be detected");
    }

    [Fact]
    public async Task LowSimilarityPairs_NeverDetected()
    {
        // Completely different papers should never match at any reasonable threshold
        var refs = new List<Reference>
        {
            new() { Id = 1, ProjectId = ProjectId, Title = "Effectiveness of cognitive behavioral therapy for depression", Year = 2022 },
            new() { Id = 2, ProjectId = ProjectId, Title = "Machine learning algorithms for protein structure prediction", Year = 2022 },
            new() { Id = 3, ProjectId = ProjectId, Title = "Environmental impact of microplastics in marine ecosystems", Year = 2022 },
            new() { Id = 4, ProjectId = ProjectId, Title = "Quantum computing approaches to cryptographic security", Year = 2022 },
        };

        var repo = new FakeReferenceRepository();
        repo.Seed(refs);
        var svc = new DeduplicationService(repo);

        // Even at a very low threshold, these shouldn't match
        var options = new DeduplicationOptions { TitleSimilarityThreshold = 0.70 };
        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId, options);

        matches.Should().BeEmpty("completely different papers should never be flagged as duplicates");
    }
}
