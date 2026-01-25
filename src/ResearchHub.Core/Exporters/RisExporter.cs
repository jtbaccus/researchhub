using ResearchHub.Core.Models;
using System.Text;

namespace ResearchHub.Core.Exporters;

public class RisExporter : IReferenceExporter
{
    public string Format => "RIS";
    public string FileExtension => ".ris";

    public string Export(IEnumerable<Reference> references)
    {
        var sb = new StringBuilder();

        foreach (var reference in references)
        {
            ExportReference(reference, sb);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public void ExportToFile(IEnumerable<Reference> references, string filePath)
    {
        var content = Export(references);
        File.WriteAllText(filePath, content, Encoding.UTF8);
    }

    private static void ExportReference(Reference reference, StringBuilder sb)
    {
        // Type - default to Journal Article
        sb.AppendLine("TY  - JOUR");

        // Title
        sb.AppendLine($"TI  - {reference.Title}");

        // Authors
        foreach (var author in reference.Authors)
        {
            sb.AppendLine($"AU  - {author}");
        }

        // Abstract
        if (!string.IsNullOrWhiteSpace(reference.Abstract))
            sb.AppendLine($"AB  - {reference.Abstract}");

        // Journal
        if (!string.IsNullOrWhiteSpace(reference.Journal))
            sb.AppendLine($"JO  - {reference.Journal}");

        // Year
        if (reference.Year.HasValue)
            sb.AppendLine($"PY  - {reference.Year}");

        // Volume
        if (!string.IsNullOrWhiteSpace(reference.Volume))
            sb.AppendLine($"VL  - {reference.Volume}");

        // Issue
        if (!string.IsNullOrWhiteSpace(reference.Issue))
            sb.AppendLine($"IS  - {reference.Issue}");

        // Pages
        if (!string.IsNullOrWhiteSpace(reference.Pages))
        {
            var pages = reference.Pages.Split('-');
            sb.AppendLine($"SP  - {pages[0].Trim()}");
            if (pages.Length > 1)
                sb.AppendLine($"EP  - {pages[1].Trim()}");
        }

        // DOI
        if (!string.IsNullOrWhiteSpace(reference.Doi))
            sb.AppendLine($"DO  - {reference.Doi}");

        // PMID
        if (!string.IsNullOrWhiteSpace(reference.Pmid))
            sb.AppendLine($"AN  - {reference.Pmid}");

        // URL
        if (!string.IsNullOrWhiteSpace(reference.Url))
            sb.AppendLine($"UR  - {reference.Url}");

        // Keywords
        foreach (var tag in reference.Tags)
        {
            sb.AppendLine($"KW  - {tag}");
        }

        // End of reference
        sb.AppendLine("ER  -");
    }
}
