using ResearchHub.Core.Models;

namespace ResearchHub.Core.Parsers;

public interface IReferenceParser
{
    string Format { get; }
    string[] SupportedExtensions { get; }
    IEnumerable<Reference> Parse(string content);
    IEnumerable<Reference> ParseFile(string filePath);
}
