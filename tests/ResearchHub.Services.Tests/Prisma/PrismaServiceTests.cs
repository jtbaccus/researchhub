using FluentAssertions;
using ResearchHub.Core.Models;
using ResearchHub.Services.Tests.Fakes;

namespace ResearchHub.Services.Tests.Prisma;

public class PrismaServiceTests
{
    private const int ProjectId = 1;
    private readonly FakeReferenceRepository _refRepo;
    private readonly FakeScreeningDecisionRepository _decisionRepo;
    private readonly PrismaService _svc;

    public PrismaServiceTests()
    {
        _refRepo = new FakeReferenceRepository();
        _decisionRepo = new FakeScreeningDecisionRepository(_refRepo);
        _svc = new PrismaService(_refRepo, _decisionRepo);
    }

    private Reference MakeRef(int id, string title)
    {
        return new Reference { Id = id, ProjectId = ProjectId, Title = title };
    }

    private void SeedRefsAndDecisions(
        int refCount,
        IEnumerable<(int refId, ScreeningPhase phase, ScreeningVerdict verdict, string? exclusionReason)> decisions)
    {
        var refs = Enumerable.Range(1, refCount)
            .Select(i => MakeRef(i, $"Study {i}"))
            .ToArray();
        _refRepo.Seed(refs);

        foreach (var (refId, phase, verdict, reason) in decisions)
        {
            var decision = new ScreeningDecision
            {
                ReferenceId = refId,
                Phase = phase,
                Verdict = verdict,
                ExclusionReason = reason,
                DecidedAt = verdict == ScreeningVerdict.Pending ? null : DateTime.UtcNow
            };
            _decisionRepo.AddAsync(decision).Wait();
        }
    }

    // --- Empty project ---

    [Fact]
    public async Task GetFlowCounts_EmptyProject_AllZeros()
    {
        var counts = await _svc.GetFlowCountsAsync(ProjectId);

        counts.Identification.RecordsIdentified.Should().Be(0);
        counts.Identification.DuplicatesRemoved.Should().Be(0);
        counts.Identification.RecordsAfterDuplicates.Should().Be(0);
        counts.Screening.RecordsScreened.Should().Be(0);
        counts.Screening.RecordsExcluded.Should().Be(0);
        counts.Eligibility.FullTextAssessed.Should().Be(0);
        counts.Eligibility.FullTextExcluded.Should().Be(0);
        counts.Inclusion.StudiesIncluded.Should().Be(0);
    }

    // --- Title-only screening ---

    [Fact]
    public async Task GetFlowCounts_TitleOnlyDecisions_CorrectCounts()
    {
        SeedRefsAndDecisions(10, new[]
        {
            (1, ScreeningPhase.TitleAbstract, ScreeningVerdict.Include, (string?)null),
            (2, ScreeningPhase.TitleAbstract, ScreeningVerdict.Include, null),
            (3, ScreeningPhase.TitleAbstract, ScreeningVerdict.Include, null),
            (4, ScreeningPhase.TitleAbstract, ScreeningVerdict.Exclude, "Not relevant"),
            (5, ScreeningPhase.TitleAbstract, ScreeningVerdict.Exclude, "Wrong population"),
            (6, ScreeningPhase.TitleAbstract, ScreeningVerdict.Maybe, null),
            (7, ScreeningPhase.TitleAbstract, ScreeningVerdict.Pending, null),
            (8, ScreeningPhase.TitleAbstract, ScreeningVerdict.Pending, null),
            (9, ScreeningPhase.TitleAbstract, ScreeningVerdict.Pending, null),
            (10, ScreeningPhase.TitleAbstract, ScreeningVerdict.Pending, null),
        });

        var counts = await _svc.GetFlowCountsAsync(ProjectId);

        counts.Identification.RecordsIdentified.Should().Be(10);
        counts.Identification.DuplicatesRemoved.Should().Be(0);
        counts.Identification.RecordsAfterDuplicates.Should().Be(10);
        counts.Screening.RecordsScreened.Should().Be(10);
        counts.Screening.RecordsExcluded.Should().Be(2); // 2 non-duplicate exclusions
        // No full-text decisions, so eligibility uses title included + maybe
        counts.Eligibility.FullTextAssessed.Should().Be(4); // 3 included + 1 maybe
        counts.Eligibility.FullTextExcluded.Should().Be(0);
        counts.Inclusion.StudiesIncluded.Should().Be(3);
    }

    // --- With full-text decisions ---

