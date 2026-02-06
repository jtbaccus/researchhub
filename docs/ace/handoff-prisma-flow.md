# Session Handoff

*Date: 2026-02-06 00:00 | Instance: Codex*

## What Was Done

- Added PRISMA flow count models in Core.
- Implemented `IPrismaService`/`PrismaService` to compute identification, screening, eligibility, and inclusion counts.
- Wired `PrismaService` into app startup.
- Updated project context and ACE artifacts.

## Current State

PRISMA flow counts can now be computed from project references and screening decisions, including duplicate removal inferred from exclusion reasons. No UI/diagram rendering is added yet.

## Blockers/Open Questions

- Should duplicate removals at import be persisted for more accurate identification counts?
- Should PRISMA counts treat `Maybe` as included for eligibility/inclusion or keep it separate in the UI?

## Next Steps

1. Add UI or export surface to render/display PRISMA flow counts.
2. Consider persisting import session stats (including duplicates) for more accurate identification counts.

## Key Context for Next Instance

Eligibility/inclusion counts fall back to Title/Abstract decisions when FullText decisions are absent. Duplicate removals are inferred by `ExclusionReason` containing "duplicate".

---

*Related artifacts:*
- Research: `docs/ace/research-prisma-flow.md`
- Plan: `docs/ace/plan-prisma-flow.md`
- Project CONTEXT.md: `CONTEXT.md`
