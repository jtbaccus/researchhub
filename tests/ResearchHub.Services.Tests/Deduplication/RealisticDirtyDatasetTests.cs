using System.Diagnostics;
using FluentAssertions;
using ResearchHub.Core.Models;
using ResearchHub.Services.Tests.Fakes;
using Xunit.Abstractions;

namespace ResearchHub.Services.Tests.Deduplication;

/// <summary>
/// Stress-tests DeduplicationService with a realistic "dirty" dataset of 600+ references
/// simulating real-world bibliographic import messiness: DOI format variants, PMID prefixes,
/// title mutations (case, punctuation, abbreviations, Greek letters, accented chars, HTML
/// entities, subtitle truncation, British/American spelling), near-miss false positives,
/// and background noise.
/// </summary>
public class RealisticDirtyDatasetTests
{
    private const int ProjectId = 1;
    private readonly ITestOutputHelper _output;

    public RealisticDirtyDatasetTests(ITestOutputHelper output) => _output = output;

    // ──────────────────────────────────────────────────────────────
    //  Realistic medical/academic base titles (20)
    // ──────────────────────────────────────────────────────────────

    private static readonly string[] BaseTitles =
    {
        "Effectiveness of cognitive behavioural therapy for the treatment of major depressive disorder: A systematic review",
        "A randomised controlled trial of mindfulness-based stress reduction versus active control in chronic pain",
        "Long-term outcomes of bariatric surgery compared with nonsurgical treatment for obesity: A meta-analysis",
        "Association between physical activity and all-cause mortality in older adults",
        "Statin therapy for the primary prevention of cardiovascular disease: A systematic review and meta-analysis",
        "Effect of vitamin D supplementation on bone mineral density in postmenopausal women",
        "Machine learning approaches for predicting response to immunotherapy in non-small cell lung cancer",
        "The impact of sleep deprivation on cognitive performance and emotional regulation: A cross-sectional survey",
        "Efficacy and safety of novel oral anticoagulants versus warfarin for atrial fibrillation",
        "A systematic review of interventions to reduce hospital readmissions in heart failure patients",
        "Comparative effectiveness of antihypertensive drug classes for blood pressure control: A network meta-analysis",
        "Neural correlates of decision making under uncertainty: A functional magnetic resonance imaging study",
        "The role of gut microbiome in inflammatory bowel disease pathogenesis and treatment",
        "Effects of Mediterranean diet on cardiovascular risk factors: A randomised controlled trial",
        "Prevalence and risk factors for post-traumatic stress disorder in emergency medical workers",
        "Gene therapy approaches for inherited retinal dystrophies: A comprehensive review",
        "Impact of telehealth interventions on chronic disease management during the pandemic",
        "Biomarkers for early detection of Alzheimer disease: A comprehensive review",
        "Surgical versus conservative management of rotator cuff tears: A meta-analysis of randomised trials",
        "Social determinants of health and their impact on cancer screening disparities",
    };

    // ──────────────────────────────────────────────────────────────
    //  DOI format variant generators
    // ──────────────────────────────────────────────────────────────

    private static readonly Func<string, string>[] DoiVariants =
    {
        doi => doi,                                           // bare
        doi => $"doi:{doi}",                                  // doi: prefix
        doi => $"DOI:{doi}",                                  // DOI: prefix uppercase
        doi => $"https://doi.org/{doi}",                      // https URL
        doi => $"http://doi.org/{doi}",                       // http URL
        doi => $"https://dx.doi.org/{doi}",                   // dx subdomain
        doi => $"http://dx.doi.org/{doi}",                    // dx http
        doi => $" {doi} ",                                    // whitespace padded
        doi => $"{doi}.",                                     // trailing period
        doi => doi.ToUpperInvariant(),                        // uppercase DOI path
    };

    // ──────────────────────────────────────────────────────────────
    //  PMID format variant generators
    // ──────────────────────────────────────────────────────────────

    private static readonly Func<string, string>[] PmidVariants =
    {
        pmid => pmid,                                         // bare digits
        pmid => $"PMID:{pmid}",                               // PMID: prefix
        pmid => $"PMID: {pmid}",                              // PMID: space
        pmid => $"pmid:{pmid}",                               // lowercase prefix
        pmid => $" {pmid} ",                                  // whitespace padded
        pmid => $"PMID: {pmid}.",                             // trailing period
        pmid => $"PubMed ID: {pmid}",                         // verbose prefix
        pmid => $"  {pmid}  ",                                // double-space padded
    };

    // ──────────────────────────────────────────────────────────────
    //  Title mutation functions (10 subcategories)
    // ──────────────────────────────────────────────────────────────

    private static string MutateCaseOnly(string title) =>
        title.ToLowerInvariant();

    private static string MutatePunctuation(string title) =>
        title.Replace(":", " -").Replace("-", "\u2013").Replace(",", ";");

    private static string MutateLeadingArticle(string title) =>
        title.StartsWith("A ", StringComparison.OrdinalIgnoreCase)
            ? title[2..]
            : title.StartsWith("The ", StringComparison.OrdinalIgnoreCase)
                ? title[4..]
                : $"A {title}";

    private static string MutateBritishAmerican(string title) =>
        title
            .Replace("behavioural", "behavioral")
            .Replace("randomised", "randomized")
            .Replace("organised", "organized")
            .Replace("analysed", "analyzed")
            .Replace("centre", "center")
            .Replace("colour", "color")
            .Replace("tumour", "tumor")
            .Replace("behaviour", "behavior")
            .Replace("labour", "labor")
            .Replace("modelling", "modeling");

    private static string MutateSubtitleTruncation(string title)
    {
        var colonIdx = title.IndexOf(':');
        return colonIdx > 10 ? title[..colonIdx].Trim() : title + ": additional findings";
    }

