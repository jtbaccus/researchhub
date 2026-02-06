# Plan: PRISMA Flow Diagram Counts

*Date: 2026-02-06 | Project: ResearchHub*

## Goal

Provide a service that computes PRISMA flow diagram counts (identification, screening, eligibility, inclusion) from project data.

## Approach

Create a Core model to represent PRISMA counts and implement a `PrismaService` in the Services layer that aggregates references and screening decisions by phase. Add simple duplicate-detection logic based on exclusion reasons and wire the service into app startup for consumption by UI/exports.

## Steps

1. **Add PRISMA count models**
   - Action: Create a Core model to represent identification/screening/eligibility/inclusion counts.
   - Verify: Project builds with new model type referenced by the service.

2. **Implement Prisma service**
   - Action: Add `IPrismaService` and `PrismaService` that compute counts using reference and screening repositories with safe fallbacks when FullText data is missing.
   - Verify: Code compiles and logic paths are covered by simple sanity checks in code review.

3. **Wire service into app**
   - Action: Register `PrismaService` in `App.axaml.cs` similarly to other services.
   - Verify: App builds; service is available via `App.PrismaService`.

4. **Update context + handoff**
   - Action: Update `CONTEXT.md` and create handoff artifact summarizing changes.
   - Verify: Files reflect new PRISMA logic and current project goals.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ResearchHub.Core/Models/PrismaFlowCounts.cs` | Create | PRISMA counts model |
| `src/ResearchHub.Services/IPrismaService.cs` | Create | Service interface |
| `src/ResearchHub.Services/PrismaService.cs` | Create | PRISMA computation logic |
| `src/ResearchHub.App/App.axaml.cs` | Modify | Wire new service |
| `CONTEXT.md` | Modify | Update project context |
| `docs/ace/handoff-prisma-flow.md` | Create | Implement phase handoff |

## Risks/Considerations

- Duplicate counts are inferred from exclusion reasons and may undercount duplicates removed at import.
- FullText phase may be unused; service should fall back to Title/Abstract data without throwing.

## Success Criteria

- [ ] `IPrismaService` returns counts for all four PRISMA sections.
- [ ] Logic handles missing FullText decisions gracefully.
- [ ] App builds with `PrismaService` available.

---

*Research artifact: `docs/ace/research-prisma-flow.md`*
