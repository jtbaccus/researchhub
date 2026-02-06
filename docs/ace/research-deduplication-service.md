# Research: Deduplication Service

*Date: 2026-02-06 | Project: ResearchHub*

## Question/Goal

Understand current data model and service patterns to add a deduplication service that identifies potential duplicate references using DOI, PMID, and fuzzy Title+Year matching.

## Findings

### Key Observations

- References are stored via `ResearchHub.Core.Models.Reference` with `Title`, `Year`, `Doi`, and `Pmid` fields.
- `LibraryService` currently blocks duplicates only during import by exact DOI/PMID match, but there is no standalone deduplication service or fuzzy matching logic.
- Services are simple classes under `ResearchHub.Services` with corresponding interfaces and are manually instantiated in `ResearchHub.App/App.axaml.cs`.

### Relevant Code/Files

- `src/ResearchHub.Core/Models/Reference.cs` — core reference fields used for deduplication.
- `src/ResearchHub.Services/LibraryService.cs` — existing exact DOI/PMID duplicate checks during import.
- `src/ResearchHub.Data/Repositories/ReferenceRepository.cs` — provides access to references per project.
- `src/ResearchHub.App/App.axaml.cs` — manual service wiring; new service should follow this pattern if exposed to UI.

### Constraints/Dependencies

- Must follow ACE workflow and create research/plan/handoff artifacts.
- No existing deduplication service; new interface/service needed in `ResearchHub.Services`.
- Fuzzy matching should be deterministic and lightweight (no external libraries in current stack).

## Conclusions

We need to add a new service in `ResearchHub.Services` that loads project references via `IReferenceRepository` and detects potential duplicates by normalized DOI/PMID exact matches and a fuzzy Title+Year comparison. We should define a small result model (e.g., duplicate pairs with reason/score) and keep the implementation dependency-free.

---

*Ready for: Plan phase*
