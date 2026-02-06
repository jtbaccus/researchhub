# ResearchHub

Cross-platform desktop application for managing the full systematic review workflow: literature search (Elicit) → screening → data extraction → citation management (EndNote/Zotero).

## Status

**MVP Complete** — 2026-01-25

- All 78 unit tests passing (including 44 parser stress tests)
- Application builds and runs successfully
- Ready for testing with real data

## Technology Stack

- **Framework:** Avalonia UI 11.x (cross-platform: Windows, macOS, Linux)
- **Runtime:** .NET 9
- **Pattern:** MVVM with CommunityToolkit.Mvvm
- **Database:** SQLite via Entity Framework Core
- **Testing:** xUnit + FluentAssertions

## Project Structure

```
researchhub/
├── src/
│   ├── ResearchHub.Core/        # Models, Parsers, Exporters
│   ├── ResearchHub.Data/        # EF Core, SQLite, Repositories
│   ├── ResearchHub.Services/    # Business logic
│   └── ResearchHub.App/         # Avalonia desktop app
└── tests/
    └── ResearchHub.Core.Tests/  # Unit tests (78 tests)
```

## Features

### Reference Library
- Import from RIS, BibTeX, CSV (Elicit export compatible)
- View/edit reference metadata (title, authors, abstract, DOI, PMID)
- Attach PDFs (stored locally)
- Tagging and organization
- Export to RIS/BibTeX

### Parser Coverage (RIS/BibTeX)
- Continuation lines and multiline fields
- Lowercase RIS tags and alternative tags (e.g., `T1`, `N2`, `PG`, `PM`)
- BibTeX parentheses entries and value concatenation with `#`
- Nested braces and common LaTeX cleanup
- Multiline quoted values and comma-containing fields

See `docs/parsers.md` for supported edge cases and behaviors.

### Screening Workspace
- Title/abstract screening queue
- Include/Exclude/Maybe decisions
- **Keyboard shortcuts:** I=Include, E=Exclude, M=Maybe
- Exclusion reason tracking
- Progress statistics dashboard
- Local LLM screening assistance backend (Ollama; UI pending)

### Data Extraction
- Define custom extraction schemas
- Import from Elicit CSV exports
- Manual data entry
- Export to CSV

## Quick Start

```bash
# Build
dotnet build

# Run tests
dotnet test

# Run the app
dotnet run --project src/ResearchHub.App
```

## Integration Points

- **Elicit:** Import via RIS/BibTeX/CSV exports
- **EndNote:** Import/export via RIS format
- **Zotero:** Import/export via BibTeX format

## Database

SQLite database stored at:
- **Linux/macOS:** `~/.local/share/ResearchHub/researchhub.db`
- **Windows:** `%APPDATA%/ResearchHub/researchhub.db`

## Future Enhancements

- PDF viewer integration
- Watch folder sync for automatic import
- PRISMA flow diagram generation
- Deduplication algorithm
- Local LLM screening UI and configuration
- Elicit API integration (when available)

---

*Part of Jon's research toolchain. See parent turing repo for context.*
