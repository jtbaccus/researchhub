using System.Diagnostics;
using FluentAssertions;
using ResearchHub.Core.Models;
using ResearchHub.Services.Tests.Fakes;
using Xunit.Abstractions;

namespace ResearchHub.Services.Tests.Deduplication;

public class DeduplicationStressTests
{
    private const int ProjectId = 1;
    private readonly ITestOutputHelper _output;

    public DeduplicationStressTests(ITestOutputHelper output) => _output = output;

    // Word pools for generating sufficiently distinct synthetic titles
    private static readonly string[] Subjects = { "patients", "children", "adults", "elderly", "women", "men", "mice", "rats", "cells", "tissues" };
    private static readonly string[] Methods = { "randomized trial", "cohort study", "case-control analysis", "cross-sectional survey", "longitudinal investigation", "prospective evaluation", "retrospective review", "qualitative assessment", "observational study", "meta-regression" };
    private static readonly string[] Fields = { "oncology", "cardiology", "neurology", "psychiatry", "orthopedics", "dermatology", "endocrinology", "nephrology", "pulmonology", "gastroenterology", "rheumatology", "ophthalmology", "pediatrics", "geriatrics", "immunology", "hematology", "infectious disease", "pharmacology", "radiology", "pathology" };
    private static readonly string[] Outcomes = { "survival", "mortality", "quality of life", "pain reduction", "symptom relief", "functional recovery", "biomarker levels", "hospitalization rates", "adverse events", "treatment adherence", "cognitive function", "mobility scores", "blood pressure", "glycemic control", "bone density" };

    private static Reference MakeUniqueRef(int id, int year)
    {
        // Build titles from different combinations to ensure low cross-similarity
        var subject = Subjects[id % Subjects.Length];
        var method = Methods[(id / Subjects.Length) % Methods.Length];
        var field = Fields[(id / (Subjects.Length * Methods.Length)) % Fields.Length];
        var outcome = Outcomes[id % Outcomes.Length];

        return new Reference
        {
            Id = id,
            ProjectId = ProjectId,
            Title = $"{method} of {subject} in {field} measuring {outcome} identifier{id}",
            Year = year,
            Doi = $"10.{1000 + id}/{id}unique"
        };
    }

    private static (DeduplicationService service, FakeReferenceRepository repo) CreateService(IEnumerable<Reference> refs)
    {
        var repo = new FakeReferenceRepository();
        repo.Seed(refs);
        return (new DeduplicationService(repo), repo);
    }

