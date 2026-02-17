using FluentAssertions;
using ResearchHub.Core.Models;
using ResearchHub.Services.Tests.Fakes;

namespace ResearchHub.Services.Tests.Screening;

public class ScreeningServiceTests
{
    private const int ProjectId = 1;
    private readonly FakeReferenceRepository _refRepo;
    private readonly FakeScreeningDecisionRepository _decisionRepo;
    private readonly ScreeningService _svc;

    public ScreeningServiceTests()
    {
        _refRepo = new FakeReferenceRepository();
        _decisionRepo = new FakeScreeningDecisionRepository(_refRepo);
        _svc = new ScreeningService(_refRepo, _decisionRepo);
    }

    private void SeedReferences(params Reference[] refs)
    {
        _refRepo.Seed(refs);
    }

    private static Reference MakeRef(int id, string title)
    {
        return new Reference { Id = id, ProjectId = ProjectId, Title = title };
    }

    // --- InitializeScreening ---

    [Fact]
    public async Task InitializeScreening_CreatesPendingDecisionsForAllRefs()
    {
        SeedReferences(
            MakeRef(1, "Study A"),
            MakeRef(2, "Study B"),
            MakeRef(3, "Study C"));

        await _svc.InitializeScreeningAsync(ProjectId, ScreeningPhase.TitleAbstract);

        var stats = await _svc.GetStatsAsync(ProjectId, ScreeningPhase.TitleAbstract);
        stats.Total.Should().Be(3);
        stats.Pending.Should().Be(3);
        stats.Included.Should().Be(0);
        stats.Excluded.Should().Be(0);
    }

    [Fact]
    public async Task InitializeScreening_DoesNotDuplicateExistingDecisions()
    {
        SeedReferences(MakeRef(1, "Study A"));

        await _svc.InitializeScreeningAsync(ProjectId, ScreeningPhase.TitleAbstract);
        await _svc.InitializeScreeningAsync(ProjectId, ScreeningPhase.TitleAbstract);

        var stats = await _svc.GetStatsAsync(ProjectId, ScreeningPhase.TitleAbstract);
        stats.Total.Should().Be(1);
    }

    [Fact]
    public async Task InitializeScreening_SeparatePhases_CreateSeparateDecisions()
    {
        SeedReferences(MakeRef(1, "Study A"));

        await _svc.InitializeScreeningAsync(ProjectId, ScreeningPhase.TitleAbstract);
        await _svc.InitializeScreeningAsync(ProjectId, ScreeningPhase.FullText);

        var taStats = await _svc.GetStatsAsync(ProjectId, ScreeningPhase.TitleAbstract);
        var ftStats = await _svc.GetStatsAsync(ProjectId, ScreeningPhase.FullText);
        taStats.Total.Should().Be(1);
        ftStats.Total.Should().Be(1);
    }

    // --- GetNextForScreening ---

    [Fact]
    public async Task GetNextForScreening_ReturnsPendingReference()
    {
        SeedReferences(
            MakeRef(1, "Study A"),
            MakeRef(2, "Study B"));

        await _svc.InitializeScreeningAsync(ProjectId, ScreeningPhase.TitleAbstract);

        var next = await _svc.GetNextForScreeningAsync(ProjectId, ScreeningPhase.TitleAbstract);

        next.Should().NotBeNull();
        next!.Title.Should().BeOneOf("Study A", "Study B");
    }

    [Fact]
    public async Task GetNextForScreening_AfterAllDecided_ReturnsNull()
    {
        SeedReferences(MakeRef(1, "Study A"));

        await _svc.InitializeScreeningAsync(ProjectId, ScreeningPhase.TitleAbstract);
        await _svc.RecordDecisionAsync(1, ScreeningPhase.TitleAbstract, ScreeningVerdict.Include);

        var next = await _svc.GetNextForScreeningAsync(ProjectId, ScreeningPhase.TitleAbstract);

        next.Should().BeNull();
    }

    [Fact]
    public async Task GetNextForScreening_NoDecisions_ReturnsNull()
    {
        var next = await _svc.GetNextForScreeningAsync(ProjectId, ScreeningPhase.TitleAbstract);
        next.Should().BeNull();
    }

    // --- RecordDecision ---

    [Fact]
    public async Task RecordDecision_CreatesNewDecision()
    {
        SeedReferences(MakeRef(1, "Study A"));

        await _svc.RecordDecisionAsync(1, ScreeningPhase.TitleAbstract, ScreeningVerdict.Include,
            notes: "Good study");

        var stats = await _svc.GetStatsAsync(ProjectId, ScreeningPhase.TitleAbstract);
        stats.Total.Should().Be(1);
        stats.Included.Should().Be(1);
    }