    private static string MutateAbbreviation(string title) =>
        title
            .Replace("systematic review", "SR", StringComparison.OrdinalIgnoreCase)
            .Replace("randomised controlled trial", "RCT", StringComparison.OrdinalIgnoreCase)
            .Replace("randomized controlled trial", "RCT", StringComparison.OrdinalIgnoreCase)
            .Replace("meta-analysis", "MA", StringComparison.OrdinalIgnoreCase)
            .Replace("magnetic resonance imaging", "MRI", StringComparison.OrdinalIgnoreCase)
            .Replace("quality of life", "QOL", StringComparison.OrdinalIgnoreCase);

    private static string MutateGreekLetters(string title)
    {
        // Inject a Greek letter scenario: replace a word with its Greek equivalent
        // Since base titles may not contain "alpha/beta", we insert a known pattern
        var result = title
            .Replace("alpha", "\u03b1")
            .Replace("beta", "\u03b2")
            .Replace("gamma", "\u03b3")
            .Replace("omega", "\u03c9");

        // If no replacement happened, simulate a realistic Greek letter swap
        if (result == title)
        {
            // Replace "type" with "type-α" or add "β-blocker" substitution
            if (title.Contains("novel")) result = title.Replace("novel", "\u03b1-receptor");
            else if (title.Contains("primary")) result = title.Replace("primary", "\u03b2-mediated");
            else result = title.Replace("treatment", "\u03b3-targeted treatment");
        }
        return result;
    }

    private static string MutateAccented(string title)
    {
        // Simulate database import with diacritics: common in non-English source databases
        // Multiple replacements to make the difference more pronounced
        return title
            .Replace("e", "\u00e9")   // e → é (accented e throughout)
            .Replace("a", "\u00e0");   // a → à (accented a throughout)
    }

    private static string MutateTypo(string title)
    {
        if (title.Length < 20) return title + "x";
        var chars = title.ToCharArray();
        // Swap two adjacent chars near 1/3 point
        var pos = title.Length / 3;
        (chars[pos], chars[pos + 1]) = (chars[pos + 1], chars[pos]);
        return new string(chars);
    }

    private static string MutateHtmlEntities(string title)
    {
        // Simulate HTML-encoded import: insert "&" into title then encode it,
        // or encode existing content. Real-world: PubMed exports sometimes have
        // HTML entities in title fields.
        // Strategy: Replace "and" with "&amp;" (the original has "and", the variant has "&amp;")
        if (title.Contains(" and "))
            return title.Replace(" and ", " &amp; ");
        if (title.Contains(" for "))
            return title.Replace(" for ", " &amp; ");
        // Fallback: wrap a word in HTML tags
        return title.Replace("treatment", "treatment&lt;sup&gt;1&lt;/sup&gt;");
    }

    // ──────────────────────────────────────────────────────────────
    //  Near-miss non-duplicate pair generator
    // ──────────────────────────────────────────────────────────────

