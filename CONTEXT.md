# Project Context: ResearchHub

*Last updated: 2026-02-08*

## Current Goal

Polish end-to-end workflow and prepare for real-world testing with reference datasets.

## Key Architecture Notes

- Services live in `src/ResearchHub.Services` with simple interfaces and manual wiring in `src/ResearchHub.App/App.axaml.cs`.
- Reference data is modeled in `src/ResearchHub.Core/Models/Reference.cs` and accessed via `IReferenceRepository`.
- `DeduplicationService` uses DOI/PMID matching + Jaccard/Dice title similarity (threshold 0.88).
- Screening View is tri-mode: standard screening (I/E/M), screening with PDF split panel (P toggle), and duplicate review (L/R/S/Esc). Inline LLM suggestion section shows on reference cards (A key or auto-suggest toggle). "Proceed to Extraction" button appears when screening is complete.
- `PdfViewerControl` is a reusable UserControl in `Controls/` — bind `FilePath` to show a PDF with page nav, zoom, and external open.
- Extraction View has PDF split panel (P key toggle), SaveFileDialog export (CSV/XLSX), and extracted-reference checkmarks.
- Nav bar has active tab highlighting (white underline indicator).
- Library View uses ListBox (not DataGrid — Avalonia 11.x requires separate `Avalonia.Controls.DataGrid` package).
- ClosedXML 0.102.3 for Excel export (0.104+ pulls SkiaSharp 3.x which conflicts with Avalonia's native assets).
- `SkiaSharp.NativeAssets.Linux` pinned to 3.119.1 in App.csproj to match managed SkiaSharp version.

## Recent Changes

- **Deduplication Service Hardening** (2026-02-08):
  - Stress-tested with 600-ref dirty dataset (80 DOI, 40 PMID, 100 title-year, 50 near-miss pairs, 60 background).
  - Lowered default TitleSimilarityThreshold from 0.88 → 0.86 (best F1 = 0.986, zero false positives).
  - Fixed DOI normalization: trailing periods/commas now stripped.
  - Added Unicode FormD normalization: accented chars (é→e, à→a) handled before title comparison.
  - Added subtitle-aware matching: pre-colon comparison when >0.95 similarity catches truncated subtitles.
- **Deduplication Optimization** (2026-02-08):
  - Performed stress test with 600 references; optimized threshold to 0.86 (F1=0.986).
  - Implemented Unicode Normalization (FormD) to handle accented characters (100% detection for diacritics).
  - Enhanced DOI normalization to strip trailing punctuation (100% DOI recall).
  - Added subtitle-aware matching (pre-colon similarity check) to catch truncated titles.
- **Deduplication Settings UI** (2026-02-08):
  - Implemented user-tunable deduplication settings in Screening View.
  - Added `DedupYearTolerance` and `DedupNormalizeSpelling` to `DeduplicationOptions`.
  - Added collapsible settings panel to `ScreeningView.axaml` with controls for year tolerance (0-5) and spelling normalization toggle.
  - Updated `DeduplicationService` to conditionally skip British/American spelling normalization based on user preference.
- **Data Export & Workflow Polish** (2026-02-07):
  - Added ClosedXML 0.102.3 for Excel export; enhanced CSV export with Authors/Journal/Year/DOI columns.
  - Replaced hardcoded Desktop export with SaveFileDialog (CSV/XLSX filters, Ctrl+E shortcut).
  - Added PDF viewer to Extraction view — three-column split layout with GridSplitters, P key toggle.
  - Added `ExtractionReferenceItem` wrapper with `IsExtracted` flag; green checkmarks in reference list.
  - Added active nav tab highlighting (white underline under current section).
  - Added "Proceed to Extraction" button on screening complete (both with/without PDF panels).
  - Fixed SkiaSharp native lib conflict (pinned `SkiaSharp.NativeAssets.Linux` 3.119.1).
  - Fixed SQLite reserved word error (quoted `"References"` in FK clause).
  - Fixed Library view blank page (replaced DataGrid with ListBox; wired Import/AttachPdf via code-behind for compiled bindings).
- **Extraction View UI** (2026-02-07):
  - Rewrote `ExtractionViewModel.cs` with `CurrentRowViewModel` pattern — loads/creates extraction rows per-reference from service, type-specific `ColumnValueViewModel` (Text/Number/Boolean/Date/Dropdown/MultiSelect), `MultiSelectOptionViewModel` for checkbox groups.
  - Added schema editor: create/edit/delete schemas with drag-to-reorder columns, type picker, options field for Dropdown/MultiSelect.
  - Added progress tracking (extracted count / total, progress bar percentage).
  - Rewrote `ExtractionView.axaml` with three-mode layout (empty state, extraction form, schema editor).
  - Added keyboard shortcuts (Ctrl+S save, Ctrl+Left/Right navigate) in code-behind.
- **LLM Screening Suggestion UI** (2026-02-07):
  - Extended `ScreeningViewModel` with LLM state, auto-suggest toggle, cancellation token management.
  - Added inline AI Suggestion section to both card variants; toolbar buttons and A key shortcut.
- **PDF Viewer UI** (2026-02-07):
  - Added `PDFtoImage 5.2.0` for in-app PDF rendering. Created `Controls/PdfViewerControl`.
  - Screening View: split panel mode (P key), Attach PDF file picker.
  - Library View: PDF Attachments section with Attach/Open/Remove.
- **PRISMA Flow Diagram** (2026-02-06):
  - `PrismaViewModel` + `PrismaView` with live counts, color-coded phases, SVG export.
- **Backend services** (2026-02-06):
  - LLM screening via Ollama, deduplication, PDF attachment, PRISMA flow counts.

## Known Issues/Questions

- ~~Fuzzy matching thresholds may need tuning with real datasets.~~ Stress test (600 refs, 270 ground-truth pairs) confirmed 0.86 as optimal F1 (0.986). Default lowered from 0.88 → 0.86.
- ~~Year tolerance bug: cross-year-group refs not compared with YearTolerance > 0.~~ Fixed — cross-group comparison now enabled.
- ~~British/American spelling sensitivity.~~ Fixed — ~30 common medical/academic spelling variants normalized before comparison.
- ~~DOI trailing punctuation not stripped.~~ Fixed — `NormalizeDoi` now strips trailing periods/commas.
- ~~Accented characters (é, à) cause dedup misses.~~ Fixed — Unicode FormD normalization strips combining marks before comparison.
- ~~Subtitle truncation causes false negatives.~~ Mitigated — subtitle-aware matching compares pre-colon portions when similarity > 0.95.
- PDFtoImage emits CA1416 platform-compat warnings (harmless for desktop-only app).
- ClosedXML must stay at 0.102.x to avoid SkiaSharp version conflict with Avalonia.
