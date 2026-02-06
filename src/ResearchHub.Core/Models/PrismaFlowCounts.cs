namespace ResearchHub.Core.Models;

public class PrismaFlowCounts
{
    public PrismaIdentificationCounts Identification { get; set; } = new();
    public PrismaScreeningCounts Screening { get; set; } = new();
    public PrismaEligibilityCounts Eligibility { get; set; } = new();
    public PrismaInclusionCounts Inclusion { get; set; } = new();
}

public class PrismaIdentificationCounts
{
    public int RecordsIdentified { get; set; }
    public int DuplicatesRemoved { get; set; }
    public int RecordsAfterDuplicates { get; set; }
}

public class PrismaScreeningCounts
{
    public int RecordsScreened { get; set; }
    public int RecordsExcluded { get; set; }
}

public class PrismaEligibilityCounts
{
    public int FullTextAssessed { get; set; }
    public int FullTextExcluded { get; set; }
}

public class PrismaInclusionCounts
{
    public int StudiesIncluded { get; set; }
}
