using FluentAssertions;
using ResearchHub.Core.Models;
using ResearchHub.Services.Tests.Fakes;

namespace ResearchHub.Services.Tests.Deduplication;

public class DeduplicationServiceTests
{
    private const int ProjectId = 1;

    private static Reference MakeRef(int id, string title, int? year = null, string? doi = null, string? pmid = null)
    {
        return new Reference { Id = id, ProjectId = ProjectId, Title = title, Year = year, Doi = doi, Pmid = pmid };
    }

    private static (DeduplicationService service, FakeReferenceRepository repo) CreateService(params Reference[] refs)
    {
        var repo = new FakeReferenceRepository();
        repo.Seed(refs);
        return (new DeduplicationService(repo), repo);
    }

    // --- DOI matching ---

    [Fact]
    public async Task ExactDoiMatch_ReturnsDoiReason()
    {
        var (svc, _) = CreateService(
            MakeRef(1, "Study A", doi: "10.1234/test"),
            MakeRef(2, "Study B", doi: "10.1234/test"));

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        matches.Should().HaveCount(1);
        matches[0].Reasons.Should().Contain(DuplicateReason.Doi);
    }

    [Theory]
    [InlineData("doi:10.1234/test")]
    [InlineData("DOI:10.1234/test")]
    [InlineData("https://doi.org/10.1234/test")]
    [InlineData("http://doi.org/10.1234/test")]
    [InlineData("https://dx.doi.org/10.1234/test")]
    [InlineData("http://dx.doi.org/10.1234/test")]
    [InlineData("  10.1234/TEST  ")]
    public async Task DoiNormalization_MatchesVariants(string variantDoi)
    {
        var (svc, _) = CreateService(
            MakeRef(1, "Study A", doi: "10.1234/test"),
            MakeRef(2, "Study B", doi: variantDoi));

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        matches.Should().HaveCount(1);
        matches[0].Reasons.Should().Contain(DuplicateReason.Doi);
    }

    [Fact]
    public async Task DifferentDois_NoMatch()
    {
        var (svc, _) = CreateService(
            MakeRef(1, "Study A", doi: "10.1234/aaa"),
            MakeRef(2, "Study B", doi: "10.1234/bbb"));

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        matches.Should().BeEmpty();
    }

    [Fact]
    public async Task NullDois_NoMatch()
    {
        var (svc, _) = CreateService(
            MakeRef(1, "Study A", doi: null),
            MakeRef(2, "Study B", doi: null));

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        matches.Should().BeEmpty();
    }

    [Fact]
    public async Task EmptyDoi_NoMatch()
    {
        var (svc, _) = CreateService(
            MakeRef(1, "Study A", doi: ""),
            MakeRef(2, "Study B", doi: "  "));

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        matches.Should().BeEmpty();
    }

    // --- PMID matching ---

    [Fact]
    public async Task ExactPmidMatch_ReturnsPmidReason()
    {
        var (svc, _) = CreateService(
            MakeRef(1, "Study A", pmid: "12345678"),
            MakeRef(2, "Study B", pmid: "12345678"));

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        matches.Should().HaveCount(1);
        matches[0].Reasons.Should().Contain(DuplicateReason.Pmid);
    }

    [Theory]
    [InlineData("PMID: 12345678")]
    [InlineData("pmid:12345678")]
    [InlineData("  12345678  ")]
    public async Task PmidNormalization_MatchesVariants(string variantPmid)
    {
        var (svc, _) = CreateService(
            MakeRef(1, "Study A", pmid: "12345678"),
            MakeRef(2, "Study B", pmid: variantPmid));

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        matches.Should().HaveCount(1);
        matches[0].Reasons.Should().Contain(DuplicateReason.Pmid);
    }

    [Fact]
    public async Task NullPmids_NoMatch()
    {
        var (svc, _) = CreateService(
            MakeRef(1, "Study A", pmid: null),
            MakeRef(2, "Study B", pmid: null));

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        matches.Should().BeEmpty();
    }

    // --- Title + Year fuzzy matching ---

    [Fact]
    public async Task IdenticalTitleAndYear_ReturnsTitleYearReason()
    {
        var (svc, _) = CreateService(
            MakeRef(1, "Effectiveness of cognitive behavioral therapy for depression", year: 2022),
            MakeRef(2, "Effectiveness of cognitive behavioral therapy for depression", year: 2022));

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        matches.Should().HaveCount(1);
        matches[0].Reasons.Should().Contain(DuplicateReason.TitleYear);
        matches[0].TitleSimilarity.Should().BeApproximately(1.0, 0.01);
    }

