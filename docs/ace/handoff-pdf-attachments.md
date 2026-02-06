# Session Handoff

*Date: 2026-02-07 | Instance: Claude Code*
*Previous: 2026-02-06 13:28 | Instance: Codex*

## What Was Done

### Phase 1 (Codex, 2026-02-06)
- Added `ReferencePdf` model, navigation on `Reference`, and EF Core mapping/DbSet for attachments.
- Implemented local PDF storage via `PdfAttachmentService` and repository support.
- Wired storage root + attachment service in `App.axaml.cs` and added schema ensure for `ReferencePdfs`.

### Phase 2 — UI (Claude Code, 2026-02-07)
- Added `PDFtoImage 5.2.0` NuGet package for in-app PDF rendering.
- Created `Controls/PdfViewerControl` (XAML + code-behind) — reusable control with `FilePath` styled property, page navigation, zoom (0.25x–3x), fit-width, open-external. Thread-safe via `SemaphoreSlim`.
- **Screening View**: split panel (toggle via button or `P` key), reference card left / PDF viewer right with `GridSplitter`. "Attach PDF" button with file picker. Auto-loads PDF for current reference.
- **Library View**: PDF Attachments section in detail panel — Attach/Open/Remove per-PDF, auto-loads on reference selection.

## Current State

PDF attachment infrastructure and UI are both complete. Users can attach, view (in-app rendered), open externally, and remove PDFs from both Screening and Library views.

## Blockers/Open Questions

- Consider whether to deprecate or repurpose the legacy `Reference.PdfPath` field.
- PDFtoImage emits CA1416 platform-compat warnings (harmless for desktop-only app).

## Next Steps

1. PRISMA flow diagram UI (service exists, no UI yet).
2. Consider adding migrations or a more general schema upgrade path.

## Key Context for Next Instance

Storage root lives under app data `ResearchHub/attachments`, and stored paths are relative. The startup schema ensure creates the `ReferencePdfs` table for existing databases. `PdfViewerControl` uses PDFtoImage (PDFium) — all rendering must go through `Task.Run` as PDFium is not thread-safe.

---

*Related artifacts:*
- Research: `docs/ace/research-pdf-attachments.md`
- Plan: `docs/ace/plan-pdf-attachments.md`
- Project CONTEXT.md: `CONTEXT.md`
