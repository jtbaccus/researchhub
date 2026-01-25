using CsvHelper;
using ResearchHub.Core.Models;
using System.Globalization;
using System.Text;

namespace ResearchHub.Core.Exporters;

public class CsvExporter : IReferenceExporter
{
    public string Format => "CSV";
    public string FileExtension => ".csv";

    public string Export(IEnumerable<Reference> references)
    {
        using var writer = new StringWriter();
        ExportToWriter(references, writer);
        return writer.ToString();
    }

    public void ExportToFile(IEnumerable<Reference> references, string filePath)
    {
        using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
        ExportToWriter(references, writer);
    }

    private void ExportToWriter(IEnumerable<Reference> references, TextWriter writer)
    {
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        // Write header
        csv.WriteField("Title");
        csv.WriteField("Authors");
        csv.WriteField("Abstract");
        csv.WriteField("Journal");
        csv.WriteField("Year");
        csv.WriteField("Volume");
        csv.WriteField("Issue");
        csv.WriteField("Pages");
        csv.WriteField("DOI");
        csv.WriteField("PMID");
        csv.WriteField("URL");
        csv.WriteField("Tags");
        csv.NextRecord();

        // Write records
        foreach (var reference in references)
        {
            csv.WriteField(reference.Title);
            csv.WriteField(string.Join("; ", reference.Authors));
            csv.WriteField(reference.Abstract);
            csv.WriteField(reference.Journal);
            csv.WriteField(reference.Year?.ToString());
            csv.WriteField(reference.Volume);
            csv.WriteField(reference.Issue);
            csv.WriteField(reference.Pages);
            csv.WriteField(reference.Doi);
            csv.WriteField(reference.Pmid);
            csv.WriteField(reference.Url);
            csv.WriteField(string.Join("; ", reference.Tags));
            csv.NextRecord();
        }
    }
}
