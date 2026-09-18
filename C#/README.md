# Arabic Document Search

Native Windows WPF application for local Arabic PDF and DOCX search.

## Current implementation

- .NET 8 WPF application, compatible with the installed .NET 9 SDK
- MVVM-style view model with dependency injection and hosting
- Recursive PDF/DOCX discovery with excluded folders
- Stable document IDs based on normalized path, size, and last-write time
- Incremental indexing that skips unchanged files
- PDF page-level extraction through PdfPig
- DOCX paragraph/table-body extraction through Open XML SDK
- Arabic normalization for letters, diacritics, Arabic/Persian digits, whitespace, and case
- Persistent Lucene index for normal searches
- Multilingual Arabic/English search with All, DocumentText, FileName, and Path scopes
- Filename tokenization that handles names such as `6871_2025_Economic_Court.pdf`
- Extracted-text viewer showing original versus normalized indexed text
- Local JSONL search and indexing diagnostics with stack traces
- Unicode-quality warnings for replacement characters, controls, and common mojibake markers
- SQLite metadata under `%LOCALAPPDATA%\\ArabicDocumentSearch`
- Controlled parallel extraction, cancellation, progress, error isolation, and deleted-file cleanup
- Search result snippets and double-click opening through the Windows default application

## Build and run

From this directory:

```powershell
dotnet build ArabicDocumentSearch.sln
dotnet run --project ArabicDocumentSearch.App
```

Run tests:

```powershell
dotnet test ArabicDocumentSearch.Tests
```

Publish a self-contained Windows x64 build:

```powershell
dotnet publish ArabicDocumentSearch.App -c Release -r win-x64 --self-contained true
```

## Notes

The current OCR hook is intentionally isolated from normal extraction. Text PDFs and DOCX files work without Tesseract. OCR service integration, advanced settings, richer DOCX locations, and full index-management screens are planned extensions of the existing interfaces.
