using Boutique.Models;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Boutique.ViewModels;

public class NpcRecordViewModel : SelectableRecordViewModel<NpcRecord>
{
    public NpcRecordViewModel(NpcRecord npcRecord) : base(npcRecord)
    {
        this.WhenAnyValue(x => x.HasConflict, x => x.ConflictingFileName)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(ConflictTooltip)));
    }

    public NpcRecord NpcRecord => Record;

    [Reactive] public bool HasConflict { get; set; }
    [Reactive] public string? ConflictingFileName { get; set; }

    public string ConflictTooltip => HasConflict && !string.IsNullOrEmpty(ConflictingFileName)
        ? $"Conflict: This NPC already has an outfit distribution in '{ConflictingFileName}'"
        : string.Empty;
}
