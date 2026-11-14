using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FinTrack.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public MainViewModel(
        DashboardViewModel dashboardViewModel,
        TransactionsViewModel transactionsViewModel)
    {
        Dashboard = dashboardViewModel
            ?? throw new ArgumentNullException(nameof(dashboardViewModel));

        Transactions = transactionsViewModel
            ?? throw new ArgumentNullException(nameof(transactionsViewModel));

        // Стартовый экран — обзор.
        CurrentViewModel = Dashboard;
    }

    /// <summary>Экран обзора. Создаётся один раз при запуске приложения.</summary>
    public DashboardViewModel Dashboard { get; }

    /// <summary>Экран транзакций. Создаётся один раз при запуске приложения.</summary>
    public TransactionsViewModel Transactions { get; }

    /// <summary>
    /// Текущий экран приложения. ViewLocator сам подберёт нужный View
    /// по имени ViewModel (DashboardViewModel → DashboardView).
    /// </summary>
    [ObservableProperty]
    public partial ViewModelBase? CurrentViewModel { get; set; }

    /// <summary>Активен ли пункт меню «Обзор».</summary>
    [ObservableProperty]
    public partial bool IsDashboardSelected { get; set; }

    /// <summary>Активен ли пункт меню «Транзакции».</summary>
    [ObservableProperty]
    public partial bool IsTransactionsSelected { get; set; }

    [RelayCommand]
    private void ShowDashboard()
    {
        CurrentViewModel = Dashboard;
    }

    [RelayCommand]
    private void ShowTransactions()
    {
        CurrentViewModel = Transactions;
    }

    partial void OnCurrentViewModelChanged(ViewModelBase? value)
    {
        IsDashboardSelected = value is DashboardViewModel;
        IsTransactionsSelected = value is TransactionsViewModel;
    }
}
