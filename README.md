# ResearchHub

Cross-platform desktop application for managing the full systematic review workflow: literature search (Elicit) → screening → data extraction → citation management (EndNote/Zotero).

## Status

**MVP Complete** — 2026-01-25

- All 28 unit tests passing
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
    └── ResearchHub.Core.Tests/  # Unit tests (28 tests)
```

## Features

### Reference Library
- Import from RIS, BibTeX, CSV (Elicit export compatible)
- View/edit reference metadata (title, authors, abstract, DOI, PMID)
- Tagging and organization
- Export to RIS/BibTeX

### Screening Workspace
- Title/abstract screening queue
- Include/Exclude/Maybe decisions
- **Keyboard shortcuts:** I=Include, E=Exclude, M=Maybe
- Exclusion reason tracking
- Progress statistics dashboard

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

- PDF attachment and viewer integration
- Watch folder sync for automatic import
- PRISMA flow diagram generation
- Deduplication algorithm
- Local LLM for screening assistance
- Elicit API integration (when available)

---

*Part of Jon's research toolchain. See parent turing repo for context.*