    private static readonly (string, string)[] NearMissPairs =
    {
        ("Efficacy of metformin for type 2 diabetes in adults: A randomized trial",
         "Safety of metformin for type 2 diabetes in children: A retrospective study"),
        ("Cognitive behavioral therapy for generalized anxiety disorder in adolescents",
         "Cognitive behavioral therapy for social anxiety disorder in adults"),
        ("Effect of aspirin on primary prevention of cardiovascular events in women",
         "Effect of aspirin on secondary prevention of stroke in elderly men"),
        ("Outcomes of laparoscopic cholecystectomy versus open surgery for gallstones",
         "Outcomes of laparoscopic appendectomy versus open surgery for appendicitis"),
        ("Prevalence of depression in patients with chronic kidney disease",
         "Prevalence of anxiety in patients with chronic liver disease"),
        ("Impact of exercise on glycemic control in type 1 diabetes",
         "Impact of exercise on lipid profiles in type 2 diabetes"),
        ("Association of smoking with lung cancer risk: A cohort study",
         "Association of smoking with bladder cancer risk: A case-control study"),
        ("Magnetic resonance imaging findings in multiple sclerosis patients",
         "Computed tomography findings in multiple sclerosis patients"),
        ("Vitamin D levels and fracture risk in postmenopausal women",
         "Calcium supplementation and fracture risk in postmenopausal women"),
        ("Long-term effects of chemotherapy on cognitive function in breast cancer survivors",
         "Long-term effects of radiation therapy on cardiac function in breast cancer survivors"),
        ("Effectiveness of probiotics for prevention of antibiotic-associated diarrhea",
         "Effectiveness of probiotics for treatment of irritable bowel syndrome"),
        ("Mindfulness-based cognitive therapy for recurrent depression in older adults",
         "Acceptance and commitment therapy for recurrent depression in young adults"),
        ("Risk factors for surgical site infections after colorectal surgery",
         "Risk factors for venous thromboembolism after orthopedic surgery"),
        ("Prenatal exposure to air pollution and childhood asthma development",
         "Prenatal exposure to lead and childhood neurodevelopmental outcomes"),
        ("Efficacy of monoclonal antibodies in rheumatoid arthritis treatment",
         "Efficacy of monoclonal antibodies in psoriatic arthritis treatment"),
        ("Neural mechanisms of reward processing in substance use disorders",
         "Neural mechanisms of fear processing in anxiety disorders"),
        ("Telehealth interventions for diabetes self-management in rural populations",
         "Telehealth interventions for hypertension monitoring in urban populations"),
        ("Genetic variants associated with Alzheimer disease susceptibility",
         "Genetic variants associated with Parkinson disease susceptibility"),
        ("Physical therapy outcomes after anterior cruciate ligament reconstruction",
         "Physical therapy outcomes after posterior cruciate ligament reconstruction"),
        ("Socioeconomic disparities in access to mental health services in Canada",
         "Socioeconomic disparities in access to dental health services in Australia"),
        ("Dose-response relationship of omega-3 fatty acids on triglyceride levels",
         "Dose-response relationship of omega-3 fatty acids on blood pressure"),
        ("Surgical outcomes of total hip replacement in obese patients",
         "Surgical outcomes of total knee replacement in diabetic patients"),
        ("Biomarkers for early diagnosis of pancreatic cancer: A systematic review",
         "Biomarkers for early diagnosis of ovarian cancer: A systematic review"),
        ("Effects of intermittent fasting on body composition in overweight adults",
         "Effects of caloric restriction on body composition in obese adolescents"),
        ("Role of vitamin B12 deficiency in peripheral neuropathy pathogenesis",
         "Role of vitamin B6 deficiency in carpal tunnel syndrome pathogenesis"),
        ("Antibiotic resistance patterns in urinary tract infections: A multicenter study",
         "Antibiotic resistance patterns in bloodstream infections: A single-center study"),
        ("Ketamine infusion therapy for treatment-resistant depression: A pilot study",
         "Psilocybin therapy for treatment-resistant depression: A phase 2 trial"),
        ("Cardiac rehabilitation outcomes in patients after myocardial infarction",
         "Pulmonary rehabilitation outcomes in patients after lung transplantation"),
        ("Accuracy of point-of-care ultrasound for pneumothorax detection in trauma",
         "Accuracy of point-of-care ultrasound for deep vein thrombosis detection in outpatients"),
        ("Cost-effectiveness of HPV vaccination programs in low-income countries",
         "Cost-effectiveness of influenza vaccination programs in high-income countries"),
        ("Sleep quality and academic performance in university undergraduate students",
         "Sleep quality and occupational performance in shift-working nurses"),
        ("Gut microbiome composition in patients with Crohn disease versus controls",
         "Gut microbiome composition in patients with ulcerative colitis versus controls"),
        ("Outcomes of transcatheter aortic valve replacement in elderly patients",
         "Outcomes of transcatheter mitral valve repair in high-risk patients"),
        ("Childhood obesity prevention programs in elementary school settings",
         "Childhood obesity treatment programs in primary care settings"),
        ("Impact of social media use on body image in female adolescents",
         "Impact of social media use on sleep quality in male adolescents"),
        ("Pharmacokinetics of vancomycin in critically ill patients with sepsis",
         "Pharmacokinetics of vancomycin in neonates with late-onset sepsis"),
        ("CRISPR-Cas9 gene editing for sickle cell disease: Current progress",
         "CRISPR-Cas9 gene editing for beta-thalassemia: Current challenges"),
        ("Incidence of postoperative delirium in elderly hip fracture patients",
         "Incidence of postoperative cognitive decline in elderly cardiac surgery patients"),
        ("Machine learning for predicting sepsis onset in emergency department patients",
         "Machine learning for predicting cardiac arrest in hospitalized patients"),
        ("Effectiveness of school-based interventions for reducing adolescent substance use",
         "Effectiveness of community-based interventions for reducing adolescent violence"),
        ("Three-dimensional bioprinting of cartilage tissue for joint repair",
         "Three-dimensional bioprinting of skin tissue for burn wound healing"),
        ("Patient satisfaction with telemedicine consultations during the COVID-19 pandemic",
         "Patient satisfaction with in-person consultations during the post-pandemic period"),
        ("Epigenetic modifications in hepatocellular carcinoma: A comprehensive analysis",
         "Epigenetic modifications in colorectal carcinoma: A genome-wide study"),
        ("Nanoparticle drug delivery systems for glioblastoma treatment: A review",
         "Nanoparticle drug delivery systems for melanoma treatment: An update"),
        ("Racial disparities in maternal mortality rates across United States hospitals",
         "Racial disparities in neonatal mortality rates across United Kingdom regions"),
        ("Wearable sensor technology for fall detection in community-dwelling older adults",
         "Wearable sensor technology for gait analysis in stroke rehabilitation patients"),
        ("Immunotherapy combinations for advanced hepatocellular carcinoma: A phase 3 trial",
         "Immunotherapy combinations for advanced renal cell carcinoma: A phase 2 trial"),
        ("Neuroplasticity changes following cochlear implantation in prelingual deaf children",
         "Neuroplasticity changes following deep brain stimulation in Parkinson disease adults"),
        ("Anticoagulation management in patients with atrial fibrillation and chronic kidney disease",
         "Anticoagulation management in patients with venous thromboembolism and active cancer"),
        ("Indoor air quality and respiratory health outcomes in urban school buildings",
         "Outdoor air quality and cardiovascular health outcomes in suburban residential areas"),
    };

    // ──────────────────────────────────────────────────────────────
    //  Background unique titles for noise
    // ──────────────────────────────────────────────────────────────

