using System.Collections.Generic;
using System.Linq;
using Boutique.Models;
using Mutagen.Bethesda.Plugins;
using ReactiveUI;

namespace Boutique.ViewModels;

public class NpcOutfitAssignmentViewModel : ReactiveObject
{
    private readonly NpcOutfitAssignment _assignment;
    private bool _isSelected;

    public NpcOutfitAssignmentViewModel(NpcOutfitAssignment assignment)
    {
        _assignment = assignment;
    }

    public NpcOutfitAssignment Assignment => _assignment;

    // NPC properties
    public FormKey NpcFormKey => _assignment.NpcFormKey;
    public string? EditorId => _assignment.EditorId;
    public string? Name => _assignment.Name;
    public string DisplayName => _assignment.DisplayName;
    public string FormKeyString => _assignment.FormKeyString;
    public string ModDisplayName => _assignment.ModDisplayName;

    // Outfit properties
    public FormKey? FinalOutfitFormKey => _assignment.FinalOutfitFormKey;
    public string? FinalOutfitEditorId => _assignment.FinalOutfitEditorId;
    public string FinalOutfitDisplay => _assignment.FinalOutfitDisplay;

    // Conflict info
    public bool HasConflict => _assignment.HasConflict;
    public int DistributionCount => _assignment.Distributions.Count;

    // Distributions for detail panel
    public IReadOnlyList<OutfitDistribution> Distributions => _assignment.Distributions;

    // Selection state for DataGrid
    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }

    /// <summary>
    /// Gets a summary of the conflict (e.g., "2 files override")
    /// </summary>
    public string ConflictSummary => HasConflict 
        ? $"{DistributionCount} files" 
        : string.Empty;

    /// <summary>
    /// Gets the winning distribution file name
    /// </summary>
    public string WinningFileName => Distributions.FirstOrDefault(d => d.IsWinner)?.FileName ?? string.Empty;
}