    [Fact]
    public async Task RecordDecision_UpdatesExistingDecision()
    {
        SeedReferences(MakeRef(1, "Study A"));

        await _svc.InitializeScreeningAsync(ProjectId, ScreeningPhase.TitleAbstract);

        // Initially pending, now update to Include
        await _svc.RecordDecisionAsync(1, ScreeningPhase.TitleAbstract, ScreeningVerdict.Include);

        var stats = await _svc.GetStatsAsync(ProjectId, ScreeningPhase.TitleAbstract);
        stats.Total.Should().Be(1);
        stats.Included.Should().Be(1);
        stats.Pending.Should().Be(0);
    }

    [Fact]
    public async Task RecordDecision_WithExclusionReason_StoresReason()
    {
        SeedReferences(MakeRef(1, "Study A"));

        await _svc.RecordDecisionAsync(1, ScreeningPhase.TitleAbstract, ScreeningVerdict.Exclude,
            exclusionReason: "Not relevant");

        var decisions = await _decisionRepo.GetByProjectAndPhaseAsync(ProjectId, ScreeningPhase.TitleAbstract);
        var decision = decisions.First();
        decision.Verdict.Should().Be(ScreeningVerdict.Exclude);
        decision.ExclusionReason.Should().Be("Not relevant");
        decision.DecidedAt.Should().NotBeNull();
    }

    // --- GetStats ---

    [Fact]
    public async Task GetStats_CorrectCountsByVerdict()
    {
        SeedReferences(
            MakeRef(1, "Study A"),
            MakeRef(2, "Study B"),
            MakeRef(3, "Study C"),
            MakeRef(4, "Study D"),
            MakeRef(5, "Study E"));

        await _svc.InitializeScreeningAsync(ProjectId, ScreeningPhase.TitleAbstract);

        await _svc.RecordDecisionAsync(1, ScreeningPhase.TitleAbstract, ScreeningVerdict.Include);
        await _svc.RecordDecisionAsync(2, ScreeningPhase.TitleAbstract, ScreeningVerdict.Include);
        await _svc.RecordDecisionAsync(3, ScreeningPhase.TitleAbstract, ScreeningVerdict.Exclude);
        await _svc.RecordDecisionAsync(4, ScreeningPhase.TitleAbstract, ScreeningVerdict.Maybe);

        var stats = await _svc.GetStatsAsync(ProjectId, ScreeningPhase.TitleAbstract);

        stats.Total.Should().Be(5);
        stats.Included.Should().Be(2);
        stats.Excluded.Should().Be(1);
        stats.Maybe.Should().Be(1);
        stats.Pending.Should().Be(1);
    }

    // --- GetByVerdict ---

    [Fact]
    public async Task GetByVerdict_FiltersCorrectly()
    {
        SeedReferences(
            MakeRef(1, "Include Me"),
            MakeRef(2, "Exclude Me"),
            MakeRef(3, "Include Me Too"));

        await _svc.InitializeScreeningAsync(ProjectId, ScreeningPhase.TitleAbstract);
        await _svc.RecordDecisionAsync(1, ScreeningPhase.TitleAbstract, ScreeningVerdict.Include);
        await _svc.RecordDecisionAsync(2, ScreeningPhase.TitleAbstract, ScreeningVerdict.Exclude);
        await _svc.RecordDecisionAsync(3, ScreeningPhase.TitleAbstract, ScreeningVerdict.Include);

        var included = (await _svc.GetByVerdictAsync(ProjectId, ScreeningPhase.TitleAbstract, ScreeningVerdict.Include)).ToList();
        var excluded = (await _svc.GetByVerdictAsync(ProjectId, ScreeningPhase.TitleAbstract, ScreeningVerdict.Exclude)).ToList();

        included.Should().HaveCount(2);
        excluded.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetByVerdict_NoMatches_ReturnsEmpty()
    {
        SeedReferences(MakeRef(1, "Study A"));
        await _svc.InitializeScreeningAsync(ProjectId, ScreeningPhase.TitleAbstract);

        var results = (await _svc.GetByVerdictAsync(ProjectId, ScreeningPhase.TitleAbstract, ScreeningVerdict.Include)).ToList();

        results.Should().BeEmpty();
    }

    // --- Screening queue ---

    [Fact]
    public async Task GetScreeningQueue_ReturnsPendingReferences()
    {
        SeedReferences(
            MakeRef(1, "Pending A"),
            MakeRef(2, "Decided B"),
            MakeRef(3, "Pending C"));

        await _svc.InitializeScreeningAsync(ProjectId, ScreeningPhase.TitleAbstract);
        await _svc.RecordDecisionAsync(2, ScreeningPhase.TitleAbstract, ScreeningVerdict.Include);

        var queue = (await _svc.GetScreeningQueueAsync(ProjectId, ScreeningPhase.TitleAbstract)).ToList();

        queue.Should().HaveCount(2);
    }

    // --- Empty project ---

    [Fact]
    public async Task InitializeScreening_NoReferences_NoDecisions()
    {
        await _svc.InitializeScreeningAsync(ProjectId, ScreeningPhase.TitleAbstract);

        var stats = await _svc.GetStatsAsync(ProjectId, ScreeningPhase.TitleAbstract);
        stats.Total.Should().Be(0);
    }
}
