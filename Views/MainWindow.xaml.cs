using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Reactive.Disposables;
using System.Reactive;
using RequiemGlamPatcher.ViewModels;

namespace RequiemGlamPatcher.Views;

public partial class MainWindow : Window
{
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