    [Fact]
    public async Task SimilarTitleSameYear_Matches()
    {
        // Minor spelling variant: "behavioral" → "Behavioral" (case only) — should match easily
        var (svc, _) = CreateService(
            MakeRef(1, "Effectiveness of cognitive behavioral therapy for treatment of depression", year: 2022),
            MakeRef(2, "Effectiveness of Cognitive Behavioral Therapy for Treatment of Depression", year: 2022));

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        matches.Should().HaveCount(1);
        matches[0].Reasons.Should().Contain(DuplicateReason.TitleYear);
    }

    [Fact]
    public async Task BritishAmericanSpelling_NormalizedToMatch()
    {
        // "behavioral" vs "behavioural" — normalized to same American spelling before comparison
        var (svc, _) = CreateService(
            MakeRef(1, "Effectiveness of cognitive behavioral therapy for depression", year: 2022),
            MakeRef(2, "Effectiveness of cognitive behavioural therapy for depression", year: 2022));

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        matches.Should().HaveCount(1);
        matches[0].Reasons.Should().Contain(DuplicateReason.TitleYear);
    }

    [Fact]
    public async Task CompletelyDifferentTitles_NoMatch()
    {
        var (svc, _) = CreateService(
            MakeRef(1, "Effectiveness of cognitive behavioral therapy for depression", year: 2022),
            MakeRef(2, "Machine learning approaches to protein folding prediction", year: 2022));

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        matches.Should().BeEmpty();
    }

    [Fact]
    public async Task SameTitleDifferentYear_RequireYearMatch_NoMatch()
    {
        var (svc, _) = CreateService(
            MakeRef(1, "Effectiveness of cognitive behavioral therapy for depression", year: 2020),
            MakeRef(2, "Effectiveness of cognitive behavioral therapy for depression", year: 2022));

        var options = new DeduplicationOptions { RequireYearMatch = true, YearTolerance = 0 };
        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId, options);

