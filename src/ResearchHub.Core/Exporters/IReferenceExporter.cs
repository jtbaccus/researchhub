using ResearchHub.Core.Models;

namespace ResearchHub.Core.Exporters;

public interface IReferenceExporter
{
    string Format { get; }
    string FileExtension { get; }
    string Export(IEnumerable<Reference> references);
    void ExportToFile(IEnumerable<Reference> references, string filePath);
}
