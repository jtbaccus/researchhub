# Plan: PDF Attachments Infrastructure

*Date: 2026-02-06 | Project: ResearchHub*

## Goal

Store PDFs locally under app data and track PDF attachments per reference in the database.

## Approach

Add a `ReferencePdf` entity with EF Core mappings and a new `PdfAttachmentService` that validates and copies PDF files into an app-owned storage directory. Wire the service in `App.axaml.cs` and add a lightweight schema initializer to ensure the new table exists for existing databases.

## Steps

1. **Model + EF Core Mapping**
   - Action: Add `ReferencePdf` model, `Reference.PdfAttachments` navigation, and DbSet/configuration in `AppDbContext`.
   - Verify: Build succeeds and model compiles with new navigation/property references.

2. **Local Storage Service**
   - Action: Create `IPdfAttachmentService` and `PdfAttachmentService` that copies PDFs to a local storage root and persists attachment records.
   - Verify: Service methods compile; logic includes file validation and path creation.

3. **Startup Wiring + Schema Ensure**
   - Action: Add storage root helper in `App.axaml.cs`, register the service, and run a small SQL-based table ensure for `ReferencePdfs` on startup.
   - Verify: App wiring compiles; schema ensure runs without errors.

4. **Context Updates**
   - Action: Update `CONTEXT.md` and add the ACE artifacts.
   - Verify: Files reflect new goal and changes.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ResearchHub.Core/Models/Reference.cs` | Modify | Add navigation collection for PDFs |
| `src/ResearchHub.Core/Models/ReferencePdf.cs` | Create | Store PDF attachment metadata |
| `src/ResearchHub.Data/AppDbContext.cs` | Modify | Add DbSet and mapping for attachments |
| `src/ResearchHub.Data/Repositories/ReferencePdfRepository.cs` | Create | Query attachments by reference |
| `src/ResearchHub.Services/IPdfAttachmentService.cs` | Create | Service contract |
| `src/ResearchHub.Services/PdfAttachmentService.cs` | Create | Local storage logic + persistence |
| `src/ResearchHub.App/App.axaml.cs` | Modify | Storage path helper, service wiring, schema ensure |
| `CONTEXT.md` | Modify | Update project context |
| `docs/ace/research-pdf-attachments.md` | Create | Research artifact |
| `docs/ace/plan-pdf-attachments.md` | Create | Plan artifact |

## Risks/Considerations

- Existing databases won’t be updated automatically without migrations; the schema ensure must match EF table shape.
- PDF validation is basic; a stronger validator could be added later.

## Success Criteria

- [ ] PDF attachments can be added via service and stored under app data.
- [ ] Database has a `ReferencePdfs` table with a foreign key to references.
- [ ] App builds with new models and service wiring.

---

*Research artifact: `docs/ace/research-pdf-attachments.md`*
