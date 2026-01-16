using Mutagen.Bethesda.Plugins;

namespace Boutique.Models;

public sealed record KeywordRecord(
    FormKey FormKey,
    string? EditorID,
    ModKey ModKey) : IGameRecord
{
    public string DisplayName => EditorID ?? "(No EditorID)";
    public string FormKeyString => FormKey.ToString();
    public string ModDisplayName => ModKey.FileName;
}
