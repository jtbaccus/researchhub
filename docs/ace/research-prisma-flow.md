# Research: PRISMA Flow Diagram Counts

*Date: 2026-02-06 | Project: ResearchHub*

## Question/Goal

Implement logic to compute PRISMA flow diagram counts (identification, screening, eligibility, inclusion) based on existing project data.

## Findings

### Key Observations

- References are stored per project in `Reference` and can be loaded with screening decisions via `IReferenceRepository.GetWithScreeningDecisionsAsync`.
- Screening decisions are tracked by phase (`TitleAbstract`, `FullText`) with verdicts (`Pending`, `Include`, `Exclude`, `Maybe`).
- Duplicate handling during screening marks exclusions with `ExclusionReason = "Duplicate"` (see `ScreeningViewModel.ResolveDuplicatePairAsync`).
- Imports prevent duplicates by DOI/PMID, but duplicate counts are not persisted; there is no import log usage or duplicate counter stored in the DB.

### Relevant Code/Files

- `src/ResearchHub.Core/Models/ScreeningDecision.cs` — phases/verdicts and exclusion reason.
- `src/ResearchHub.Core/Models/Reference.cs` — reference model with screening decisions.
- `src/ResearchHub.Data/Repositories/ScreeningDecisionRepository.cs` — phase stats and decision retrieval.
- `src/ResearchHub.Data/Repositories/ReferenceRepository.cs` — project reference retrieval.
- `src/ResearchHub.Services/ScreeningService.cs` — screening workflow and decision recording.
- `src/ResearchHub.App/ViewModels/ScreeningViewModel.cs` — duplicate exclusion uses reason "Duplicate".

### Constraints/Dependencies

- No existing PRISMA logic or UI; needs a new service/model.
- Duplicate counts prior to import are unavailable; counts must be derived from stored references/decisions.

## Conclusions

A new service can compute PRISMA counts from references and screening decisions. Identification counts will be based on total references with duplicate removals inferred from `TitleAbstract` exclusions marked "Duplicate". Screening, eligibility, and inclusion can be derived from phase decisions, with a fallback to Title/Abstract decisions if FullText data is absent.

---

*Ready for: Plan phase*
