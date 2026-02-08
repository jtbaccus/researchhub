# Project Context: ResearchHub

*Last updated: 2026-02-07*

## Current Goal

Continue building out remaining UI features and polish existing views.

## Key Architecture Notes

- Services live in `src/ResearchHub.Services` with simple interfaces and manual wiring in `src/ResearchHub.App/App.axaml.cs`.
- Reference data is modeled in `src/ResearchHub.Core/Models/Reference.cs` and accessed via `IReferenceRepository`.
- `DeduplicationService` uses DOI/PMID matching + Jaccard/Dice title similarity (threshold 0.88).
- Screening View is tri-mode: standard screening (I/E/M), screening with PDF split panel (P toggle), and duplicate review (L/R/S/Esc). Inline LLM suggestion section shows on reference cards (A key or auto-suggest toggle).
- `PdfViewerControl` is a reusable UserControl in `Controls/` — bind `FilePath` to show a PDF with page nav, zoom, and external open.

## Recent Changes

- **Extraction View UI** (2026-02-07):
  - Rewrote `ExtractionViewModel.cs` with `CurrentRowViewModel` pattern — loads/creates extraction rows per-reference from service, type-specific `ColumnValueViewModel` (Text/Number/Boolean/Date/Dropdown/MultiSelect), `MultiSelectOptionViewModel` for checkbox groups.
  - Added schema editor: create/edit/delete schemas with drag-to-reorder columns, type picker, options field for Dropdown/MultiSelect.
  - Added progress tracking (extracted count / total, progress bar percentage).
  - Added prev/next reference navigation commands.
  - Rewrote `ExtractionView.axaml` with three-mode layout (empty state, extraction form, schema editor) using IsVisible pattern. Type-specific controls render per column type.
  - Added keyboard shortcuts (Ctrl+S save, Ctrl+Left/Right navigate) in code-behind.
- **LLM Screening Suggestion UI** (2026-02-07):
  - Extended `ScreeningViewModel` with LLM state: observable properties for verdict/confidence/reason/error, auto-suggest toggle, cancellation token management.
  - Added `RequestLlmSuggestion` (manual) and `ToggleLlmAutoSuggest` commands; auto-fires suggestion on reference advance when enabled.
  - Added inline AI Suggestion section to both reference card variants (with/without PDF panel) — shows loading spinner, verdict+confidence+reason, or error.
  - Added toolbar buttons: "Auto LLM" toggle and "Ask LLM (A)" button.
  - Added `A` keyboard shortcut for manual LLM trigger; updated shortcut legend.
- **PRISMA Flow Diagram UI** (2026-02-06):
  - Created `PrismaViewModel` with 8 computed text properties, Refresh/ExportSvg commands, and programmatic SVG generation.
  - Created `PrismaView` with native Avalonia Grid layout: color-coded phase labels (Identification/Screening/Eligibility/Included), flow boxes with Unicode arrow connectors, side exclusion boxes.
  - SVG export produces publication-ready vector diagram with `<marker>` arrowheads, phase color coding, and proper text layout.
  - Added "PRISMA" nav button in MainWindow toolbar, wired with DataTemplate.
- **PDF Viewer UI** (2026-02-07):
  - Added `PDFtoImage 5.2.0` NuGet package for in-app PDF rendering via PDFium/SkiaSharp.
  - Created `Controls/PdfViewerControl` — reusable control with page navigation, zoom, fit-width, open-external.
  - Screening View: toggle button + `P` key opens split panel (reference card left, PDF viewer right, `GridSplitter` resizable).
  - Screening View: "Attach PDF" button with file picker in action bar.
  - Library View: PDF Attachments section in detail panel with Attach/Open/Remove per-PDF.
  - All rendering is thread-safe (`SemaphoreSlim`, `Task.Run` for PDFium calls), DPI capped at 288.
- **Local LLM screening backend** (2026-02-06):
  - Added `ILlmScreeningService`/`LlmScreeningService` with Ollama HTTP integration.
  - Configurable prompt template and model/endpoint via environment variables.
  - Structured JSON parsing for verdict suggestions with error handling for Ollama/model issues.
- Integrated `DeduplicationService` into the Screening View as a dual-mode workflow.
- Added `ReferencePdf` model + EF Core mapping for PDF attachments.
- Added local PDF attachment storage service and startup schema ensure.
- Added PRISMA flow count models and service for identification/screening/eligibility/inclusion totals.

## Known Issues/Questions

- Fuzzy matching thresholds may need tuning with real datasets.
- PRISMA flow diagram renders with live counts; SVG export available for publications.
- PDFtoImage emits CA1416 platform-compat warnings (harmless for desktop-only app).
