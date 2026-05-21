using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Lightweight symbolic analyzer for Turkish and English geometry prompts.
/// Recognizes ISAS 2018 design vocabulary (Wong form interactions, Ching principles
/// and organization schemas) in addition to the original geometry keywords.
/// </summary>
public sealed class KeywordSymbolicInputAnalyzer : ISymbolicInputAnalyzer
{
    private static readonly Regex TokenRegex = new Regex(@"[\p{L}\p{N}#']+", RegexOptions.Compiled);
    private readonly Dictionary<string, CommandSymbolType> symbolTypes = new Dictionary<string, CommandSymbolType>();

    // Canonical forms for hint extraction (normalized key → canonical value passed to AI)
    private readonly Dictionary<string, string> interactionCanonical   = new Dictionary<string, string>();
    private readonly Dictionary<string, string> principleCanonical     = new Dictionary<string, string>();
    private readonly Dictionary<string, string> schemaCanonical        = new Dictionary<string, string>();

    public KeywordSymbolicInputAnalyzer()
    {
        // ── Existing geometry vocabulary ──────────────────────────────────────────
        Register(CommandSymbolType.Action,
            "create", "build", "make", "spawn", "olustur", "oluştur", 
            "yap", "yarat", "ciz", "çiz", "ekle", "insa", "inşa");

        Register(CommandSymbolType.Shape,
            "cube", "box", "kup", "küp", "kupu", "küpü",
            "sphere", "ball", "kure", "küre", "kureyi", "küreyi",
            "cylinder", "silindir", "silindiri");

        Register(CommandSymbolType.Color,
            "red", "blue", "yellow", "green", "white", "black",
            "orange", "purple", "pink", "cyan", "gray", "grey", "brown");
        Register(CommandSymbolType.Color,
            "kirmizi", "kırmızı", "mavi", "avi", "sari", "sarı",
            "yesil", "yeşil", "beyaz", "siyah", "turuncu", "mor",
            "pembe", "gri", "kahverengi");

        Register(CommandSymbolType.Position,
            "left", "right", "center", "middle", "between", "side",
            "sol", "solda", "sag", "sağ", "sagda", "sağda",
            "orta", "ortada", "ortasina", "ortasına", "yanina", "yanına");

        Register(CommandSymbolType.SplitOperator,
            "split", "divide", "partition", "binary", "half", "halves",
            "ikiye", "2ye", "yarim", "yarım", "ayir", "ayır",
            "bol", "böl", "bolunme", "bölünme", "bolme", "bölme");

        // ── Wong (1969) — 8 form interactions ────────────────────────────────────
        RegisterInteraction("detachment",
            "detachment", "kopma", "kopuk", "ayrik", "ayrık", "ayrılmış");

        RegisterInteraction("touching",
            "touching", "touch", "temas", "temaseden", "temasetme",
            "dokunma", "dokunuyor", "kenar");

        RegisterInteraction("overlapping",
            "overlapping", "overlap", "ortusme", "örtüşme", "ortusüyor",
            "üstüste", "ustuste", "kaplıyor", "kapliyor");

        RegisterInteraction("penetration",
            "penetration", "penetrate", "icegirme", "içegirme",
            "icinden", "içinden", "icinegecen", "nüfuz");

        RegisterInteraction("union",
            "union", "birlesme", "birleşme", "birlesiyor", "birleşiyor",
            "birlestir", "birleştir", "merge", "merged");

        RegisterInteraction("subtraction",
            "subtraction", "subtract", "eksilme", "eksilme", "cikar",
            "çıkar", "cikarma", "çıkarma", "kesip", "remove");

        RegisterInteraction("intersection",
            "intersection", "intersect", "kesisme", "kesişme",
            "kesisiyor", "kesişiyor", "ortak", "overlap_zone");

        RegisterInteraction("coinciding",
            "coinciding", "coincide", "denkgelme", "denk", "ayni",
            "aynı", "ustuste", "exactly", "same_position");

        // ── Ching (2014) — 6 design principles ───────────────────────────────────
        RegisterPrinciple("harmony",
            "harmony", "uyum", "harmoni", "uyumlu", "harmonious",
            "ritim", "rhythm", "tekrar", "repetition",
            "sureklilik", "süreklilik", "continuity");

        RegisterPrinciple("balance",
            "balance", "denge", "dengeli", "balanced",
            "simetri", "symmetry", "simetrik", "symmetric",
            "asimetri", "asymmetry", "asimetrik", "asymmetric",
            "radyal", "radial", "merkezi", "central_balance");

        RegisterPrinciple("hierarchy",
            "hierarchy", "hiyerarsi", "hiyerarşi", "hierarchical",
            "agaclar", "ağaçlar", "trees", "kumeler", "kümeler",
            "clusters", "kalinlik", "kalınlık", "weight");

        RegisterPrinciple("proportion",
            "proportion", "proporsiyon", "orantı", "orantisal",
            "boyut", "size", "oran", "ratio", "parcalar", "parçalar", "parts");

        RegisterPrinciple("dominance",
            "dominance", "dominant", "hakimiyet", "vurgu", "emphasis",
            "odak", "focal", "accentuation", "prominence");

        RegisterPrinciple("contrast",
            "contrast", "karsitlik", "karşıtlık", "benzerlik",
            "similarity", "acikkoyu", "açıkkoyu", "value_contrast",
            "cizgi", "çizgi", "line_contrast", "shape_contrast");

        // ── Ching (2014) — 5 organization schemas ────────────────────────────────
        RegisterSchema("central",
            "central", "merkezi", "center_schema", "merkez");

        RegisterSchema("linear",
            "linear", "cizgisel", "çizgisel", "lineer", "dizi", "sira", "sıra", "row");

        RegisterSchema("radial",
            "radial", "isinsal", "ışınsal", "radyel", "spoke");

        RegisterSchema("clustered",
            "clustered", "cluster", "kumeli", "kümeli", "grup", "group", "kume", "küme");

        RegisterSchema("grid",
            "grid", "gridal", "izgara", "ızgara", "lattice", "matrix");
    }

