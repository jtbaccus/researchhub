using Microsoft.EntityFrameworkCore;
using ResearchHub.Core.Models;
using System.Text.Json;

namespace ResearchHub.Data;

public class AppDbContext : DbContext
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Reference> References => Set<Reference>();
    public DbSet<ReferencePdf> ReferencePdfs => Set<ReferencePdf>();
    public DbSet<ScreeningDecision> ScreeningDecisions => Set<ScreeningDecision>();
    public DbSet<ExtractionSchema> ExtractionSchemas => Set<ExtractionSchema>();
    public DbSet<ExtractionRow> ExtractionRows => Set<ExtractionRow>();
    public DbSet<SyncLog> SyncLogs => Set<SyncLog>();

    private readonly string _dbPath;

    public AppDbContext(string dbPath)
    {
        _dbPath = dbPath;
    }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        _dbPath = string.Empty;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured && !string.IsNullOrEmpty(_dbPath))
        {
            optionsBuilder.UseSqlite($"Data Source={_dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Project
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired();
            entity.HasMany(e => e.References)
                  .WithOne(r => r.Project)
                  .HasForeignKey(r => r.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.ExtractionSchemas)
                  .WithOne(s => s.Project)
                  .HasForeignKey(s => s.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Reference
        modelBuilder.Entity<Reference>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired();

            // Store lists as JSON
            entity.Property(e => e.Authors)
                  .HasConversion(
                      v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                      v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

            entity.Property(e => e.Tags)
                  .HasConversion(
                      v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                      v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

            entity.Property(e => e.CustomFields)
                  .HasConversion(
                      v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                      v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>());

            entity.HasMany(e => e.ScreeningDecisions)
                  .WithOne(s => s.Reference)
                  .HasForeignKey(s => s.ReferenceId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.ExtractionRows)
                  .WithOne(r => r.Reference)
                  .HasForeignKey(r => r.ReferenceId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.PdfAttachments)
                  .WithOne(p => p.Reference)
                  .HasForeignKey(p => p.ReferenceId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            entity.HasIndex(e => e.Doi);
            entity.HasIndex(e => e.Pmid);
            entity.HasIndex(e => e.Year);
        });

        // ReferencePdf
        modelBuilder.Entity<ReferencePdf>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StoredPath).IsRequired();
            entity.Property(e => e.FileSizeBytes).IsRequired();

            entity.HasIndex(e => e.ReferenceId);
            entity.HasIndex(e => new { e.ReferenceId, e.StoredPath }).IsUnique();
        });

        // ScreeningDecision
        modelBuilder.Entity<ScreeningDecision>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ReferenceId, e.Phase }).IsUnique();
        });

        // ExtractionSchema
        modelBuilder.Entity<ExtractionSchema>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();

            entity.Property(e => e.Columns)
                  .HasConversion(
                      v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                      v => JsonSerializer.Deserialize<List<ExtractionColumn>>(v, (JsonSerializerOptions?)null) ?? new List<ExtractionColumn>());

            entity.HasMany(e => e.Rows)
                  .WithOne(r => r.Schema)
                  .HasForeignKey(r => r.SchemaId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ExtractionRow
        modelBuilder.Entity<ExtractionRow>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ReferenceId, e.SchemaId }).IsUnique();

            entity.Property(e => e.Values)
                  .HasConversion(
                      v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                      v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>());
        });

        // SyncLog
        modelBuilder.Entity<SyncLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Source).IsRequired();
            entity.HasOne(e => e.Project)
                  .WithMany()
                  .HasForeignKey(e => e.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
