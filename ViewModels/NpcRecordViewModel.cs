using Boutique.Models;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Boutique.ViewModels;

public partial class NpcRecordViewModel : SelectableRecordViewModel<NpcRecord>
{
    public NpcRecordViewModel(NpcRecord npcRecord) : base(npcRecord)
    {
        this.WhenAnyValue(x => x.HasConflict, x => x.ConflictingFileName)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(ConflictTooltip)));
    }

    public NpcRecord NpcRecord => Record;

    [Reactive]
    private bool _hasConflict;

    [Reactive]
    private string? _conflictingFileName;

    public string ConflictTooltip => HasConflict && !string.IsNullOrEmpty(ConflictingFileName)
        ? $"Conflict: This NPC already has an outfit distribution in '{ConflictingFileName}'"
        : string.Empty;
}