    /// <inheritdoc/>
    public SymbolicAnalysisResult Analyze(string input)
    {
        SymbolicAnalysisResult result = new SymbolicAnalysisResult();
        if (string.IsNullOrWhiteSpace(input))
            return result;

        MatchCollection matches = TokenRegex.Matches(input);
        for (int index = 0; index < matches.Count; index++)
        {
            Match match = matches[index];
            string normalized = TextNormalizer.NormalizeKey(match.Value);
            CommandSymbolType type = ResolveType(normalized);

            if (type == CommandSymbolType.Unknown && int.TryParse(normalized, out _))
                type = CommandSymbolType.Quantity;

            CommandSymbol symbol = new CommandSymbol
            {
                Type = type,
                RawText = match.Value,
                NormalizedText = normalized,
                StartIndex = match.Index,
                Length = match.Length
            };

            result.Symbols.Add(symbol);

            if (type == CommandSymbolType.SplitOperator)
                result.RequestsBinaryPartition = true;
        }

        result.PreferredPartitionAxis = InferPartitionAxis(result.Symbols);

        // Populate ISAS 2018 hint fields from detected tokens
        result.DetectedFormInteraction   = InferCanonical(result.Symbols, CommandSymbolType.FormInteraction,   interactionCanonical);
        result.DetectedDesignPrinciple   = InferCanonical(result.Symbols, CommandSymbolType.DesignPrinciple,   principleCanonical);
        result.DetectedOrganizationSchema = InferCanonical(result.Symbols, CommandSymbolType.OrganizationSchema, schemaCanonical);

        return result;
    }

    // ── Registration helpers ───────────────────────────────────────────────────

    private void Register(CommandSymbolType type, params string[] aliases)
    {
        for (int index = 0; index < aliases.Length; index++)
            symbolTypes[TextNormalizer.NormalizeKey(aliases[index])] = type;
    }

    private void RegisterInteraction(string canonical, params string[] aliases)
    {
        for (int i = 0; i < aliases.Length; i++)
        {
            string key = TextNormalizer.NormalizeKey(aliases[i]);
            symbolTypes[key] = CommandSymbolType.FormInteraction;
            interactionCanonical[key] = canonical;
        }
    }

    private void RegisterPrinciple(string canonical, params string[] aliases)
    {
        for (int i = 0; i < aliases.Length; i++)
        {
            string key = TextNormalizer.NormalizeKey(aliases[i]);
            symbolTypes[key] = CommandSymbolType.DesignPrinciple;
            principleCanonical[key] = canonical;
        }
    }

    private void RegisterSchema(string canonical, params string[] aliases)
    {
        for (int i = 0; i < aliases.Length; i++)
        {
            string key = TextNormalizer.NormalizeKey(aliases[i]);
            symbolTypes[key] = CommandSymbolType.OrganizationSchema;
            schemaCanonical[key] = canonical;
        }
    }

    // ── Inference helpers ─────────────────────────────────────────────────────

    private CommandSymbolType ResolveType(string normalizedToken)
    {
        return symbolTypes.TryGetValue(normalizedToken, out CommandSymbolType type)
            ? type
            : CommandSymbolType.Unknown;
    }

    /// <summary>
    /// Returns the canonical form of the first detected symbol of the given type,
    /// or empty string if none was found.
    /// </summary>
    private string InferCanonical(
        List<CommandSymbol> symbols,
        CommandSymbolType targetType,
        Dictionary<string, string> canonicalMap)
    {
        for (int i = 0; i < symbols.Count; i++)
        {
            if (symbols[i].Type == targetType &&
                canonicalMap.TryGetValue(symbols[i].NormalizedText, out string canonical))
                return canonical;
        }
        return string.Empty;
    }

    private PartitionAxis InferPartitionAxis(List<CommandSymbol> symbols)
    {
        for (int index = 0; index < symbols.Count; index++)
        {
            string token = symbols[index].NormalizedText;

            if (token == "vertical" || token == "dikey" || token == "yukari" || token == "asagi")
                return PartitionAxis.Y;

            if (token == "depth" || token == "ileri" || token == "geri" || token == "z")
                return PartitionAxis.Z;
        }

        return PartitionAxis.X;
    }
}
