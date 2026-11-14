using FinTrack.Data;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FinTrack.Services;
using FinTrack.ViewModels;
using FinTrack.Views;

namespace FinTrack;


public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        DatabaseInitializer.Initialize();
    }


    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Точка сборки зависимостей: сервисы → ViewModel экранов → главная ViewModel.
            // Сервисы создаются один раз и переиспользуются экранами.
            var transactionService = new TransactionService();
            var statisticsService = new StatisticsService();

            var dashboardViewModel = new DashboardViewModel(
                transactionService,
                statisticsService);

            var transactionsViewModel = new TransactionsViewModel(transactionService);

            var mainViewModel = new MainViewModel(
                dashboardViewModel,
                transactionsViewModel);

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel,
            };
        }

         base.OnFrameworkInitializationCompleted();
    }
}
