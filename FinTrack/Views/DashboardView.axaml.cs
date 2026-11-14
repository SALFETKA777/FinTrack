using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FinTrack.ViewModels;
using ScottPlot;
using ScottPlot.Plottables;

namespace FinTrack.Views;

/// <summary>
/// Экран обзора. ViewModel поставляет готовые данные, а View только
/// настраивает внешний вид графиков ScottPlot и перерисовывает их
/// при изменении коллекций или завершении загрузки.
/// </summary>
public partial class DashboardView : UserControl
{
    // Цвета согласованы с палитрой App.axaml и тёмной темой приложения.
    private static readonly Color SurfaceColor = Color.FromHex("#161A21");
    private static readonly Color AxisColor = Color.FromHex("#96A1B2");
    private static readonly Color GridColor = Color.FromHex("#262C36");
    private static readonly Color IncomeColor = Color.FromHex("#34D399");
    private static readonly Color ExpenseColor = Color.FromHex("#F87171");
    private static readonly Color FallbackSliceColor = Color.FromHex("#6B7280");

    private static readonly CultureInfo MonthCulture =
        CultureInfo.GetCultureInfo("ru-RU");

    private DashboardViewModel? _viewModel;

    public DashboardView()
    {
        InitializeComponent();

        ConfigurePlots();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>
    /// Загружает данные при показе экрана. При повторном открытии Dashboard
    /// данные перечитываются, поэтому изменения с экрана транзакций видны сразу.
    /// </summary>
    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        AttachViewModel(DataContext as DashboardViewModel);

        if (_viewModel is not null)
        {
            await _viewModel.LoadCommand.ExecuteAsync(null);
            UpdateCharts();
        }
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        DetachViewModel();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        AttachViewModel(DataContext as DashboardViewModel);
    }

    private void AttachViewModel(DashboardViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
        {
            return;
        }

        DetachViewModel();

        if (viewModel is null)
        {
            return;
        }

        _viewModel = viewModel;
        _viewModel.MonthlySummaries.CollectionChanged += OnChartDataChanged;
        _viewModel.CategoryShares.CollectionChanged += OnChartDataChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        UpdateCharts();
    }

    private void DetachViewModel()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.MonthlySummaries.CollectionChanged -= OnChartDataChanged;
        _viewModel.CategoryShares.CollectionChanged -= OnChartDataChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = null;
    }

    private void OnChartDataChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateCharts();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // После завершения загрузки данные могут остаться пустыми —
        // перерисовываем графики, чтобы они отражали актуальное состояние.
        if (e.PropertyName == nameof(DashboardViewModel.IsLoading))
        {
            UpdateCharts();
        }
    }

    private void UpdateCharts()
    {
        RenderTrendChart();
        RenderCategoryChart();
    }

    // ===================== Столбчатая диаграмма =====================

    private void ConfigurePlots()
    {
        var trendPlot = TrendPlot.Plot;
        ApplyBaseStyle(trendPlot);
        trendPlot.Grid.MajorLineWidth = 1;
        trendPlot.Grid.MinorLineWidth = 0;
        trendPlot.Axes.Bottom.TickLabelStyle.FontSize = 11;
        trendPlot.Axes.Left.TickLabelStyle.FontSize = 11;
        trendPlot.Axes.Margins(bottom: 0);

        var categoryPlot = CategoryPlot.Plot;
        ApplyBaseStyle(categoryPlot);
        categoryPlot.HideGrid();
        categoryPlot.Axes.Frameless();
        categoryPlot.Axes.Bottom.TickLabelStyle.IsVisible = false;
        categoryPlot.Axes.Left.TickLabelStyle.IsVisible = false;
    }

    private static void ApplyBaseStyle(Plot plot)
    {
        plot.FigureBackground.Color = SurfaceColor;
        plot.DataBackground.Color = SurfaceColor;
        plot.Grid.MajorLineColor = GridColor;
        plot.Axes.Color(AxisColor);
    }

    private void RenderTrendChart()
    {
        var plot = TrendPlot.Plot;
        var summaries = _viewModel?.MonthlySummaries;

        plot.Clear();

        if (summaries is null || summaries.Count == 0)
        {
            plot.Axes.SetLimitsX(-0.6, 0.6);
            plot.Axes.SetLimitsY(0, 1);
            TrendPlot.Refresh();
            return;
        }

        int count = summaries.Count;
        var positions = new double[count];
        var incomeValues = new double[count];
        var expenseValues = new double[count];
        var labels = new string[count];

        for (int i = 0; i < count; i++)
        {
            positions[i] = i;
            incomeValues[i] = (double)summaries[i].Income;
            expenseValues[i] = (double)summaries[i].Expense;
            labels[i] = summaries[i].Month.ToString("MMM yy", MonthCulture);
        }

        // Два ряда столбцов: доходы слева, расходы справа от подписи месяца.
        const double barOffset = 0.2;
        const double barWidth = 0.36;

        var incomePositions = new double[count];
        var expensePositions = new double[count];

        for (int i = 0; i < count; i++)
        {
            incomePositions[i] = positions[i] - barOffset;
            expensePositions[i] = positions[i] + barOffset;
        }

        var incomeBars = plot.Add.Bars(incomePositions, incomeValues);
        incomeBars.Color = IncomeColor;
        SetBarWidth(incomeBars, barWidth);

        var expenseBars = plot.Add.Bars(expensePositions, expenseValues);
        expenseBars.Color = ExpenseColor;
        SetBarWidth(expenseBars, barWidth);

        double maxValue = 0;
        foreach (var value in incomeValues)
        {
            if (value > maxValue)
            {
                maxValue = value;
            }
        }

        foreach (var value in expenseValues)
        {
            if (value > maxValue)
            {
                maxValue = value;
            }
        }

        double top = maxValue <= 0 ? 1 : maxValue * 1.15;

        plot.Axes.SetLimitsX(-0.6, count - 0.4);
        plot.Axes.SetLimitsY(0, top);
        plot.Axes.Bottom.SetTicks(positions, labels);

        TrendPlot.Refresh();
    }

    private static void SetBarWidth(BarPlot bars, double width)
    {
        foreach (var bar in bars.Bars)
        {
            bar.Size = width;
        }
    }

    // ===================== Круговая диаграмма =====================

    private void RenderCategoryChart()
    {
        var plot = CategoryPlot.Plot;
        var shares = _viewModel?.CategoryShares;

        plot.Clear();

        if (shares is null || shares.Count == 0)
        {
            CategoryPlot.Refresh();
            return;
        }

        var slices = new List<PieSlice>(shares.Count);

        foreach (var share in shares)
        {
            slices.Add(new PieSlice
            {
                Value = (double)share.Amount,
                FillColor = ParseColor(share.Color)
            });
        }

        var pie = plot.Add.Pie(slices);
        pie.ExplodeFraction = 0;
        pie.SliceLabelDistance = 1.15;
        plot.Axes.AutoScale();

        CategoryPlot.Refresh();
    }

    private static Color ParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return FallbackSliceColor;
        }

        try
        {
            return Color.FromHex(hex);
        }
        catch
        {
            // Некорректный цвет категории не должен ронять экран.
            return FallbackSliceColor;
        }
    }
}
