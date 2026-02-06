# Research: PDF Attachments Infrastructure

*Date: 2026-02-06 | Project: ResearchHub*

## Question/Goal

Implement local PDF attachment storage and update the Reference model/database to track attached PDFs.

## Findings

### Key Observations

- References are modeled in `Reference` with an existing `PdfPath` string that is unused elsewhere in the codebase.
- The app uses EF Core with SQLite and calls `DbContext.Database.EnsureCreated()` on startup (no migrations).
- Services are wired manually in `App.axaml.cs` with simple repositories.

### Relevant Code/Files

- `src/ResearchHub.Core/Models/Reference.cs` — current reference model; includes unused `PdfPath` field.
- `src/ResearchHub.Data/AppDbContext.cs` — EF Core model configuration; no attachment entities yet.
- `src/ResearchHub.Services/LibraryService.cs` — reference import/export logic; no PDF storage.
- `src/ResearchHub.App/App.axaml.cs` — service initialization and DB creation via `EnsureCreated()`.

### Constraints/Dependencies

- Database schema updates are not managed via migrations; `EnsureCreated()` will not update existing DBs.
- Services follow a simple interface + repository pattern, instantiated manually at app startup.

## Conclusions

We need a new attachment model/table (likely one-to-many from Reference) plus a local storage service that copies PDFs into an app-owned directory. Because migrations are not used, we should add a lightweight schema check/creation path on startup for the new table (or otherwise accept that existing DBs won’t be updated).

---

*Ready for: Plan phase*