        matches.Should().BeEmpty();
    }

    [Fact]
    public async Task SameTitleYearTolerance1_WithinSameYearGroup_Matches()
    {
        // YearTolerance is checked within year groups. Refs in the same year group
        // with identical titles always match regardless of tolerance setting.
        var (svc, _) = CreateService(
            MakeRef(1, "Effectiveness of cognitive behavioral therapy for depression", year: 2022),
            MakeRef(2, "Effectiveness of cognitive behavioral therapy for depression", year: 2022));

        var options = new DeduplicationOptions { RequireYearMatch = true, YearTolerance = 1 };
        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId, options);

        matches.Should().HaveCount(1);
    }

    [Fact]
    public async Task YearTolerance1_AcrossAdjacentYears_Matches()
    {
        // Cross-group comparison: years 2021 and 2022 are within tolerance=1
        var (svc, _) = CreateService(
            MakeRef(1, "Effectiveness of cognitive behavioral therapy for depression", year: 2021),
            MakeRef(2, "Effectiveness of cognitive behavioral therapy for depression", year: 2022));

        var options = new DeduplicationOptions { RequireYearMatch = true, YearTolerance = 1 };
        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId, options);

        matches.Should().HaveCount(1);
        matches[0].Reasons.Should().Contain(DuplicateReason.TitleYear);
    }

    [Fact]
    public async Task YearTolerance1_AcrossNonAdjacentYears_NoMatch()
    {
        // Years 2020 and 2022 differ by 2, exceeding tolerance=1
        var (svc, _) = CreateService(
            MakeRef(1, "Effectiveness of cognitive behavioral therapy for depression", year: 2020),
            MakeRef(2, "Effectiveness of cognitive behavioral therapy for depression", year: 2022));

        var options = new DeduplicationOptions { RequireYearMatch = true, YearTolerance = 1 };
        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId, options);

        matches.Should().BeEmpty();
    }

    [Fact]
    public async Task RequireYearMatchFalse_NullYearsStillMatch()
    {
        var (svc, _) = CreateService(
            MakeRef(1, "Effectiveness of cognitive behavioral therapy for depression", year: null),
            MakeRef(2, "Effectiveness of cognitive behavioral therapy for depression", year: null));

        var options = new DeduplicationOptions { RequireYearMatch = false };
        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId, options);

        matches.Should().HaveCount(1);
        matches[0].Reasons.Should().Contain(DuplicateReason.TitleYear);
    }

    [Fact]
    public async Task RequireYearMatchTrue_NullYearsExcluded()
    {
        var (svc, _) = CreateService(
            MakeRef(1, "Effectiveness of cognitive behavioral therapy for depression", year: null),
            MakeRef(2, "Effectiveness of cognitive behavioral therapy for depression", year: null));

        var options = new DeduplicationOptions { RequireYearMatch = true };
        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId, options);

        matches.Should().BeEmpty();
    }

    // --- Mixed reasons ---

    [Fact]
    public async Task DoiAndTitleYear_BothReasonsPresent()
    {
        var (svc, _) = CreateService(
            MakeRef(1, "Effectiveness of cognitive behavioral therapy for depression", year: 2022, doi: "10.1234/test"),
            MakeRef(2, "Effectiveness of cognitive behavioral therapy for depression", year: 2022, doi: "10.1234/test"));

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        matches.Should().HaveCount(1);
        matches[0].Reasons.Should().Contain(DuplicateReason.Doi);
        matches[0].Reasons.Should().Contain(DuplicateReason.TitleYear);
    }

    [Fact]
    public async Task DoiAndPmid_BothReasonsPresent()
    {
        var (svc, _) = CreateService(
            MakeRef(1, "Study A", doi: "10.1234/test", pmid: "12345678"),
            MakeRef(2, "Study B", doi: "10.1234/test", pmid: "12345678"));

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        matches.Should().HaveCount(1);
        matches[0].Reasons.Should().Contain(DuplicateReason.Doi);
        matches[0].Reasons.Should().Contain(DuplicateReason.Pmid);
    }

    [Fact]
    public async Task AllThreeReasons_AllPresent()
    {
        var (svc, _) = CreateService(
            MakeRef(1, "Effectiveness of cognitive behavioral therapy for depression", year: 2022, doi: "10.1234/test", pmid: "12345678"),
            MakeRef(2, "Effectiveness of cognitive behavioral therapy for depression", year: 2022, doi: "10.1234/test", pmid: "12345678"));

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        matches.Should().HaveCount(1);
        matches[0].Reasons.Should().HaveCount(3);
    }

    // --- Edge cases ---

    [Fact]
    public async Task SingleReference_NoMatches()
    {
        var (svc, _) = CreateService(
            MakeRef(1, "Study A", year: 2022, doi: "10.1234/test"));

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        matches.Should().BeEmpty();
    }

    [Fact]
    public async Task NoReferences_NoMatches()
    {
        var (svc, _) = CreateService();

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        matches.Should().BeEmpty();
    }

    [Fact]
    public async Task EmptyTitles_NoTitleYearMatch()
    {
        var (svc, _) = CreateService(
            MakeRef(1, "", year: 2022),
            MakeRef(2, "", year: 2022));

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        matches.Should().BeEmpty();
    }

    [Fact]
    public async Task PrimaryHasLowerId()
    {
        var (svc, _) = CreateService(
            MakeRef(5, "Study A", doi: "10.1234/test"),
            MakeRef(3, "Study B", doi: "10.1234/test"));

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        matches.Should().HaveCount(1);
        matches[0].Primary.Id.Should().Be(3);
        matches[0].Duplicate.Id.Should().Be(5);
    }

    [Fact]
    public async Task ThreeWayDoiDuplicate_ProducesThreePairs()
    {
        var (svc, _) = CreateService(
            MakeRef(1, "A", doi: "10.1234/test"),
            MakeRef(2, "B", doi: "10.1234/test"),
            MakeRef(3, "C", doi: "10.1234/test"));

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        // 3 choose 2 = 3 pairs
        matches.Should().HaveCount(3);
    }

    [Fact]
    public async Task TitleWithPunctuationDifferences_StillMatches()
    {
        var (svc, _) = CreateService(
            MakeRef(1, "Cognitive-Behavioral Therapy: A Systematic Review", year: 2022),
            MakeRef(2, "Cognitive Behavioral Therapy - A Systematic Review", year: 2022));

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        matches.Should().HaveCount(1);
        matches[0].Reasons.Should().Contain(DuplicateReason.TitleYear);
    }

    [Fact]
    public async Task CaseDifferences_StillMatches()
    {
        var (svc, _) = CreateService(
            MakeRef(1, "EFFECTIVENESS OF COGNITIVE BEHAVIORAL THERAPY", year: 2022),
            MakeRef(2, "effectiveness of cognitive behavioral therapy", year: 2022));

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        matches.Should().HaveCount(1);
    }

    [Fact]
    public async Task DefaultOptions_UsedWhenNull()
    {
        var (svc, _) = CreateService(
            MakeRef(1, "Effectiveness of cognitive behavioral therapy for depression", year: 2022),
            MakeRef(2, "Effectiveness of cognitive behavioral therapy for depression", year: 2022));

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId, null);

        matches.Should().HaveCount(1);
    }

    [Fact]
    public async Task BritishAmericanSpelling_MultipleVariants()
    {
        // Multiple British/American spelling differences in a single title
        var (svc, _) = CreateService(
            MakeRef(1, "Randomized paediatric trial at a centre for behavioral research", year: 2023),
            MakeRef(2, "Randomised paediatric trial at a centre for behavioural research", year: 2023));

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);

        matches.Should().HaveCount(1);
        matches[0].Reasons.Should().Contain(DuplicateReason.TitleYear);
    }
}
