using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Reactive.Disposables;
using System.Reactive;
using RequiemGlamPatcher.ViewModels;

namespace RequiemGlamPatcher.Views;

public partial class MainWindow : Window
{
    private const string ArmorDragDataFormat = "RequiemGlamPatcher.ArmorRecords";
    private Point? _outfitDragStartPoint;
    private sealed record DropVisualSnapshot(Brush BorderBrush, Brush Background);

    private bool _syncingSourceSelection;
    private bool _syncingOutfitSelection;
    private bool _initialized;
    private readonly CompositeDisposable _bindings = new();

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.PropertyChanged += ViewModelOnPropertyChanged;
        var notificationDisposable = viewModel.PatchCreatedNotification.RegisterHandler(async interaction =>
        {
            var message = interaction.Input;
            await Dispatcher.InvokeAsync(() =>
                MessageBox.Show(this, message, "Patch Created", MessageBoxButton.OK, MessageBoxImage.Information));
            interaction.SetOutput(Unit.Default);
        });
        _bindings.Add(notificationDisposable);

        var confirmDisposable = viewModel.ConfirmOverwritePatch.RegisterHandler(async interaction =>
        {
            var message = interaction.Input;
            var result = await Dispatcher.InvokeAsync(() =>
                MessageBox.Show(this, message, "Overwrite Existing Patch?", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No));
            interaction.SetOutput(result == MessageBoxResult.Yes);
        });
        _bindings.Add(confirmDisposable);

        var outfitNameDisposable = viewModel.RequestOutfitName.RegisterHandler(async interaction =>
        {
            var prompt = interaction.Input;
            var result = await Dispatcher.InvokeAsync(() =>
            {
                var input = Microsoft.VisualBasic.Interaction.InputBox(prompt, "Create Outfit", string.Empty);
                return string.IsNullOrWhiteSpace(input) ? null : input;
            });
            interaction.SetOutput(result);
        });
        _bindings.Add(outfitNameDisposable);

        SourceArmorsGrid.Loaded += (_, _) => SynchronizeSourceSelection();
        OutfitArmorsGrid.Loaded += (_, _) => SynchronizeOutfitSelection();

        TargetArmorsGrid.Loaded += (_, _) =>
        {
            if (TargetArmorsGrid.Columns.Count > 0)
            {
                TargetArmorsGrid.Columns[0].SortDirection = ListSortDirection.Ascending;
            }
        };

        SynchronizeSourceSelection();

