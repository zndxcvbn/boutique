using Boutique.Models;
using Mutagen.Bethesda.Plugins;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Boutique.ViewModels;

public class ClassRecordViewModel : ReactiveObject, ISelectableRecordViewModel
{
    private readonly string _searchCache;

    public ClassRecordViewModel(ClassRecord classRecord)
    {
        ClassRecord = classRecord;
        _searchCache = $"{DisplayName} {EditorID} {ModDisplayName} {FormKeyString}".ToLowerInvariant();
    }

    public ClassRecord ClassRecord { get; }

    public string EditorID => ClassRecord.EditorID ?? "(No EditorID)";
    public string DisplayName => ClassRecord.DisplayName;
    public string ModDisplayName => ClassRecord.ModDisplayName;
    public string FormKeyString => ClassRecord.FormKeyString;
    public FormKey FormKey => ClassRecord.FormKey;

    [Reactive] public bool IsSelected { get; set; }

    public bool MatchesSearch(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return true;

        return _searchCache.Contains(searchTerm.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
