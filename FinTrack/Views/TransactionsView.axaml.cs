using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FinTrack.ViewModels;

namespace FinTrack.Views;

public partial class TransactionsView : UserControl
{
    private TransactionsViewModel? _viewModel;

    public TransactionsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>
    /// Загружает данные при первом показе экрана.
    /// </summary>
    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        AttachViewModel(DataContext as TransactionsViewModel);

        if (DataContext is TransactionsViewModel viewModel)
        {
            await viewModel.LoadCommand.ExecuteAsync(null);
        }
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        AttachViewModel(null);
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        AttachViewModel(DataContext as TransactionsViewModel);
    }

    private void AttachViewModel(TransactionsViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
        {
            return;
        }

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = viewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Кнопка «Изменить» находится в списке ниже формы, поэтому при входе
        // в режим редактирования прокручиваем страницу к форме.
        if (e.PropertyName == nameof(TransactionsViewModel.IsEditing)
            && _viewModel?.IsEditing == true)
        {
            FormCard.BringIntoView();
        }
    }
}