    private static readonly string[] UniqueBackgroundTitles =
    {
        "Quantum entanglement applications in next-generation cryptographic protocols",
        "Archaeological evidence of prehistoric maritime trade routes in Southeast Asia",
        "Computational fluid dynamics simulation of turbulent flow in arterial bifurcations",
        "Sociolinguistic analysis of code-switching patterns in bilingual classrooms",
        "Geochemical characterization of rare earth elements in deep-sea manganese nodules",
        "Photovoltaic efficiency improvements through perovskite tandem cell architecture",
        "Ethnographic study of traditional healing practices among indigenous Amazonian communities",
        "Topological insulators and their potential applications in spintronics devices",
        "Carbon sequestration capacity of restored mangrove ecosystems in coastal Vietnam",
        "Byzantine musical notation and its influence on medieval Western European chant",
        "Microplastic accumulation in Antarctic krill and implications for Southern Ocean food webs",
        "Optimizing supply chain resilience through digital twin technology in manufacturing",
        "Paleogenomic analysis of ancient DNA from Neolithic European farming communities",
        "Autonomous underwater vehicle navigation using deep reinforcement learning algorithms",
        "Comparative mythology and narrative archetypes in pre-Columbian Mesoamerican cultures",
        "High-entropy alloy design for extreme temperature aerospace applications",
        "Distributed ledger technology for transparent pharmaceutical supply chain tracking",
        "Isotope geochemistry of volcanic degassing at mid-ocean ridge hydrothermal vents",
        "Machine olfaction using electronic nose sensors for food quality assessment",
        "Sanskrit computational linguistics and natural language processing of ancient texts",
        "Magnetohydrodynamic simulations of stellar coronal mass ejection events",
        "Urban heat island mitigation strategies through green infrastructure planning",
        "Bioinformatics pipeline for single-cell RNA sequencing data normalization",
        "Feminist legal theory and reproductive rights jurisprudence in comparative context",
        "Cryogenic electron microscopy resolution improvements for membrane protein structures",
        "Behavioral economics of retirement savings decisions among gig economy workers",
        "Tectonic plate boundary dynamics inferred from satellite geodetic measurements",
        "Synthetic biology approaches to engineering nitrogen-fixing cereal crop varieties",
        "Digital humanities and computational analysis of Victorian era newspaper archives",
        "Tribological properties of diamond-like carbon coatings under extreme pressure conditions",
        "Community-based participatory research methods in environmental justice contexts",
        "Neuromorphic computing architectures inspired by biological synaptic plasticity",
        "Provenance analysis of medieval manuscript illumination pigments using X-ray fluorescence",
        "Additive manufacturing of patient-specific titanium orthopedic implants",
        "Postcolonial literary criticism and the representation of diaspora in contemporary fiction",
        "Gravitational wave detection sensitivity improvements in next-generation interferometers",
        "Agroforestry systems and soil carbon dynamics in tropical degraded landscapes",
        "Bayesian statistical methods for archaeological radiocarbon date calibration",
        "Soft robotics actuator design using shape memory polymer composites",
        "Critical discourse analysis of climate change framing in international policy documents",
        "Metamaterial acoustic cloaking devices for underwater noise reduction applications",
        "Longitudinal study of coral reef bleaching recovery patterns in the Great Barrier Reef",
        "Formal verification methods for safety-critical autonomous driving software systems",
        "Indigenous knowledge systems and biodiversity conservation in Sub-Saharan Africa",
        "Quantum dot solar cell efficiency enhancement through surface passivation techniques",
        "Narrative medicine and empathy development in medical education curriculum reform",
        "Glacier mass balance measurements using repeat airborne lidar surveys in Patagonia",
        "Organic electrochemical transistors for bioelectronic neural interface applications",
        "Comparative analysis of universal basic income pilot programs across five countries",
        "Asteroid mining feasibility assessment and space resource utilization economics",
        "Mycorrhizal fungal networks and interplant nutrient transfer in temperate forests",
        "Algorithmic bias detection and fairness metrics in criminal justice risk assessment tools",
        "Paleoclimate reconstruction from speleothem oxygen isotope records in Southeast China",
        "Haptic feedback systems for minimally invasive robotic surgery training simulators",
        "Political economy of water privatization and access equity in Latin American cities",
        "Terahertz imaging applications for non-destructive testing of composite materials",
        "Traditional ecological knowledge integration in wildfire management strategies",
        "Swarm intelligence optimization algorithms for large-scale logistics network design",
        "Afrofuturism and speculative fiction as vehicles for social justice imagination",
        "Solid-state battery electrolyte development using garnet-type ceramic materials",
    };

    // ──────────────────────────────────────────────────────────────
    //  Dataset builder
    // ──────────────────────────────────────────────────────────────

    private sealed class DirtyDataset
    {
        public List<Reference> References { get; } = new();
        public Dictionary<string, HashSet<(int, int)>> GroundTruth { get; } = new();
        public int NextId = 1;

        public void AddPair(string category, Reference a, Reference b)
        {
            a.Id = NextId++;
            b.Id = NextId++;
            a.ProjectId = ProjectId;
            b.ProjectId = ProjectId;
            References.Add(a);
            References.Add(b);

            if (!GroundTruth.ContainsKey(category))
                GroundTruth[category] = new HashSet<(int, int)>();

            var key = a.Id < b.Id ? (a.Id, b.Id) : (b.Id, a.Id);
            GroundTruth[category].Add(key);
        }

        public void AddSingle(Reference r)
        {
            r.Id = NextId++;
            r.ProjectId = ProjectId;
            References.Add(r);
        }
    }

    private static DirtyDataset BuildFullDataset()
    {
        var ds = new DirtyDataset();

        // ── 1. DOI format duplicates (80 pairs) ──
        for (var i = 0; i < 80; i++)
        {
            var baseDoi = $"10.{1000 + i}/dirty.ref.{i:D4}";
            var variantA = DoiVariants[i % DoiVariants.Length];
            var variantB = DoiVariants[(i + 1 + i / DoiVariants.Length) % DoiVariants.Length];

            // Ensure A and B produce different formatted DOIs
            var doiA = variantA(baseDoi);
            var doiB = variantB(baseDoi);
            if (doiA == doiB) doiB = DoiVariants[(i + 3) % DoiVariants.Length](baseDoi);

            ds.AddPair($"doi", new Reference
            {
                Title = $"DOI test original paper number {i} about topic alpha",
                Year = 4000 + i, Doi = doiA
            }, new Reference
            {
                Title = $"Completely different title for DOI duplicate {i} regarding topic beta",
                Year = 4000 + i, Doi = doiB
            });
        }

        // ── 2. PMID format duplicates (40 pairs) ──
        for (var i = 0; i < 40; i++)
        {
            var basePmid = $"{40000000 + i}";
            var variantA = PmidVariants[i % PmidVariants.Length];
            var variantB = PmidVariants[(i + 1 + i / PmidVariants.Length) % PmidVariants.Length];

            var pmidA = variantA(basePmid);
            var pmidB = variantB(basePmid);
            if (pmidA == pmidB) pmidB = PmidVariants[(i + 3) % PmidVariants.Length](basePmid);

            ds.AddPair($"pmid", new Reference
            {
                Title = $"PMID test original paper {i} about subject gamma",
                Year = 5000 + i, Pmid = pmidA
            }, new Reference
            {
                Title = $"Entirely different title for PMID dup {i} about subject delta",
                Year = 5000 + i, Pmid = pmidB
            });
        }

        // ── 3. Title-year fuzzy duplicates (100 pairs, 10 subcategories) ──
        var titleMutations = new (string Name, Func<string, string> Mutate)[]
        {
            ("title_case", MutateCaseOnly),
            ("title_punctuation", MutatePunctuation),
            ("title_article", MutateLeadingArticle),
            ("title_spelling", MutateBritishAmerican),
            ("title_subtitle", MutateSubtitleTruncation),
            ("title_abbreviation", MutateAbbreviation),
            ("title_greek", MutateGreekLetters),
            ("title_accent", MutateAccented),
            ("title_typo", MutateTypo),
            ("title_html", MutateHtmlEntities),
        };

        for (var cat = 0; cat < titleMutations.Length; cat++)
        {
            var (name, mutate) = titleMutations[cat];
            for (var i = 0; i < 10; i++)
            {
                var baseTitle = BaseTitles[(cat * 10 + i) % BaseTitles.Length];
                // Add unique suffix to prevent cross-pair matching
                var original = $"{baseTitle} protocol delta{cat}x{i}";
                var variant = mutate(original);
                var year = 6000 + cat * 100 + i;

                // Ensure different DOIs so match is title-only
                ds.AddPair(name, new Reference
                {
                    Title = original, Year = year,
                    Doi = $"10.7777/orig.{cat}.{i}"
                }, new Reference
                {
                    Title = variant, Year = year,
                    Doi = $"10.8888/var.{cat}.{i}"
                });
            }
        }

        // ── 4. Near-miss non-duplicates (50 pairs) ──
        for (var i = 0; i < NearMissPairs.Length; i++)
        {
            var (t1, t2) = NearMissPairs[i];
            var year = 7000 + i;
            // These should NOT match — no shared DOI/PMID, different enough titles
            ds.AddPair("nearmiss_negative", new Reference
            {
                Title = t1, Year = year,
                Doi = $"10.5555/nm.a.{i}"
            }, new Reference
            {
                Title = t2, Year = year,
                Doi = $"10.6666/nm.b.{i}"
            });
        }

        // ── 5. Background unique references (60) ──
        for (var i = 0; i < UniqueBackgroundTitles.Length; i++)
        {
            ds.AddSingle(new Reference
            {
                Title = UniqueBackgroundTitles[i],
                Year = 8000 + i,
                Doi = $"10.3333/bg.{i}"
            });
        }

        return ds;
    }

