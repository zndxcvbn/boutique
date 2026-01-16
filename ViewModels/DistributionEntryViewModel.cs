using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using Boutique.Models;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Boutique.ViewModels;

public enum GenderFilter
{
    Any,
    Female,
    Male
}

public enum UniqueFilter
{
    Any,
    UniqueOnly,
    NonUniqueOnly
}

public class DistributionEntryViewModel : ReactiveObject
{
    private ObservableCollection<NpcRecordViewModel> _selectedNpcs = [];
    private ObservableCollection<FactionRecordViewModel> _selectedFactions = [];
    private ObservableCollection<KeywordRecordViewModel> _selectedKeywords = [];
    private ObservableCollection<RaceRecordViewModel> _selectedRaces = [];
    private ObservableCollection<ClassRecordViewModel> _selectedClasses = [];

    public event EventHandler? EntryChanged;

    private void RaiseEntryChanged() => EntryChanged?.Invoke(this, EventArgs.Empty);

    public DistributionEntryViewModel(
        DistributionEntry entry,
        System.Action<DistributionEntryViewModel>? removeAction = null,
        Func<bool>? isFormatChangingToSpid = null)
    {
        Entry = entry;
        Type = entry.Type;
        SelectedOutfit = entry.Outfit;
        KeywordToDistribute = entry.KeywordToDistribute ?? string.Empty;
        UseChance = entry.Chance.HasValue;
        Chance = entry.Chance ?? 100;
        LevelFilters = entry.LevelFilters ?? string.Empty;
        RawStringFilters = entry.RawStringFilters ?? string.Empty;
        RawFormFilters = entry.RawFormFilters ?? string.Empty;

        Gender = entry.TraitFilters.IsFemale switch
        {
            true => GenderFilter.Female,
            false => GenderFilter.Male,
            null => GenderFilter.Any
        };
        Unique = entry.TraitFilters.IsUnique switch
        {
            true => UniqueFilter.UniqueOnly,
            false => UniqueFilter.NonUniqueOnly,
            null => UniqueFilter.Any
        };
        IsChild = entry.TraitFilters.IsChild;

        if (entry.NpcFormKeys.Count > 0)
        {
            var npcVms = entry.NpcFormKeys
                .Select(fk => new NpcRecordViewModel(new NpcRecord(fk, null, null, fk.ModKey)))
                .ToList();

            foreach (var npcVm in npcVms)
            {
                _selectedNpcs.Add(npcVm);
            }
        }

        if (entry.FactionFormKeys.Count > 0)
        {
            var factionVms = entry.FactionFormKeys
                .Select(fk => new FactionRecordViewModel(new FactionRecord(fk, null, null, fk.ModKey)))
                .ToList();

            foreach (var factionVm in factionVms)
            {
                _selectedFactions.Add(factionVm);
            }
        }

        if (entry.KeywordEditorIds.Count > 0)
        {
            var keywordVms = entry.KeywordEditorIds
                .Select(editorId => new KeywordRecordViewModel(new KeywordRecord(FormKey.Null, editorId, ModKey.Null)))
                .ToList();

            foreach (var keywordVm in keywordVms)
            {
                _selectedKeywords.Add(keywordVm);
            }
        }

        if (entry.RaceFormKeys.Count > 0)
        {
            var raceVms = entry.RaceFormKeys
                .Select(fk => new RaceRecordViewModel(new RaceRecord(fk, null, null, fk.ModKey)))
                .ToList();

            foreach (var raceVm in raceVms)
            {
                _selectedRaces.Add(raceVm);
            }
        }

        if (entry.ClassFormKeys.Count > 0)
        {
            var classVms = entry.ClassFormKeys
                .Select(fk => new ClassRecordViewModel(new ClassRecord(fk, null, null, fk.ModKey)))
                .ToList();

            foreach (var classVm in classVms)
            {
                _selectedClasses.Add(classVm);
            }
        }

        this.WhenAnyValue(x => x.Type)
            .Skip(1)
            .Subscribe(type =>
            {
                Entry.Type = type;
                this.RaisePropertyChanged(nameof(IsOutfitDistribution));
                this.RaisePropertyChanged(nameof(IsKeywordDistribution));
                RaiseEntryChanged();
            });

        this.WhenAnyValue(x => x.SelectedOutfit)
            .Skip(1)
            .Subscribe(outfit =>
            {
                Entry.Outfit = outfit;
                RaiseEntryChanged();
            });

        this.WhenAnyValue(x => x.KeywordToDistribute)
            .Skip(1)
            .Subscribe(keyword =>
            {
                Entry.KeywordToDistribute = keyword;
                RaiseEntryChanged();
            });

        this.WhenAnyValue(x => x.Gender)
            .Skip(1)
            .Subscribe(gender =>
            {
                Entry.TraitFilters = Entry.TraitFilters with
                {
                    IsFemale = gender switch
                    {
                        GenderFilter.Female => true,
                        GenderFilter.Male => false,
                        _ => null
                    }
                };
                RaiseEntryChanged();
            });

        this.WhenAnyValue(x => x.Unique)
            .Skip(1)
            .Subscribe(unique =>
            {
                Entry.TraitFilters = Entry.TraitFilters with
                {
                    IsUnique = unique switch
                    {
                        UniqueFilter.UniqueOnly => true,
                        UniqueFilter.NonUniqueOnly => false,
                        _ => null
                    }
                };
                RaiseEntryChanged();
            });

        this.WhenAnyValue(x => x.IsChild)
            .Skip(1)
            .Subscribe(isChild =>
            {
                Entry.TraitFilters = Entry.TraitFilters with { IsChild = isChild };
                RaiseEntryChanged();
            });

        var previousUseChance = UseChance;
        this.WhenAnyValue(x => x.UseChance)
            .Skip(1)
            .Subscribe(useChance =>
            {
                var wasEnabled = previousUseChance;
                previousUseChance = useChance;

                if (useChance && !wasEnabled && isFormatChangingToSpid != null)
                {
                    if (isFormatChangingToSpid())
                    {
                        var result = System.Windows.MessageBox.Show(
                            "Enabling chance-based distribution will change the file format to SPID.\n\n" +
                            "SkyPatcher does not support chance-based outfit distribution. " +
                            "The file will be saved in SPID format to support this feature.\n\n" +
                            "Do you want to continue?",
                            "Format Change Required",
                            System.Windows.MessageBoxButton.YesNo,
                            System.Windows.MessageBoxImage.Warning);

                        if (result == System.Windows.MessageBoxResult.No)
                        {
                            previousUseChance = false;
                            UseChance = false;
                            return;
                        }
                    }
                }

                Entry.Chance = useChance ? Chance : null;
                RaiseEntryChanged();
            });

        this.WhenAnyValue(x => x.Chance)
            .Skip(1)
            .Subscribe(chance =>
            {
                if (UseChance)
                {
                    Entry.Chance = chance;
                    RaiseEntryChanged();
                }
            });

        this.WhenAnyValue(x => x.LevelFilters)
            .Skip(1)
            .Subscribe(levelFilters =>
            {
                Entry.LevelFilters = string.IsNullOrWhiteSpace(levelFilters) ? null : levelFilters;
                RaiseEntryChanged();
            });

        this.WhenAnyValue(x => x.RawStringFilters)
            .Skip(1)
            .Subscribe(rawFilters =>
            {
                Entry.RawStringFilters = string.IsNullOrWhiteSpace(rawFilters) ? null : rawFilters;
                Entry.OriginalSpidFilter = null;
                RaiseEntryChanged();
            });

        this.WhenAnyValue(x => x.RawFormFilters)
            .Skip(1)
            .Subscribe(rawFilters =>
            {
                Entry.RawFormFilters = string.IsNullOrWhiteSpace(rawFilters) ? null : rawFilters;
                Entry.OriginalSpidFilter = null;
                RaiseEntryChanged();
            });

        RemoveCommand = ReactiveCommand.Create(() => removeAction?.Invoke(this));
    }

