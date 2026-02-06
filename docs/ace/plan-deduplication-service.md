# Plan: Deduplication Service

*Date: 2026-02-06 | Project: ResearchHub*

## Goal

Provide a `ResearchHub.Services` deduplication service that returns potential duplicate references based on normalized DOI, PMID, and fuzzy Title+Year matching.

## Approach

Add a new service interface and implementation that uses `IReferenceRepository` to load project references, normalizes identifiers, and computes duplicate pairs with reasons and similarity scores. Keep logic dependency-free and deterministic. Optionally expose the service via `App.axaml.cs` to match existing service wiring.

## Steps

1. **Define deduplication contracts**
   - Action: Add `IDeduplicationService`, `DeduplicationOptions`, and `DuplicateMatch`/`DuplicateReason` models under `ResearchHub.Services`.
   - Verify: Types compile and are discoverable by services project.

2. **Implement deduplication logic**
   - Action: Add `DeduplicationService` that loads references, normalizes DOI/PMID, groups matches, and performs fuzzy Title+Year comparison.
   - Verify: Build passes; logic uses repository and avoids duplicate pair emission.

3. **Wire service for app access (optional but consistent)**
   - Action: Add a static `DeduplicationService` property in `App.axaml.cs` and initialize it alongside other services.
   - Verify: App compiles without DI errors.

4. **Update context artifacts**
   - Action: Create/update `CONTEXT.md` and write a handoff artifact summarizing work.
   - Verify: Files exist with current date and accurate summary.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ResearchHub.Services/IDeduplicationService.cs` | Create | Service contract + options/results models. |
| `src/ResearchHub.Services/DeduplicationService.cs` | Create | Deduplication implementation. |
| `src/ResearchHub.App/App.axaml.cs` | Modify | Optional service wiring for app access. |
| `CONTEXT.md` | Create/Modify | Project context snapshot. |
| `docs/ace/handoff-deduplication-service.md` | Create | Session handoff artifact. |

## Risks/Considerations

- Fuzzy matching thresholds can over/under-match; default values should be conservative and configurable via options.
- Pairwise matching is O(n^2); grouping by year mitigates typical library sizes.

## Success Criteria

- [ ] `DeduplicationService` returns duplicate pairs with reasons for DOI, PMID, and Title+Year matches.
- [ ] Code builds with new service types included.
- [ ] ACE artifacts and `CONTEXT.md` are updated.

---

*Research artifact: docs/ace/research-deduplication-service.md*
