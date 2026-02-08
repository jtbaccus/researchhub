using FluentAssertions;
using ResearchHub.Core.Models;
using ResearchHub.Services.Tests.Fakes;
using Xunit.Abstractions;

namespace ResearchHub.Services.Tests.Extraction;

public class ExtractionServiceTests : IDisposable
{
    private const int ProjectId = 1;
    private readonly ITestOutputHelper _output;
    private readonly FakeRepository<ExtractionSchema> _schemaRepo;
    private readonly FakeRepository<ExtractionRow> _rowRepo;
    private readonly FakeReferenceRepository _refRepo;
    private readonly ExtractionService _svc;
    private readonly List<string> _tempFiles = new();

    public ExtractionServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _schemaRepo = new FakeRepository<ExtractionSchema>(s => s.Id, (s, id) => s.Id = id);
        _rowRepo = new FakeRepository<ExtractionRow>(r => r.Id, (r, id) => r.Id = id);
        _refRepo = new FakeReferenceRepository();
        _svc = new ExtractionService(_schemaRepo, _rowRepo, _refRepo);
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            try { File.Delete(f); } catch { }
        }
    }

    private string TempFile(string ext = ".csv")
    {
        var path = Path.Combine(Path.GetTempPath(), $"researchhub_test_{Guid.NewGuid()}{ext}");
        _tempFiles.Add(path);
        return path;
    }

    private static List<ExtractionColumn> SampleColumns(int count = 3)
    {
        return Enumerable.Range(1, count).Select(i => new ExtractionColumn
        {
            Name = $"Column{i}",
            Type = (ExtractionColumnType)(i % 6),
            IsRequired = i == 1
        }).ToList();
    }

    // --- Schema CRUD ---

    [Fact]
    public async Task CreateSchema_AssignsId()
    {
        var schema = await _svc.CreateSchemaAsync(ProjectId, "Test Schema", "A description", SampleColumns());

        schema.Id.Should().BeGreaterThan(0);
        schema.Name.Should().Be("Test Schema");
        schema.Columns.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetSchema_ReturnsCreatedSchema()
    {
        var created = await _svc.CreateSchemaAsync(ProjectId, "Test", null, SampleColumns());
        var retrieved = await _svc.GetSchemaAsync(created.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Test");
    }

    [Fact]
    public async Task GetSchemasByProject_FiltersCorrectly()
    {
        await _svc.CreateSchemaAsync(1, "Schema A", null, SampleColumns());
        await _svc.CreateSchemaAsync(1, "Schema B", null, SampleColumns());
        await _svc.CreateSchemaAsync(2, "Schema C", null, SampleColumns());

        var project1Schemas = (await _svc.GetSchemasByProjectAsync(1)).ToList();
        project1Schemas.Should().HaveCount(2);
        project1Schemas.Should().AllSatisfy(s => s.ProjectId.Should().Be(1));
    }

    [Fact]
    public async Task UpdateSchema_PersistsChanges()
    {
        var schema = await _svc.CreateSchemaAsync(ProjectId, "Original", null, SampleColumns());
        schema.Name = "Updated";
        await _svc.UpdateSchemaAsync(schema);

        var retrieved = await _svc.GetSchemaAsync(schema.Id);
        retrieved!.Name.Should().Be("Updated");
    }

    [Fact]
    public async Task DeleteSchema_RemovesIt()
    {
        var schema = await _svc.CreateSchemaAsync(ProjectId, "ToDelete", null, SampleColumns());
        await _svc.DeleteSchemaAsync(schema.Id);

        var retrieved = await _svc.GetSchemaAsync(schema.Id);
        retrieved.Should().BeNull();
    }

    // --- Extraction row CRUD (upsert) ---

    [Fact]
    public async Task SaveExtraction_CreatesNewRow()
    {
        var values = new Dictionary<string, string> { ["Column1"] = "value1", ["Column2"] = "value2" };
        var row = await _svc.SaveExtractionAsync(referenceId: 1, schemaId: 1, values);

        row.Id.Should().BeGreaterThan(0);
        row.Values.Should().ContainKey("Column1").WhoseValue.Should().Be("value1");
    }

    [Fact]
    public async Task SaveExtraction_UpdatesExistingRow()
    {
        var values1 = new Dictionary<string, string> { ["Column1"] = "original" };
        var row1 = await _svc.SaveExtractionAsync(1, 1, values1);

        var values2 = new Dictionary<string, string> { ["Column1"] = "updated" };
        var row2 = await _svc.SaveExtractionAsync(1, 1, values2);

        row2.Id.Should().Be(row1.Id);
        row2.Values["Column1"].Should().Be("updated");
        row2.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetExtraction_ReturnsCorrectRow()
    {
        await _svc.SaveExtractionAsync(1, 1, new Dictionary<string, string> { ["A"] = "1" });
        await _svc.SaveExtractionAsync(2, 1, new Dictionary<string, string> { ["A"] = "2" });
        await _svc.SaveExtractionAsync(1, 2, new Dictionary<string, string> { ["A"] = "3" });

        var row = await _svc.GetExtractionAsync(2, 1);
        row.Should().NotBeNull();
        row!.Values["A"].Should().Be("2");
    }

    [Fact]
    public async Task GetExtractionsForSchema_ReturnsAll()
    {
        await _svc.SaveExtractionAsync(1, 1, new Dictionary<string, string> { ["A"] = "1" });
        await _svc.SaveExtractionAsync(2, 1, new Dictionary<string, string> { ["A"] = "2" });
        await _svc.SaveExtractionAsync(3, 2, new Dictionary<string, string> { ["A"] = "3" });

        var rows = (await _svc.GetExtractionsForSchemaAsync(1)).ToList();
        rows.Should().HaveCount(2);
    }

    // --- Stress test ---

    [Fact]
    public async Task StressTest_1000Refs10Columns_SaveAndRetrieve()
    {
        var schema = await _svc.CreateSchemaAsync(ProjectId, "Stress Schema", null, SampleColumns(10));

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Save 1000 rows
        for (var i = 1; i <= 1000; i++)
        {
            var values = new Dictionary<string, string>();
            for (var c = 1; c <= 10; c++)
                values[$"Column{c}"] = $"ref{i}_col{c}";

            await _svc.SaveExtractionAsync(i, schema.Id, values);
        }

        var saveTime = sw.ElapsedMilliseconds;

        // Retrieve all
        sw.Restart();
        var rows = (await _svc.GetExtractionsForSchemaAsync(schema.Id)).ToList();
        var retrieveTime = sw.ElapsedMilliseconds;

        _output.WriteLine($"1000 rows × 10 cols: save={saveTime}ms, retrieve={retrieveTime}ms");

        rows.Should().HaveCount(1000);

        // Spot-check values
        var row500 = await _svc.GetExtractionAsync(500, schema.Id);
        row500.Should().NotBeNull();
        row500!.Values["Column5"].Should().Be("ref500_col5");
    }

    // --- CSV round-trip ---

    [Fact]
    public async Task CsvRoundTrip_ExportImport_ValuesMatch()
    {
        var columns = new List<ExtractionColumn>
        {
            new() { Name = "Outcome", Type = ExtractionColumnType.Text },
            new() { Name = "EffectSize", Type = ExtractionColumnType.Number },
            new() { Name = "Significant", Type = ExtractionColumnType.Boolean },
        };
        var schema = await _svc.CreateSchemaAsync(ProjectId, "RoundTrip Schema", null, columns);

        // Seed references so export can look them up
        var refs = Enumerable.Range(1, 50).Select(i => new Reference
        {
            Id = i,
            ProjectId = ProjectId,
            Title = $"Study {i}",
            Year = 2020 + (i % 5),
            Doi = $"10.1234/{i}",
            Authors = new List<string> { $"Author{i}" }
        });
        _refRepo.Seed(refs);

        // Save 50 extraction rows
        for (var i = 1; i <= 50; i++)
        {
            await _svc.SaveExtractionAsync(i, schema.Id, new Dictionary<string, string>
            {
                ["Outcome"] = $"outcome_{i}",
                ["EffectSize"] = $"{i * 0.1:F1}",
                ["Significant"] = i % 2 == 0 ? "true" : "false"
            });
        }

        // Export
        var csvPath = TempFile();
        await _svc.ExportToCsvAsync(schema.Id, csvPath);

        File.Exists(csvPath).Should().BeTrue();
        var lines = File.ReadAllLines(csvPath);
        _output.WriteLine($"Exported {lines.Length} lines (incl header)");
        lines.Length.Should().Be(51); // header + 50 rows

        // Import into a new schema (re-create service with fresh row repo to simulate)
        var rowRepo2 = new FakeRepository<ExtractionRow>(r => r.Id, (r, id) => r.Id = id);
        var svc2 = new ExtractionService(_schemaRepo, rowRepo2, _refRepo);

        var imported = await svc2.ImportFromCsvAsync(schema.Id, csvPath, "ReferenceId");
        _output.WriteLine($"Imported {imported} rows");
        imported.Should().Be(50);

        // Verify values match
        var reimported = await svc2.GetExtractionAsync(25, schema.Id);
        reimported.Should().NotBeNull();
        reimported!.Values["Outcome"].Should().Be("outcome_25");
        reimported.Values["EffectSize"].Should().Be("2.5");
        reimported.Values["Significant"].Should().Be("false");
    }

    [Fact]
    public async Task CsvImport_InvalidRefIds_Skipped()
    {
        var columns = new List<ExtractionColumn>
        {
            new() { Name = "Value", Type = ExtractionColumnType.Text },
        };
        var schema = await _svc.CreateSchemaAsync(ProjectId, "Import Test", null, columns);

        var csvPath = TempFile();
        File.WriteAllText(csvPath, "ReferenceId,Value\n1,good\n,missing\nabc,bad\n2,also_good\n");

        var imported = await _svc.ImportFromCsvAsync(schema.Id, csvPath, "ReferenceId");
        imported.Should().Be(2);
    }

    [Fact]
    public async Task CsvImport_NonexistentSchema_Throws()
    {
        var csvPath = TempFile();
        File.WriteAllText(csvPath, "ReferenceId,Col1\n1,val\n");

        var act = async () => await _svc.ImportFromCsvAsync(999, csvPath, "ReferenceId");
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