        Closed += (_, _) =>
        {
            viewModel.PropertyChanged -= ViewModelOnPropertyChanged;
            _bindings.Dispose();
        };
        Loaded += OnLoaded;
    }

    private void TargetArmorsDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        e.Handled = true;

        var dataGrid = (DataGrid)sender;
        var newDirection = e.Column.SortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;

        foreach (var column in dataGrid.Columns)
        {
            if (!ReferenceEquals(column, e.Column))
            {
                column.SortDirection = null;
            }
        }

        e.Column.SortDirection = newDirection;

        var sortMember = e.Column.SortMemberPath;
        if (string.IsNullOrWhiteSpace(sortMember) && e.Column is DataGridBoundColumn boundColumn)
        {
            if (boundColumn.Binding is Binding binding && binding.Path != null)
            {
                sortMember = binding.Path.Path;
            }
        }

        if (string.IsNullOrWhiteSpace(sortMember))
        {
            sortMember = nameof(ArmorRecordViewModel.DisplayName);
        }

        viewModel.ApplyTargetSort(sortMember, newDirection);
    }

    private void SourceArmorsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingSourceSelection)
            return;

        if (DataContext is not MainViewModel viewModel)
            return;

        var selected = SourceArmorsGrid.SelectedItems.Cast<object>().ToList();
        viewModel.SelectedSourceArmors = selected;
    }

    private void OutfitArmorsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingOutfitSelection)
            return;

        if (DataContext is not MainViewModel viewModel)
            return;

        var selected = OutfitArmorsGrid.SelectedItems.Cast<object>().ToList();
        viewModel.SelectedOutfitArmors = selected;
    }

    private void OutfitArmorsGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _outfitDragStartPoint = e.GetPosition(null);
    }

    private void OutfitArmorsGrid_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _outfitDragStartPoint == null)
            return;

        var position = e.GetPosition(null);
        if (Math.Abs(position.X - _outfitDragStartPoint.Value.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _outfitDragStartPoint.Value.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _outfitDragStartPoint = null;

        if (DataContext is not MainViewModel viewModel)
            return;

        var selected = OutfitArmorsGrid.SelectedItems
            .OfType<ArmorRecordViewModel>()
            .ToList();

        if (selected.Count == 0)
        {
            var underMouse = GetArmorRecordFromEvent(e);
            if (underMouse != null)
            {
                selected.Add(underMouse);
            }
        }

        if (selected.Count == 0)
            return;

        var data = new DataObject(ArmorDragDataFormat, selected);
        DragDrop.DoDragDrop(OutfitArmorsGrid, data, DragDropEffects.Copy);
        e.Handled = true;
    }

    private async void OutfitArmorsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        var pieces = OutfitArmorsGrid.SelectedItems
            .OfType<ArmorRecordViewModel>()
            .ToList();

        if (pieces.Count == 0)
        {
            var underMouse = GetArmorRecordFromEvent(e);
            if (underMouse != null)
            {
                pieces.Add(underMouse);
            }
        }

        if (pieces.Count == 0)
            return;

        await viewModel.CreateOutfitFromPiecesAsync(pieces);
        e.Handled = true;
    }

    private void NewOutfitDropZone_OnDragEnter(object sender, DragEventArgs e) =>
        HandleDropTargetDrag(sender as Border, e);

    private void NewOutfitDropZone_OnDragOver(object sender, DragEventArgs e) =>
        HandleDropTargetDrag(sender as Border, e);

    private void NewOutfitDropZone_OnDragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border border)
        {
            SetDropTargetState(border, false);
        }

        e.Handled = true;
    }

    private async void NewOutfitDropZone_OnDrop(object sender, DragEventArgs e)
    {
        if (sender is Border border)
        {
            SetDropTargetState(border, false);
        }

        if (DataContext is not MainViewModel viewModel)
        {
            e.Handled = true;
            return;
        }

        if (!TryExtractArmorRecords(e.Data, out var pieces) || pieces.Count == 0)
        {
            e.Handled = true;
            return;
        }

        await viewModel.CreateOutfitFromPiecesAsync(pieces);
        e.Handled = true;
    }

    private void OutfitDraftBorder_OnDragEnter(object sender, DragEventArgs e) =>
        HandleDropTargetDrag(sender as Border, e);

    private void OutfitDraftBorder_OnDragOver(object sender, DragEventArgs e) =>
        HandleDropTargetDrag(sender as Border, e);

    private void OutfitDraftBorder_OnDragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border border)
        {
            SetDropTargetState(border, false);
        }

        e.Handled = true;
    }

    private void OutfitDraftBorder_OnDrop(object sender, DragEventArgs e)
    {
        if (sender is Border border)
        {
            SetDropTargetState(border, false);
        }

        if (DataContext is not MainViewModel viewModel)
        {
            e.Handled = true;
            return;
        }

        if (sender is not Border draftBorder || draftBorder.DataContext is not OutfitDraftViewModel draft)
        {
            e.Handled = true;
            return;
        }

        if (!TryExtractArmorRecords(e.Data, out var pieces) || pieces.Count == 0)
        {
            e.Handled = true;
            return;
        }

        viewModel.TryAddPiecesToDraft(draft, pieces);
        e.Handled = true;
    }

    private void HandleDropTargetDrag(Border? border, DragEventArgs e)
    {
        if (border == null)
            return;

        if (HasArmorRecords(e.Data))
        {
            SetDropTargetState(border, true);
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            SetDropTargetState(border, false);
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private static bool HasArmorRecords(IDataObject data) =>
        data.GetDataPresent(ArmorDragDataFormat);

    private static bool TryExtractArmorRecords(IDataObject data, out List<ArmorRecordViewModel> pieces)
    {
        if (!HasArmorRecords(data))
        {
            pieces = new List<ArmorRecordViewModel>();
            return false;
        }

        if (data.GetData(ArmorDragDataFormat) is IEnumerable<ArmorRecordViewModel> records)
        {
            pieces = records
                .Where(r => r != null)
                .ToList();
            return pieces.Count > 0;
        }

        pieces = new List<ArmorRecordViewModel>();
        return false;
    }

    private void SetDropTargetState(Border? border, bool isActive)
    {
        if (border == null)
            return;

        if (border.Tag is not DropVisualSnapshot snapshot)
        {
            snapshot = new DropVisualSnapshot(border.BorderBrush, border.Background);
            border.Tag = snapshot;
        }

        if (isActive)
        {
            border.BorderBrush = Brushes.DodgerBlue;
            border.Background = new SolidColorBrush(Color.FromArgb(48, 30, 144, 255));
        }
        else
        {
            border.BorderBrush = snapshot.BorderBrush;
            border.Background = snapshot.Background;
        }
    }

    private ArmorRecordViewModel? GetArmorRecordFromEvent(MouseEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
            return null;

        var row = FindAncestor<DataGridRow>(source);
        return row?.Item as ArmorRecordViewModel;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match)
                return match;

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedSourceArmors))
        {
            SynchronizeSourceSelection();
        }
        else if (e.PropertyName == nameof(MainViewModel.SelectedOutfitArmors))
        {
            SynchronizeOutfitSelection();
        }
    }

    private void SynchronizeSourceSelection()
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        _syncingSourceSelection = true;
        try
        {
            SourceArmorsGrid.SelectedItems.Clear();
            foreach (var armor in viewModel.SelectedSourceArmors.OfType<object>())
            {
                SourceArmorsGrid.SelectedItems.Add(armor);
            }
        }
        finally
        {
            _syncingSourceSelection = false;
        }
    }

    private void SynchronizeOutfitSelection()
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        _syncingOutfitSelection = true;
        try
        {
            OutfitArmorsGrid.SelectedItems.Clear();
            foreach (var armor in viewModel.SelectedOutfitArmors.OfType<object>())
            {
                OutfitArmorsGrid.SelectedItems.Add(armor);
            }
        }
        finally
        {
            _syncingOutfitSelection = false;
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_initialized)
            return;

        if (DataContext is MainViewModel viewModel && viewModel.InitializeCommand.CanExecute(null))
        {
            viewModel.InitializeCommand.Execute(null);
            _initialized = true;
        }
    }
}
