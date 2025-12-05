using Boutique.Models;
using Mutagen.Bethesda.Plugins;
using ReactiveUI;

namespace Boutique.ViewModels;

public class NpcRecordViewModel : ReactiveObject
{
    private readonly string _searchCache;
    private bool _isSelected;
    private bool _hasConflict;
    private string? _conflictingFileName;

    public NpcRecordViewModel(NpcRecord npcRecord)
    {
        NpcRecord = npcRecord;
        _searchCache = $"{DisplayName} {EditorID} {ModDisplayName} {FormKeyString}".ToLowerInvariant();
    }

    public NpcRecord NpcRecord { get; }

    public string EditorID => NpcRecord.EditorID ?? "(No EditorID)";
    public string DisplayName => NpcRecord.DisplayName;
    public string ModDisplayName => NpcRecord.ModDisplayName;
    public string FormKeyString => NpcRecord.FormKeyString;
    public FormKey FormKey => NpcRecord.FormKey;

    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }

    /// <summary>
    /// Indicates whether this NPC has a conflicting outfit distribution in an existing file.
    /// </summary>
    public bool HasConflict
    {
        get => _hasConflict;
        set => this.RaiseAndSetIfChanged(ref _hasConflict, value);
    }

    /// <summary>
    /// The name of the file that has a conflicting distribution for this NPC.
    /// </summary>
    public string? ConflictingFileName
    {
        get => _conflictingFileName;
        set => this.RaiseAndSetIfChanged(ref _conflictingFileName, value);
    }

    /// <summary>
    /// Tooltip text for the conflict warning.
    /// </summary>
    public string ConflictTooltip => HasConflict && !string.IsNullOrEmpty(ConflictingFileName)
        ? $"⚠ Conflict: This NPC already has an outfit distribution in '{ConflictingFileName}'"
        : string.Empty;

    public bool MatchesSearch(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return true;

        return _searchCache.Contains(searchTerm.Trim().ToLowerInvariant());
    }
}