    [Fact]
    public async Task GetFlowCounts_WithFullTextDecisions_CorrectCounts()
    {
        SeedRefsAndDecisions(5, new[]
        {
            // Title screening
            (1, ScreeningPhase.TitleAbstract, ScreeningVerdict.Include, (string?)null),
            (2, ScreeningPhase.TitleAbstract, ScreeningVerdict.Include, null),
            (3, ScreeningPhase.TitleAbstract, ScreeningVerdict.Include, null),
            (4, ScreeningPhase.TitleAbstract, ScreeningVerdict.Exclude, "Not relevant"),
            (5, ScreeningPhase.TitleAbstract, ScreeningVerdict.Exclude, "Wrong population"),
            // Full-text screening
            (1, ScreeningPhase.FullText, ScreeningVerdict.Include, null),
            (2, ScreeningPhase.FullText, ScreeningVerdict.Exclude, "No full text"),
            (3, ScreeningPhase.FullText, ScreeningVerdict.Include, null),
        });

        var counts = await _svc.GetFlowCountsAsync(ProjectId);

        counts.Identification.RecordsIdentified.Should().Be(5);
        counts.Screening.RecordsExcluded.Should().Be(2);
        counts.Eligibility.FullTextAssessed.Should().Be(3); // 2 include + 1 exclude in FT
        counts.Eligibility.FullTextExcluded.Should().Be(1);
        counts.Inclusion.StudiesIncluded.Should().Be(2);
    }

    // --- Duplicate removal ---

    [Fact]
    public async Task GetFlowCounts_DuplicateExclusions_CountedCorrectly()
    {
        SeedRefsAndDecisions(6, new[]
        {
            (1, ScreeningPhase.TitleAbstract, ScreeningVerdict.Include, (string?)null),
            (2, ScreeningPhase.TitleAbstract, ScreeningVerdict.Include, null),
            (3, ScreeningPhase.TitleAbstract, ScreeningVerdict.Exclude, "duplicate - DOI match"),
            (4, ScreeningPhase.TitleAbstract, ScreeningVerdict.Exclude, "Duplicate entry"),
            (5, ScreeningPhase.TitleAbstract, ScreeningVerdict.Exclude, "Not relevant"),
            (6, ScreeningPhase.TitleAbstract, ScreeningVerdict.Pending, null),
        });

        var counts = await _svc.GetFlowCountsAsync(ProjectId);

        counts.Identification.RecordsIdentified.Should().Be(6);
        counts.Identification.DuplicatesRemoved.Should().Be(2); // 2 with "duplicate" in reason
        counts.Identification.RecordsAfterDuplicates.Should().Be(4); // 6 - 2
        counts.Screening.RecordsScreened.Should().Be(4);
        counts.Screening.RecordsExcluded.Should().Be(1); // 3 total excluded - 2 duplicates = 1
    }

    [Fact]
    public async Task GetFlowCounts_DuplicateReasonCaseInsensitive()
    {
        SeedRefsAndDecisions(3, new[]
        {
            (1, ScreeningPhase.TitleAbstract, ScreeningVerdict.Include, (string?)null),
            (2, ScreeningPhase.TitleAbstract, ScreeningVerdict.Exclude, "DUPLICATE"),
            (3, ScreeningPhase.TitleAbstract, ScreeningVerdict.Exclude, "Duplicate found"),
        });

        var counts = await _svc.GetFlowCountsAsync(ProjectId);

        counts.Identification.DuplicatesRemoved.Should().Be(2);
    }

    [Fact]
    public async Task GetFlowCounts_NullExclusionReason_NotDuplicate()
    {
        SeedRefsAndDecisions(2, new[]
        {
            (1, ScreeningPhase.TitleAbstract, ScreeningVerdict.Exclude, (string?)null),
            (2, ScreeningPhase.TitleAbstract, ScreeningVerdict.Exclude, ""),
        });

        var counts = await _svc.GetFlowCountsAsync(ProjectId);

        counts.Identification.DuplicatesRemoved.Should().Be(0);
    }

    // --- References only, no decisions ---

    [Fact]
    public async Task GetFlowCounts_ReferencesButNoDecisions_ShowsIdentification()
    {
        _refRepo.Seed(new[]
        {
            MakeRef(1, "Study A"),
            MakeRef(2, "Study B"),
            MakeRef(3, "Study C")
        });

        var counts = await _svc.GetFlowCountsAsync(ProjectId);

        counts.Identification.RecordsIdentified.Should().Be(3);
        counts.Identification.DuplicatesRemoved.Should().Be(0);
        counts.Identification.RecordsAfterDuplicates.Should().Be(3);
        counts.Screening.RecordsScreened.Should().Be(3);
        counts.Screening.RecordsExcluded.Should().Be(0);
        counts.Inclusion.StudiesIncluded.Should().Be(0);
    }
}