    public DistributionEntry Entry { get; }

    [Reactive]
    public DistributionType Type { get; set; } = DistributionType.Outfit;

    [Reactive]
    public IOutfitGetter? SelectedOutfit { get; set; }

    [Reactive]
    public string KeywordToDistribute { get; set; } = string.Empty;

    public bool IsOutfitDistribution => Type == DistributionType.Outfit;
    public bool IsKeywordDistribution => Type == DistributionType.Keyword;

    [Reactive]
    public bool UseChance { get; set; }

    [Reactive]
    public int Chance { get; set; } = 100;

    [Reactive]
    public string LevelFilters { get; set; } = string.Empty;

    [Reactive]
    public string RawStringFilters { get; set; } = string.Empty;

    [Reactive]
    public string RawFormFilters { get; set; } = string.Empty;

    [Reactive]
    public GenderFilter Gender { get; set; } = GenderFilter.Any;

    [Reactive]
    public UniqueFilter Unique { get; set; } = UniqueFilter.Any;

    [Reactive]
    public bool? IsChild { get; set; }

    public static DistributionType[] TypeOptions { get; } = [DistributionType.Outfit, DistributionType.Keyword];
    public static GenderFilter[] GenderOptions { get; } = [GenderFilter.Any, GenderFilter.Female, GenderFilter.Male];
    public static UniqueFilter[] UniqueOptions { get; } = [UniqueFilter.Any, UniqueFilter.UniqueOnly, UniqueFilter.NonUniqueOnly];

    public bool HasTraitFilters => Gender != GenderFilter.Any || Unique != UniqueFilter.Any || IsChild.HasValue;