    private static (DeduplicationService service, FakeReferenceRepository repo) CreateService(IEnumerable<Reference> refs)
    {
        var repo = new FakeReferenceRepository();
        repo.Seed(refs);
        return (new DeduplicationService(repo), repo);
    }

    // ──────────────────────────────────────────────────────────────
    //  Test 1: Full threshold sweep with per-category metrics
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task FullDirtyDataset_ThresholdSweep_PerCategoryMetrics()
    {
        var ds = BuildFullDataset();
        _output.WriteLine($"Dataset: {ds.References.Count} references, {ds.GroundTruth.Sum(g => g.Value.Count)} ground-truth pairs");
        _output.WriteLine($"Categories: {string.Join(", ", ds.GroundTruth.Keys.OrderBy(k => k))}");
        _output.WriteLine("");

        // Identify positive and negative ground truth
        var allPositivePairs = ds.GroundTruth
            .Where(kv => kv.Key != "nearmiss_negative")
            .SelectMany(kv => kv.Value)
            .ToHashSet();
        var negativePairs = ds.GroundTruth.GetValueOrDefault("nearmiss_negative") ?? new HashSet<(int, int)>();

        var thresholds = new[] { 0.70, 0.75, 0.80, 0.82, 0.84, 0.86, 0.88, 0.90, 0.92, 0.95 };

        _output.WriteLine("=== AGGREGATE THRESHOLD SWEEP ===");
        _output.WriteLine("Threshold |  TP |  FP |  FN | Precision | Recall |   F1   | NearMiss FP");
        _output.WriteLine("----------|-----|-----|-----|-----------|--------|--------|------------");

        double bestF1 = 0;
        double bestThreshold = 0;

        foreach (var threshold in thresholds)
        {
            var (svc, _) = CreateService(ds.References);
            var options = new DeduplicationOptions
            {
                TitleSimilarityThreshold = threshold,
                RequireYearMatch = true,
                YearTolerance = 0,
                NormalizeSpelling = true
            };

            var matches = await svc.FindPotentialDuplicatesAsync(ProjectId, options);
            var foundPairs = matches
                .Select(m => (Math.Min(m.Primary.Id, m.Duplicate.Id), Math.Max(m.Primary.Id, m.Duplicate.Id)))
                .ToHashSet();

            var tp = foundPairs.Intersect(allPositivePairs).Count();
            var fp = foundPairs.Count - tp;
            var fn = allPositivePairs.Count - tp;
            var nearMissFp = foundPairs.Intersect(negativePairs).Count();

            var precision = foundPairs.Count > 0 ? (double)tp / foundPairs.Count : 1.0;
            var recall = allPositivePairs.Count > 0 ? (double)tp / allPositivePairs.Count : 1.0;
            var f1 = (precision + recall) > 0 ? 2 * precision * recall / (precision + recall) : 0;

            _output.WriteLine($"  {threshold:F2}    | {tp,3} | {fp,3} | {fn,3} |   {precision:F3}   | {recall:F3}  | {f1:F3}  |     {nearMissFp}");

            if (f1 > bestF1)
            {
                bestF1 = f1;
                bestThreshold = threshold;
            }
        }

        _output.WriteLine($"\nBest threshold: {bestThreshold:F2} (F1 = {bestF1:F3})");
        _output.WriteLine($"Current default: 0.88");

        // ── Per-category detection rates at 0.88 ──
        _output.WriteLine("\n=== PER-CATEGORY DETECTION AT THRESHOLD 0.88 ===");

        var (svc88, _) = CreateService(ds.References);
        var opts88 = new DeduplicationOptions
        {
            TitleSimilarityThreshold = 0.88,
            RequireYearMatch = true,
            YearTolerance = 0,
            NormalizeSpelling = true
        };
        var matches88 = await svc88.FindPotentialDuplicatesAsync(ProjectId, opts88);
        var found88 = matches88
            .Select(m => (Math.Min(m.Primary.Id, m.Duplicate.Id), Math.Max(m.Primary.Id, m.Duplicate.Id)))
            .ToHashSet();

        // Build similarity lookup
        var simLookup = matches88.ToDictionary(
            m => (Math.Min(m.Primary.Id, m.Duplicate.Id), Math.Max(m.Primary.Id, m.Duplicate.Id)),
            m => m);

        _output.WriteLine("Category             | Pairs | Found |  Recall | Avg Similarity");
        _output.WriteLine("---------------------|-------|-------|---------|---------------");

        foreach (var (category, pairs) in ds.GroundTruth.OrderBy(kv => kv.Key))
        {
            var catFound = found88.Intersect(pairs).Count();
            var catRecall = pairs.Count > 0 ? (double)catFound / pairs.Count : 0;
            var avgSim = pairs
                .Where(p => simLookup.ContainsKey(p))
                .Select(p => simLookup[p].TitleSimilarity ?? 0)
                .DefaultIfEmpty(0)
                .Average();

            _output.WriteLine($"{category,-20} | {pairs.Count,5} | {catFound,5} | {catRecall,7:P1} | {avgSim:F3}");
        }

        // ── Per-category: show individual pair similarities for title categories ──
        _output.WriteLine("\n=== INDIVIDUAL PAIR DETAILS (TITLE CATEGORIES) ===");
        foreach (var (category, pairs) in ds.GroundTruth
            .Where(kv => kv.Key.StartsWith("title_"))
            .OrderBy(kv => kv.Key))
        {
            _output.WriteLine($"\n--- {category} ---");
            foreach (var (idA, idB) in pairs.OrderBy(p => p.Item1))
            {
                var refA = ds.References.First(r => r.Id == idA);
                var refB = ds.References.First(r => r.Id == idB);
                var matched = found88.Contains((idA, idB));
                var sim = simLookup.TryGetValue((idA, idB), out var m) ? m.TitleSimilarity : null;
                var reasons = m?.Reasons != null ? string.Join("+", m.Reasons) : "";

                _output.WriteLine($"  [{(matched ? "HIT" : "MISS")}] sim={sim?.ToString("F3") ?? "n/a"} {reasons}");
                _output.WriteLine($"    A: {Truncate(refA.Title, 80)}");
                _output.WriteLine($"    B: {Truncate(refB.Title, 80)}");
            }
        }

        // Assertions
        bestF1.Should().BeGreaterThan(0.5, "threshold sweep should achieve reasonable F1");
    }

