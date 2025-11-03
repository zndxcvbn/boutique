using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive;
using Mutagen.Bethesda.Plugins;
using ReactiveUI;

namespace RequiemGlamPatcher.ViewModels;

public class OutfitDraftViewModel : ReactiveObject
{
    private readonly ObservableCollection<ArmorRecordViewModel> _pieces;
    private readonly Action<OutfitDraftViewModel> _removeDraft;
    private readonly Action<OutfitDraftViewModel, ArmorRecordViewModel> _removePiece;
    private FormKey? _formKey;

    public OutfitDraftViewModel(
        string name,
        string editorId,
        IEnumerable<ArmorRecordViewModel> pieces,
        Action<OutfitDraftViewModel> removeDraft,
        Action<OutfitDraftViewModel, ArmorRecordViewModel> removePiece)
    {
        Name = name;
        EditorId = editorId;
        _removeDraft = removeDraft;
        _removePiece = removePiece;

        _pieces = new ObservableCollection<ArmorRecordViewModel>(pieces);
        _pieces.CollectionChanged += PiecesOnCollectionChanged;
        Pieces = new ReadOnlyObservableCollection<ArmorRecordViewModel>(_pieces);

        RemovePieceCommand = ReactiveCommand.Create<ArmorRecordViewModel>(piece => _removePiece(this, piece));
        RemoveSelfCommand = ReactiveCommand.Create(() => _removeDraft(this));
    }

    public Guid Id { get; } = Guid.NewGuid();

    public string Name { get; }

    public string EditorId { get; }

    public ReadOnlyObservableCollection<ArmorRecordViewModel> Pieces { get; }

    public bool HasPieces => _pieces.Count > 0;

    public FormKey? FormKey
    {
        get => _formKey;
        set
        {
            if (_formKey == value)
                return;

            this.RaiseAndSetIfChanged(ref _formKey, value);
            this.RaisePropertyChanged(nameof(FormIdDisplay));
            this.RaisePropertyChanged(nameof(Header));
        }
    }

    public string FormIdDisplay => _formKey.HasValue ? $"0x{_formKey.Value.ID:X8}" : "Pending";

    public string Header => $"{Name} ({EditorId}) — FormID {FormIdDisplay}";

    public ReactiveCommand<ArmorRecordViewModel, Unit> RemovePieceCommand { get; }

    public ReactiveCommand<Unit, Unit> RemoveSelfCommand { get; }

    public IReadOnlyList<ArmorRecordViewModel> GetPieces() => _pieces.ToList();

    public void RemovePiece(ArmorRecordViewModel piece)
    {
        if (_pieces.Remove(piece))
        {
            this.RaisePropertyChanged(nameof(HasPieces));
        }
    }

    private void PiecesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        this.RaisePropertyChanged(nameof(HasPieces));
    }
}