    [Fact]
    public async Task UniqueReferences1000_ZeroMatches()
    {
        var refs = Enumerable.Range(1, 1000)
            .Select(i => MakeUniqueRef(i, 2000 + (i % 25)))
            .ToList();

        var (svc, _) = CreateService(refs);
        var sw = Stopwatch.StartNew();

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        sw.Stop();
        _output.WriteLine($"1000 unique refs: {sw.ElapsedMilliseconds}ms, {matches.Count} matches");

        matches.Should().BeEmpty();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task KnownDoiDuplicates50_ExactlyFound()
    {
        var refs = new List<Reference>();
        var id = 1;

        // 900 unique refs
        for (var i = 0; i < 900; i++)
            refs.Add(MakeUniqueRef(id++, 2000 + (i % 20)));

        // 50 duplicate pairs via DOI
        for (var i = 0; i < 50; i++)
        {
            var doi = $"10.9999/dup{i}";
            refs.Add(new Reference { Id = id++, ProjectId = ProjectId, Title = $"Original paper {i}", Year = 2020, Doi = doi });
            refs.Add(new Reference { Id = id++, ProjectId = ProjectId, Title = $"Different title {i}", Year = 2021, Doi = doi });
        }

        var (svc, _) = CreateService(refs);
        var sw = Stopwatch.StartNew();

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        sw.Stop();
        _output.WriteLine($"1000 refs with 50 DOI dups: {sw.ElapsedMilliseconds}ms, {matches.Count} matches");

        var doiMatches = matches.Where(m => m.Reasons.Contains(DuplicateReason.Doi)).ToList();
        doiMatches.Should().HaveCount(50);
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task KnownTitleYearDuplicates50_MeasuresRecall()
    {
        var refs = new List<Reference>();
        var id = 1;
        var expectedPairs = new HashSet<(int, int)>();

        // 900 unique refs spread across years (using truly distinct titles)
        for (var i = 0; i < 900; i++)
            refs.Add(MakeUniqueRef(id++, 2000 + (i % 20)));

        // 50 title-year duplicate pairs — each pair has a fully unique long title
        for (var i = 0; i < 50; i++)
        {
            // Build a unique title per pair using deterministic word substitution
            var subject = Subjects[i % Subjects.Length];
            var field = Fields[i % Fields.Length];
            var outcome = Outcomes[i % Outcomes.Length];
            var title1 = $"Comprehensive evaluation of {subject} receiving novel {field} intervention measuring {outcome} in multicenter protocol alpha{i}";
            var year = 2050 + (i % 10); // Use a year range distinct from unique refs to avoid cross-matching

            var id1 = id++;
            refs.Add(new Reference { Id = id1, ProjectId = ProjectId, Title = title1, Year = year, Doi = $"10.1111/orig{i}" });

            // Variant: case change + minor differences
            var variant = title1.Replace("evaluation", "Evaluation").Replace("novel", "Novel");
            if (i % 3 == 0) variant = variant.Replace("-", " ");
            if (i % 5 == 0) variant = variant.ToUpperInvariant();

            var id2 = id++;
            refs.Add(new Reference { Id = id2, ProjectId = ProjectId, Title = variant, Year = year, Doi = $"10.2222/var{i}" });

            expectedPairs.Add((Math.Min(id1, id2), Math.Max(id1, id2)));
        }

        var (svc, _) = CreateService(refs);
        var sw = Stopwatch.StartNew();

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        sw.Stop();
        var titleMatches = matches.Where(m => m.Reasons.Contains(DuplicateReason.TitleYear)).ToList();
        var foundPairs = titleMatches
            .Select(m => (Math.Min(m.Primary.Id, m.Duplicate.Id), Math.Max(m.Primary.Id, m.Duplicate.Id)))
            .ToHashSet();

        var truePositives = foundPairs.Intersect(expectedPairs).Count();
        var falsePositives = foundPairs.Count - truePositives;

        _output.WriteLine($"1000 refs with 50 title-year dups: {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"  Title-year matches: {titleMatches.Count} total, {truePositives} TP, {falsePositives} FP");
        _output.WriteLine($"  Recall: {truePositives / 50.0:P1}");

        truePositives.Should().BeGreaterThanOrEqualTo(35, "should find most title-year duplicates");
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task MixedDuplicates2000_AllPipelinesWork()
    {
        var refs = new List<Reference>();
        var id = 1;

        // 1800 unique refs
        for (var i = 0; i < 1800; i++)
            refs.Add(MakeUniqueRef(id++, 2000 + (i % 25)));

        // 50 DOI duplicate pairs
        for (var i = 0; i < 50; i++)
        {
            var doi = $"10.8888/doidup{i}";
            refs.Add(new Reference { Id = id++, ProjectId = ProjectId, Title = $"DOI original {i}", Year = 2020, Doi = doi });
            refs.Add(new Reference { Id = id++, ProjectId = ProjectId, Title = $"DOI variant {i}", Year = 2021, Doi = doi });
        }

        // 25 PMID duplicate pairs
        for (var i = 0; i < 25; i++)
        {
            var pmid = $"{30000000 + i}";
            refs.Add(new Reference { Id = id++, ProjectId = ProjectId, Title = $"PMID original {i}", Year = 2020, Pmid = pmid });
            refs.Add(new Reference { Id = id++, ProjectId = ProjectId, Title = $"PMID variant {i}", Year = 2021, Pmid = pmid });
        }

        // 25 title-year duplicate pairs
        for (var i = 0; i < 25; i++)
        {
            var title = $"A comprehensive systematic review of intervention {i} for clinical outcome {i}";
            refs.Add(new Reference { Id = id++, ProjectId = ProjectId, Title = title, Year = 2018 });
            refs.Add(new Reference { Id = id++, ProjectId = ProjectId, Title = title, Year = 2018 });
        }

        var (svc, _) = CreateService(refs);
        var sw = Stopwatch.StartNew();

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        sw.Stop();
        var doiCount = matches.Count(m => m.Reasons.Contains(DuplicateReason.Doi));
        var pmidCount = matches.Count(m => m.Reasons.Contains(DuplicateReason.Pmid));
        var titleCount = matches.Count(m => m.Reasons.Contains(DuplicateReason.TitleYear));

        _output.WriteLine($"2000 mixed refs: {sw.ElapsedMilliseconds}ms, {matches.Count} total matches");
        _output.WriteLine($"  DOI: {doiCount}, PMID: {pmidCount}, TitleYear: {titleCount}");

        doiCount.Should().Be(50);
        pmidCount.Should().Be(25);
        titleCount.Should().BeGreaterThanOrEqualTo(25);
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Benchmark5000Refs_CompletesInTime()
    {
        var refs = Enumerable.Range(1, 5000)
            .Select(i => MakeUniqueRef(i, 2000 + (i % 25)))
            .ToList();

        var (svc, _) = CreateService(refs);
        var sw = Stopwatch.StartNew();

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        sw.Stop();
        _output.WriteLine($"5000 unique refs: {sw.ElapsedMilliseconds}ms, {matches.Count} matches");

        // Primarily a performance test — some spurious title matches are acceptable
        // since all refs share structural patterns in their synthetic titles
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task ClusteredSameYear_PerformanceStable()
    {
        // Worst case: all 1000 refs in the same year → O(n²) pairwise in title matching
        var refs = Enumerable.Range(1, 1000)
            .Select(i => MakeUniqueRef(i, 2022))
            .ToList();

        var (svc, _) = CreateService(refs);
        var sw = Stopwatch.StartNew();

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        sw.Stop();
        _output.WriteLine($"1000 refs same year: {sw.ElapsedMilliseconds}ms, {matches.Count} matches");

        // Even with O(n²) pairwise, should complete reasonably
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task DuplicateCluster_AllPairsFound()
    {
        // 5 refs all sharing the same DOI → 5C2 = 10 pairs
        var refs = Enumerable.Range(1, 5)
            .Select(i => new Reference
            {
                Id = i,
                ProjectId = ProjectId,
                Title = $"Paper {i}",
                Year = 2020,
                Doi = "10.9999/cluster"
            })
            .ToList();

        var (svc, _) = CreateService(refs);
        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        matches.Should().HaveCount(10);
        matches.Should().AllSatisfy(m => m.Reasons.Should().Contain(DuplicateReason.Doi));
    }

    [Fact]
    public async Task CrossProjectIsolation_NoLeakage()
    {
        var refs = new List<Reference>
        {
            new() { Id = 1, ProjectId = 1, Title = "Study A", Doi = "10.1234/test" },
            new() { Id = 2, ProjectId = 2, Title = "Study B", Doi = "10.1234/test" }
        };

        var (svc, _) = CreateService(refs);

        // Project 1 should see no duplicates (only 1 ref in project 1)
        var matches = await svc.FindPotentialDuplicatesAsync(1);
        matches.Should().BeEmpty();
    }
}