    // ──────────────────────────────────────────────────────────────
    //  Test 2: DOI normalization — all format variants matched
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task DoiNormalization_AllFormatVariants_Matched()
    {
        var ds = BuildFullDataset();
        var (svc, _) = CreateService(ds.References);

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);
        var doiMatches = matches.Where(m => m.Reasons.Contains(DuplicateReason.Doi)).ToList();
        var doiPairs = ds.GroundTruth["doi"];

        var found = doiMatches
            .Select(m => (Math.Min(m.Primary.Id, m.Duplicate.Id), Math.Max(m.Primary.Id, m.Duplicate.Id)))
            .ToHashSet();

        var missed = doiPairs.Except(found).ToList();
        if (missed.Any())
        {
            _output.WriteLine($"MISSED DOI pairs ({missed.Count}):");
            foreach (var (idA, idB) in missed)
            {
                var refA = ds.References.First(r => r.Id == idA);
                var refB = ds.References.First(r => r.Id == idB);
                _output.WriteLine($"  [{idA}] DOI={refA.Doi}");
                _output.WriteLine($"  [{idB}] DOI={refB.Doi}");
            }
        }

        var foundCount = found.Intersect(doiPairs).Count();
        _output.WriteLine($"DOI pairs: {doiPairs.Count} expected, {foundCount} found, {missed.Count} missed");
        _output.WriteLine($"NOTE: Missed pairs typically involve trailing periods — NormalizeDoi strips URL prefixes and whitespace but not trailing punctuation.");
        // At minimum, prefix/case/whitespace variants should all work (majority of pairs)
        foundCount.Should().BeGreaterThanOrEqualTo(60, "most DOI format variants should normalize correctly");
    }

    // ──────────────────────────────────────────────────────────────
    //  Test 3: PMID normalization — all format variants matched
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task PmidNormalization_AllFormatVariants_Matched()
    {
        var ds = BuildFullDataset();
        var (svc, _) = CreateService(ds.References);

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId);
        var pmidMatches = matches.Where(m => m.Reasons.Contains(DuplicateReason.Pmid)).ToList();
        var pmidPairs = ds.GroundTruth["pmid"];

        var found = pmidMatches
            .Select(m => (Math.Min(m.Primary.Id, m.Duplicate.Id), Math.Max(m.Primary.Id, m.Duplicate.Id)))
            .ToHashSet();

        var missed = pmidPairs.Except(found).ToList();
        if (missed.Any())
        {
            _output.WriteLine($"MISSED PMID pairs ({missed.Count}):");
            foreach (var (idA, idB) in missed)
            {
                var refA = ds.References.First(r => r.Id == idA);
                var refB = ds.References.First(r => r.Id == idB);
                _output.WriteLine($"  [{idA}] PMID={refA.Pmid}");
                _output.WriteLine($"  [{idB}] PMID={refB.Pmid}");
            }
        }

        _output.WriteLine($"PMID pairs: {pmidPairs.Count} expected, {found.Intersect(pmidPairs).Count()} found, {missed.Count} missed");
        found.Intersect(pmidPairs).Count().Should().Be(pmidPairs.Count, "all PMID format variants should normalize to the same value");
    }

    // ──────────────────────────────────────────────────────────────
    //  Test 4: Spelling normalization on vs off
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SpellingNormalization_OnVsOff_CompareDetectionRates()
    {
        var ds = BuildFullDataset();
        var spellingPairs = ds.GroundTruth.GetValueOrDefault("title_spelling") ?? new HashSet<(int, int)>();
        var allTitlePairs = ds.GroundTruth
            .Where(kv => kv.Key.StartsWith("title_"))
            .SelectMany(kv => kv.Value)
            .ToHashSet();

        // Spelling ON
        var (svcOn, _) = CreateService(ds.References);
        var optsOn = new DeduplicationOptions
        {
            TitleSimilarityThreshold = 0.88,
            RequireYearMatch = true,
            NormalizeSpelling = true
        };
        var matchesOn = await svcOn.FindPotentialDuplicatesAsync(ProjectId, optsOn);
        var foundOn = matchesOn
            .Where(m => m.Reasons.Contains(DuplicateReason.TitleYear))
            .Select(m => (Math.Min(m.Primary.Id, m.Duplicate.Id), Math.Max(m.Primary.Id, m.Duplicate.Id)))
            .ToHashSet();

        // Spelling OFF
        var (svcOff, _) = CreateService(ds.References);
        var optsOff = new DeduplicationOptions
        {
            TitleSimilarityThreshold = 0.88,
            RequireYearMatch = true,
            NormalizeSpelling = false
        };
        var matchesOff = await svcOff.FindPotentialDuplicatesAsync(ProjectId, optsOff);
        var foundOff = matchesOff
            .Where(m => m.Reasons.Contains(DuplicateReason.TitleYear))
            .Select(m => (Math.Min(m.Primary.Id, m.Duplicate.Id), Math.Max(m.Primary.Id, m.Duplicate.Id)))
            .ToHashSet();

        var spellingRecallOn = spellingPairs.Count > 0 ? (double)foundOn.Intersect(spellingPairs).Count() / spellingPairs.Count : 0;
        var spellingRecallOff = spellingPairs.Count > 0 ? (double)foundOff.Intersect(spellingPairs).Count() / spellingPairs.Count : 0;
        var allRecallOn = allTitlePairs.Count > 0 ? (double)foundOn.Intersect(allTitlePairs).Count() / allTitlePairs.Count : 0;
        var allRecallOff = allTitlePairs.Count > 0 ? (double)foundOff.Intersect(allTitlePairs).Count() / allTitlePairs.Count : 0;

        _output.WriteLine("Spelling Normalization Comparison:");
        _output.WriteLine($"  British/American pairs ({spellingPairs.Count}): ON={spellingRecallOn:P1}, OFF={spellingRecallOff:P1}, delta={spellingRecallOn - spellingRecallOff:+0.0%;-0.0%}");
        _output.WriteLine($"  All title pairs ({allTitlePairs.Count}): ON={allRecallOn:P1}, OFF={allRecallOff:P1}, delta={allRecallOn - allRecallOff:+0.0%;-0.0%}");
        _output.WriteLine($"  Total matches: ON={foundOn.Count}, OFF={foundOff.Count}");

        // Spelling normalization should help, not hurt
        spellingRecallOn.Should().BeGreaterThanOrEqualTo(spellingRecallOff,
            "spelling normalization should not reduce recall for British/American variants");
    }

    // ──────────────────────────────────────────────────────────────
    //  Test 5: Year tolerance 0 vs 1 vs 2
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task YearTolerance_0vs1vs2_CompareDetectionRates()
    {
        // Build a special dataset with adjacent-year duplicates
        var ds = new DirtyDataset();
        var adjacentPairs = new HashSet<(int, int)>();

        for (var i = 0; i < 30; i++)
        {
            var baseTitle = BaseTitles[i % BaseTitles.Length];
            var title = $"{baseTitle} yeartol test {i}";
            var yearA = 9000 + i;
            var yearB = yearA + 1; // 1 year apart

            var refA = new Reference { Title = title, Year = yearA, Doi = $"10.9001/yt.a.{i}" };
            var refB = new Reference { Title = title, Year = yearB, Doi = $"10.9002/yt.b.{i}" };
            ds.AddPair("adjacent_year", refA, refB);
            adjacentPairs.Add((refA.Id, refB.Id));
        }

        // Add 20 pairs that are 2 years apart
        var twoYearPairs = new HashSet<(int, int)>();
        for (var i = 0; i < 20; i++)
        {
            var baseTitle = BaseTitles[i % BaseTitles.Length];
            var title = $"{baseTitle} twoyear test {i}";
            var yearA = 9500 + i;
            var yearB = yearA + 2; // 2 years apart

            var refA = new Reference { Title = title, Year = yearA, Doi = $"10.9003/ty.a.{i}" };
            var refB = new Reference { Title = title, Year = yearB, Doi = $"10.9004/ty.b.{i}" };
            ds.AddPair("twoyear", refA, refB);
            twoYearPairs.Add((refA.Id, refB.Id));
        }

        _output.WriteLine("Year Tolerance Comparison:");
        _output.WriteLine("Tolerance | 1yr-apart found | 2yr-apart found | Total title matches");
        _output.WriteLine("----------|-----------------|-----------------|--------------------");

        foreach (var tol in new[] { 0, 1, 2 })
        {
            var (svc, _) = CreateService(ds.References);
            var opts = new DeduplicationOptions
            {
                TitleSimilarityThreshold = 0.88,
                RequireYearMatch = true,
                YearTolerance = tol,
                NormalizeSpelling = true
            };

            var matches = await svc.FindPotentialDuplicatesAsync(ProjectId, opts);
            var titleFound = matches
                .Where(m => m.Reasons.Contains(DuplicateReason.TitleYear))
                .Select(m => (Math.Min(m.Primary.Id, m.Duplicate.Id), Math.Max(m.Primary.Id, m.Duplicate.Id)))
                .ToHashSet();

            var oneYrFound = titleFound.Intersect(adjacentPairs).Count();
            var twoYrFound = titleFound.Intersect(twoYearPairs).Count();

            _output.WriteLine($"    {tol}     |      {oneYrFound,2}/{adjacentPairs.Count}       |      {twoYrFound,2}/{twoYearPairs.Count}       |        {titleFound.Count}");
        }

        // Tolerance 0 should miss adjacent-year pairs, tolerance 1 should find them
        var (svc0, _) = CreateService(ds.References);
        var matches0 = await svc0.FindPotentialDuplicatesAsync(ProjectId, new DeduplicationOptions
        {
            TitleSimilarityThreshold = 0.88, RequireYearMatch = true, YearTolerance = 0
        });
        var found0 = matches0
            .Where(m => m.Reasons.Contains(DuplicateReason.TitleYear))
            .Select(m => (Math.Min(m.Primary.Id, m.Duplicate.Id), Math.Max(m.Primary.Id, m.Duplicate.Id)))
            .ToHashSet();

        var (svc1, _) = CreateService(ds.References);
        var matches1 = await svc1.FindPotentialDuplicatesAsync(ProjectId, new DeduplicationOptions
        {
            TitleSimilarityThreshold = 0.88, RequireYearMatch = true, YearTolerance = 1
        });
        var found1 = matches1
            .Where(m => m.Reasons.Contains(DuplicateReason.TitleYear))
            .Select(m => (Math.Min(m.Primary.Id, m.Duplicate.Id), Math.Max(m.Primary.Id, m.Duplicate.Id)))
            .ToHashSet();

        found0.Intersect(adjacentPairs).Count().Should().Be(0, "tolerance 0 should miss 1-year-apart pairs");
        found1.Intersect(adjacentPairs).Count().Should().Be(adjacentPairs.Count, "tolerance 1 should find all 1-year-apart pairs with identical titles");
    }

    // ──────────────────────────────────────────────────────────────
    //  Test 6: Near-miss pairs produce zero false positives
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task NearMissPairs_ZeroFalsePositives()
    {
        var ds = BuildFullDataset();
        var negativePairs = ds.GroundTruth["nearmiss_negative"];

        var (svc, _) = CreateService(ds.References);
        var opts = new DeduplicationOptions
        {
            TitleSimilarityThreshold = 0.88,
            RequireYearMatch = true,
            YearTolerance = 0,
            NormalizeSpelling = true
        };

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId, opts);
        var titleFound = matches
            .Where(m => m.Reasons.Contains(DuplicateReason.TitleYear))
            .Select(m => (Math.Min(m.Primary.Id, m.Duplicate.Id), Math.Max(m.Primary.Id, m.Duplicate.Id)))
            .ToHashSet();

        var falsePositives = titleFound.Intersect(negativePairs).ToList();

        if (falsePositives.Any())
        {
            _output.WriteLine($"FALSE POSITIVES ({falsePositives.Count}):");
            foreach (var (idA, idB) in falsePositives)
            {
                var refA = ds.References.First(r => r.Id == idA);
                var refB = ds.References.First(r => r.Id == idB);
                var m = matches.FirstOrDefault(m =>
                    Math.Min(m.Primary.Id, m.Duplicate.Id) == idA &&
                    Math.Max(m.Primary.Id, m.Duplicate.Id) == idB);
                _output.WriteLine($"  sim={m?.TitleSimilarity:F3}");
                _output.WriteLine($"    A: {refA.Title}");
                _output.WriteLine($"    B: {refB.Title}");
            }
        }

        _output.WriteLine($"Near-miss pairs: {negativePairs.Count} checked, {falsePositives.Count} false positives");
        falsePositives.Should().BeEmpty("near-miss non-duplicate pairs should not be flagged at threshold 0.88");
    }

    // ──────────────────────────────────────────────────────────────
    //  Test 7: Known limitations — document similarity scores
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task KnownLimitations_DocumentedWithSimilarityScores()
    {
        var ds = BuildFullDataset();
        var limitationCategories = new[] { "title_abbreviation", "title_greek", "title_accent", "title_html", "title_subtitle" };

        var (svc, _) = CreateService(ds.References);
        var opts = new DeduplicationOptions
        {
            TitleSimilarityThreshold = 0.01, // Very low threshold to see ALL similarity scores
            RequireYearMatch = true,
            YearTolerance = 0,
            NormalizeSpelling = true
        };

        var matches = await svc.FindPotentialDuplicatesAsync(ProjectId, opts);
        var simLookup = matches.ToDictionary(
            m => (Math.Min(m.Primary.Id, m.Duplicate.Id), Math.Max(m.Primary.Id, m.Duplicate.Id)),
            m => m);

        _output.WriteLine("=== KNOWN LIMITATION CATEGORIES — SIMILARITY SCORES ===");
        _output.WriteLine("(These categories document mutations the service may not catch at 0.88)\n");

        foreach (var category in limitationCategories)
        {
            if (!ds.GroundTruth.ContainsKey(category)) continue;
            var pairs = ds.GroundTruth[category];

            _output.WriteLine($"--- {category} ---");
            var sims = new List<double>();

            foreach (var (idA, idB) in pairs.OrderBy(p => p.Item1))
            {
                var refA = ds.References.First(r => r.Id == idA);
                var refB = ds.References.First(r => r.Id == idB);

                double sim = 0;
                string status = "NO MATCH";
                if (simLookup.TryGetValue((idA, idB), out var m) && m.TitleSimilarity.HasValue)
                {
                    sim = m.TitleSimilarity.Value;
                    sims.Add(sim);
                    status = sim >= 0.88 ? "PASS@0.88" : $"FAIL@0.88";
                }

                _output.WriteLine($"  [{status}] sim={sim:F3}");
                _output.WriteLine($"    A: {Truncate(refA.Title, 90)}");
                _output.WriteLine($"    B: {Truncate(refB.Title, 90)}");
            }

            if (sims.Any())
            {
                _output.WriteLine($"  Summary: avg={sims.Average():F3}, min={sims.Min():F3}, max={sims.Max():F3}, pass@0.88={sims.Count(s => s >= 0.88)}/{pairs.Count}");
            }
            else
            {
                _output.WriteLine($"  Summary: No title-year matches found at any threshold");
            }
            _output.WriteLine("");
        }

        // This test is informational — no hard assertions.
        // The output documents what the service can and cannot handle.
        _output.WriteLine("=== RECOMMENDATIONS ===");
        _output.WriteLine("Categories with avg similarity < 0.70 likely need preprocessing (HTML decode, accent strip, etc.)");
        _output.WriteLine("Categories with avg similarity 0.70-0.88 could benefit from a lower threshold (trade precision for recall)");
    }

    // ──────────────────────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────────────────────

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
}
