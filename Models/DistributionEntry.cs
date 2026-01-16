using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;

namespace Boutique.Models;

public enum DistributionType
{
    Outfit,
    Keyword
}

public sealed class DistributionEntry
{
    public DistributionType Type { get; set; } = DistributionType.Outfit;
    public IOutfitGetter? Outfit { get; set; }
    public string? KeywordToDistribute { get; set; }
    public List<FormKey> NpcFormKeys { get; set; } = [];
    public List<FormKey> FactionFormKeys { get; set; } = [];

    /// <summary>
    /// The original parsed SPID filter, preserved for round-trip formatting.
    /// When set, formatting uses this instead of reconstructing from resolved values.
    /// </summary>
    public SpidDistributionFilter? OriginalSpidFilter { get; set; }

    /// <summary>
    /// Gets or sets the keyword EditorIDs used for filtering. Includes both game keywords and virtual
    /// keywords (SPID-distributed via Keyword = lines).
    /// </summary>
    public List<string> KeywordEditorIds { get; set; } = [];

    public List<FormKey> RaceFormKeys { get; set; } = [];
    public List<FormKey> ClassFormKeys { get; set; } = [];
    public List<FormKey> CombatStyleFormKeys { get; set; } = [];
    public List<FormKey> OutfitFilterFormKeys { get; set; } = [];
    public List<FormKey> PerkFormKeys { get; set; } = [];
    public List<FormKey> VoiceTypeFormKeys { get; set; } = [];
    public List<FormKey> LocationFormKeys { get; set; } = [];
    public List<FormKey> FormListFormKeys { get; set; } = [];

    public SpidTraitFilters TraitFilters { get; set; } = new();

    /// <summary>
    /// Gets or sets SPID level filters (position 4). Supports level ranges (e.g., "5/20") and
    /// skill filters (e.g., "12(85/999)" for Alteration skill 85+).
    /// </summary>
    public string? LevelFilters { get; set; }

    public int? Chance { get; set; }

    /// <summary>
    /// Raw/advanced string filters that can't be represented by dropdowns.
    /// Supports wildcards (*Mage, *Guard), virtual keywords, and other SPID syntax.
    /// </summary>
    public string? RawStringFilters { get; set; }

    /// <summary>
    /// Raw/advanced form filters that can't be represented by dropdowns.
    /// Supports SPID form filter syntax for advanced use cases.
    /// </summary>
    public string? RawFormFilters { get; set; }

    /// <summary>
    /// List of excluded/negated keyword EditorIDs (prefixed with - in SPID output).
    /// </summary>
    public List<string> ExcludedKeywordEditorIds { get; set; } = [];

    /// <summary>
    /// List of excluded/negated faction FormKeys (prefixed with - in SPID output).
    /// </summary>
    public List<FormKey> ExcludedFactionFormKeys { get; set; } = [];

    /// <summary>
    /// List of excluded/negated race FormKeys (prefixed with - in SPID output).
    /// </summary>
    public List<FormKey> ExcludedRaceFormKeys { get; set; } = [];
}

public sealed record DistributionParseError(int LineNumber, string LineContent, string Reason)
{
    public string ErrorHeader => $"Line {LineNumber}: {Reason}";
}