    public ObservableCollection<NpcRecordViewModel> SelectedNpcs
    {
        get => _selectedNpcs;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedNpcs, value);
            UpdateEntryNpcs();
        }
    }

    public ObservableCollection<FactionRecordViewModel> SelectedFactions
    {
        get => _selectedFactions;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedFactions, value);
            UpdateEntryFactions();
        }
    }

    public ObservableCollection<KeywordRecordViewModel> SelectedKeywords
    {
        get => _selectedKeywords;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedKeywords, value);
            UpdateEntryKeywords();
        }
    }

    public ObservableCollection<RaceRecordViewModel> SelectedRaces
    {
        get => _selectedRaces;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedRaces, value);
            UpdateEntryRaces();
        }
    }

    public ObservableCollection<ClassRecordViewModel> SelectedClasses
    {
        get => _selectedClasses;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedClasses, value);
            UpdateEntryClasses();
        }
    }

    public ReactiveCommand<Unit, Unit> RemoveCommand { get; }

    [Reactive] public bool IsSelected { get; set; }

    public void UpdateEntryNpcs()
    {
        Entry.NpcFormKeys.Clear();
        Entry.NpcFormKeys.AddRange(SelectedNpcs.Select(npc => npc.FormKey));
        RaiseEntryChanged();
    }

    public void UpdateEntryFactions()
    {
        Entry.FactionFormKeys.Clear();
        Entry.ExcludedFactionFormKeys.Clear();

        foreach (var faction in SelectedFactions)
        {
            if (faction.IsExcluded)
                Entry.ExcludedFactionFormKeys.Add(faction.FormKey);
            else
                Entry.FactionFormKeys.Add(faction.FormKey);
        }

        Entry.OriginalSpidFilter = null;
        RaiseEntryChanged();
    }

    public void UpdateEntryKeywords()
    {
        Entry.KeywordEditorIds.Clear();
        Entry.ExcludedKeywordEditorIds.Clear();

        foreach (var keyword in SelectedKeywords)
        {
            var editorId = keyword.KeywordRecord.EditorID;
            if (string.IsNullOrWhiteSpace(editorId))
                continue;

            if (keyword.IsExcluded)
                Entry.ExcludedKeywordEditorIds.Add(editorId);
            else
                Entry.KeywordEditorIds.Add(editorId);
        }

        Entry.OriginalSpidFilter = null;
        RaiseEntryChanged();
    }

    public void UpdateEntryRaces()
    {
        Entry.RaceFormKeys.Clear();
        Entry.ExcludedRaceFormKeys.Clear();

        foreach (var race in SelectedRaces)
        {
            if (race.IsExcluded)
                Entry.ExcludedRaceFormKeys.Add(race.FormKey);
            else
                Entry.RaceFormKeys.Add(race.FormKey);
        }

        Entry.OriginalSpidFilter = null;
        RaiseEntryChanged();
    }

    public void UpdateEntryClasses()
    {
        Entry.ClassFormKeys.Clear();
        Entry.ClassFormKeys.AddRange(SelectedClasses.Select(c => c.FormKey));
        RaiseEntryChanged();
    }

    public bool AddCriterion<T>(T item, ObservableCollection<T> collection, Action updateAction)
        where T : ISelectableRecordViewModel
    {
        if (collection.Any(existing => existing.FormKey == item.FormKey))
            return false;

        collection.Add(item);
        updateAction();
        return true;
    }

    public bool RemoveCriterion<T>(T item, ObservableCollection<T> collection, Action updateAction)
        where T : class
    {
        if (!collection.Remove(item))
            return false;

        updateAction();
        return true;
    }

    public void AddNpc(NpcRecordViewModel npc) => AddCriterion(npc, _selectedNpcs, UpdateEntryNpcs);
    public void RemoveNpc(NpcRecordViewModel npc) => RemoveCriterion(npc, _selectedNpcs, UpdateEntryNpcs);

    public void AddFaction(FactionRecordViewModel faction) => AddCriterion(faction, _selectedFactions, UpdateEntryFactions);
    public void RemoveFaction(FactionRecordViewModel faction) => RemoveCriterion(faction, _selectedFactions, UpdateEntryFactions);

    public void AddKeyword(KeywordRecordViewModel keyword) => AddCriterion(keyword, _selectedKeywords, UpdateEntryKeywords);
    public void RemoveKeyword(KeywordRecordViewModel keyword) => RemoveCriterion(keyword, _selectedKeywords, UpdateEntryKeywords);

    public void AddRace(RaceRecordViewModel race) => AddCriterion(race, _selectedRaces, UpdateEntryRaces);
    public void RemoveRace(RaceRecordViewModel race) => RemoveCriterion(race, _selectedRaces, UpdateEntryRaces);

    public void AddClass(ClassRecordViewModel classVm) => AddCriterion(classVm, _selectedClasses, UpdateEntryClasses);
    public void RemoveClass(ClassRecordViewModel classVm) => RemoveCriterion(classVm, _selectedClasses, UpdateEntryClasses);
}
