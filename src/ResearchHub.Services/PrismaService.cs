using ResearchHub.Core.Models;
using ResearchHub.Data.Repositories;

namespace ResearchHub.Services;

public class PrismaService : IPrismaService
{
    private readonly IReferenceRepository _referenceRepository;
    private readonly IScreeningDecisionRepository _screeningRepository;

    public PrismaService(
        IReferenceRepository referenceRepository,
        IScreeningDecisionRepository screeningRepository)
    {
        _referenceRepository = referenceRepository;
        _screeningRepository = screeningRepository;
    }

    public async Task<PrismaFlowCounts> GetFlowCountsAsync(int projectId)
    {
        var references = await _referenceRepository.GetByProjectIdAsync(projectId);
        var totalReferences = references.Count();

        var titleDecisions = (await _screeningRepository
                .GetByProjectAndPhaseAsync(projectId, ScreeningPhase.TitleAbstract))
            .ToList();

        var fullTextDecisions = (await _screeningRepository
                .GetByProjectAndPhaseAsync(projectId, ScreeningPhase.FullText))
            .ToList();

        var titleIncluded = titleDecisions.Count(d => d.Verdict == ScreeningVerdict.Include);
        var titleExcluded = titleDecisions.Count(d => d.Verdict == ScreeningVerdict.Exclude);
        var titleMaybe = titleDecisions.Count(d => d.Verdict == ScreeningVerdict.Maybe);

        var duplicateRemoved = titleDecisions.Count(d =>
            d.Verdict == ScreeningVerdict.Exclude && IsDuplicateReason(d.ExclusionReason));

        var recordsAfterDuplicates = Math.Max(totalReferences - duplicateRemoved, 0);
        var titleExcludedNonDuplicate = Math.Max(titleExcluded - duplicateRemoved, 0);

        var fullTextIncluded = fullTextDecisions.Count(d => d.Verdict == ScreeningVerdict.Include);
        var fullTextExcluded = fullTextDecisions.Count(d => d.Verdict == ScreeningVerdict.Exclude);
        var fullTextMaybe = fullTextDecisions.Count(d => d.Verdict == ScreeningVerdict.Maybe);

        var hasFullTextDecisions = fullTextDecisions.Count > 0;

        var eligibilityAssessed = hasFullTextDecisions
            ? fullTextIncluded + fullTextExcluded + fullTextMaybe
            : titleIncluded + titleMaybe;

        var eligibilityExcluded = hasFullTextDecisions
            ? fullTextExcluded
            : 0;

        var studiesIncluded = hasFullTextDecisions
            ? fullTextIncluded
            : titleIncluded;

        return new PrismaFlowCounts
        {
            Identification = new PrismaIdentificationCounts
            {
                RecordsIdentified = totalReferences,
                DuplicatesRemoved = duplicateRemoved,
                RecordsAfterDuplicates = recordsAfterDuplicates
            },
            Screening = new PrismaScreeningCounts
            {
                RecordsScreened = recordsAfterDuplicates,
                RecordsExcluded = titleExcludedNonDuplicate
            },
            Eligibility = new PrismaEligibilityCounts
            {
                FullTextAssessed = eligibilityAssessed,
                FullTextExcluded = eligibilityExcluded
            },
            Inclusion = new PrismaInclusionCounts
            {
                StudiesIncluded = studiesIncluded
            }
        };
    }

    private static bool IsDuplicateReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return false;
        }

        return reason.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    }
}
