# Parser Notes (RIS and BibTeX)

This document summarizes the RIS and BibTeX parsing behavior implemented in `ResearchHub.Core` and the edge cases covered by tests.

## RIS

Supported behaviors:
- Tag parsing is case-insensitive (e.g., `TY`, `ty`).
- Continuation lines (lines starting with whitespace) append to the previous tag value with a space.
- Titles are pulled from `TI` or `T1`.
- Abstracts are pulled from `AB` or `N2`.
- Journal names are pulled from `JO`, `JF`, `T2`, or `JA`.
- Year is parsed from `PY`, `Y1`, or `DA` (first 4-digit year).
- Pages are pulled from `PG` if present, otherwise `SP` + `EP` are combined.
- DOI tags: `DO`.
- PMID tags: `PM`, `AN`.
- URL tags: `UR`, `L1`, `L2`.
- Keywords from `KW` become tags.

RIS record boundaries:
- `ER  -` ends a record.
- Files missing a trailing `ER` are still parsed by emitting the final record.

## BibTeX

Supported behaviors:
- Entry delimiters: `{}` and `()` are both supported.
- Field values support nested braces.
- Values can be concatenated with `#` (e.g., `{A} # " B"`).
- Quoted values can span multiple lines and contain commas.
- Common LaTeX cleanup is applied:
  - `\textit{}` / `\textbf{}` / `\emph{}` / `\textrm{}` / `\textsc{}` are unwrapped.
  - Accent sequences like `\"{u}` are simplified to `u`.
  - Escaped `\&` and `\%` are converted to `&` and `%`.
  - Remaining braces are removed for case preservation.
- Authors are split on `and`.
- Keywords are split on commas and semicolons into tags.
- Non-reference entries are ignored: `@string`, `@comment`, `@preamble`.

Mapped fields:
- `title` -> Title (required; missing title skips entry)
- `abstract` -> Abstract
- `journal` or `booktitle` -> Journal
- `year` -> Year (int)
- `volume` -> Volume
- `number` -> Issue
- `pages` -> Pages (double dash converted to single dash)
- `doi` -> DOI
- `pmid` -> PMID
- `url` -> URL

RIS robustness:
- Empty tag values (e.g., `AU  -` with no value) are silently ignored.
- Mixed line endings (`\r\n` and `\n` in the same file) are handled correctly.

## Test Coverage

Parser edge cases are exercised in:
- `tests/ResearchHub.Core.Tests/Parsers/RisParserTests.cs` — 11 core tests
- `tests/ResearchHub.Core.Tests/Parsers/BibTexParserTests.cs` — 13 core tests
- `tests/ResearchHub.Core.Tests/Parsers/RisParserStressTests.cs` — 20 stress tests (PubMed/Scopus exports, Unicode, empty/malformed input, multi-line abstracts, bulk parsing)
- `tests/ResearchHub.Core.Tests/Parsers/BibTexParserStressTests.cs` — 24 stress tests (Google Scholar/Zotero exports, deep nesting, LaTeX, corporate authors, entry types, UTF-8, mixed delimiters, bulk parsing)
