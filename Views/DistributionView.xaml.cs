using System;
using System.Reactive;
using System.Windows;
using System.Windows.Controls;
using Boutique.ViewModels;
using ReactiveUI;

namespace Boutique.Views;

public partial class DistributionView
{
    private IDisposable? _previewSubscription;

    public DistributionView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DisposePreviewSubscription();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DisposePreviewSubscription();

        if (e.NewValue is not DistributionViewModel viewModel)
            return;

        _previewSubscription = viewModel.ShowPreview.RegisterHandler(async interaction =>
        {
            await Dispatcher.InvokeAsync(() =>
            {
                var owner = Window.GetWindow(this);
                var window = new OutfitPreviewWindow(interaction.Input)
                {
                    Owner = owner
                };
                window.Show();
            });

            interaction.SetOutput(Unit.Default);
        });
    }

    private void DisposePreviewSubscription()
    {
        _previewSubscription?.Dispose();
        _previewSubscription = null;
    }
}
