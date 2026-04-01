using System;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using PriceDropCatcher.Data;
using PriceDropCatcher.Helpers;
using PriceDropCatcher.Models;
using PriceDropCatcher.Properties;
using PriceDropCatcher.Services;

namespace PriceDropCatcher.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly ProductPageService _pageService;
        private readonly SerpApiService _serp;
        private readonly SqliteDatabaseService _db;
        private readonly Dispatcher _dispatcher;
        private readonly DispatcherTimer _monitorTimer;
        private CancellationTokenSource _pipelineCts;
        private string _lastProcessedUrl;

        public MainViewModel(ProductPageService pageService, SerpApiService serp, SqliteDatabaseService db)
        {
            _pageService = pageService;
            _serp = serp;
            _db = db;
            _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

            SeriesCollection = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Lowest offer (USD)",
                    Values = new ChartValues<ObservableValue>(),
                    Stroke = (Brush)new BrushConverter().ConvertFromString("#6366F1"),
                    Fill = Brushes.Transparent,
                    PointGeometrySize = 8
                }
            };

            SearchCommand = new RelayCommand(() => _ = RunSearchFromQueryAsync(), () => !IsLoadingPage && !IsLoadingPrices && !string.IsNullOrWhiteSpace(SearchQuery));
            RetryCommand = new RelayCommand(() => _ = RetryLastAsync(), () => !string.IsNullOrWhiteSpace(_lastProcessedUrl) && !IsLoadingPage && !IsLoadingPrices);
            TrackCommand = new RelayCommand(TrackCurrent, () => CurrentProduct != null && !IsLoadingPrices);
            RefreshTrackedCommand = new RelayCommand(() => LoadTrackedFromDb());
            DismissNotificationCommand = new RelayCommand<AppNotification>(n => { if (n != null) Notifications.Remove(n); });
            OpenLinkCommand = new RelayCommand<string>(url =>
            {
                if (string.IsNullOrWhiteSpace(url) || url == "#") return;
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
                catch { /* ignore */ }
            });
            SaveApiKeyCommand = new RelayCommand(SaveApiKey);

            try
            {
                SerpApiKeyInput = Settings.Default.SerpApiKey ?? "";
            }
            catch
            {
                SerpApiKeyInput = "";
            }
            RefreshSerpKeyStatus();

            var minutes = 45;
            var cfg = ConfigurationManager.AppSettings["PriceMonitorIntervalMinutes"];
            if (int.TryParse(cfg, out var m) && m >= 5 && m <= 120) minutes = m;
            _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(minutes) };
            _monitorTimer.Tick += (_, __) => _ = RunMonitorTickAsync();
            _monitorTimer.Start();

            LoadTrackedFromDb();
        }

        public SeriesCollection SeriesCollection { get; }
        public Func<double, string> YFormatter => v => "$" + v.ToString("0.00");

        private string _searchQuery;
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                    SearchCommand.RaiseCanExecuteChanged();
            }
        }

        private Product _currentProduct;
        public Product CurrentProduct
        {
            get => _currentProduct;
            set
            {
                if (SetProperty(ref _currentProduct, value))
                {
                    TrackCommand.RaiseCanExecuteChanged();
                    RaisePropertyChanged(nameof(HasCurrentProduct));
                }
            }
        }

        public bool HasCurrentProduct => CurrentProduct != null;

        private bool _isLoadingPage;
        public bool IsLoadingPage
        {
            get => _isLoadingPage;
            set
            {
                if (SetProperty(ref _isLoadingPage, value))
                {
                    SearchCommand.RaiseCanExecuteChanged();
                    RetryCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private bool _isLoadingPrices;
        public bool IsLoadingPrices
        {
            get => _isLoadingPrices;
            set
            {
                if (SetProperty(ref _isLoadingPrices, value))
                {
                    SearchCommand.RaiseCanExecuteChanged();
                    RetryCommand.RaiseCanExecuteChanged();
                    TrackCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        private string _statusText = "Bridge: starting…";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private Recommendation _recommendation;
        public Recommendation Recommendation
        {
            get => _recommendation;
            set
            {
                if (SetProperty(ref _recommendation, value))
                {
                    RaisePropertyChanged(nameof(RecommendationBadgeText));
                    RaisePropertyChanged(nameof(RecommendationBrushKey));
                }
            }
        }

        public string RecommendationBadgeText
        {
            get
            {
                if (Recommendation == null) return "—";
                switch (Recommendation.Verdict)
                {
                    case RecommendationVerdict.BuyNow: return "BUY NOW";
                    case RecommendationVerdict.Wait: return "WAIT";
                    default: return "NEUTRAL";
                }
            }
        }

        public string RecommendationBrushKey
        {
            get
            {
                if (Recommendation == null) return "NeutralBadgeBrush";
                switch (Recommendation.Verdict)
                {
                    case RecommendationVerdict.BuyNow: return "BuyBadgeBrush";
                    case RecommendationVerdict.Wait: return "WaitBadgeBrush";
                    default: return "NeutralBadgeBrush";
                }
            }
        }

        private decimal? _lowestPrice;
        public decimal? LowestPrice
        {
            get => _lowestPrice;
            set
            {
                if (SetProperty(ref _lowestPrice, value))
                    RaisePropertyChanged(nameof(LowestPriceDisplay));
            }
        }

        public string LowestPriceDisplay => LowestPrice.HasValue ? LowestPrice.Value.ToString("C", System.Globalization.CultureInfo.CurrentCulture) : "—";

        private decimal? _averagePrice;
        public decimal? AveragePrice
        {
            get => _averagePrice;
            set
            {
                if (SetProperty(ref _averagePrice, value))
                    RaisePropertyChanged(nameof(AveragePriceDisplay));
            }
        }

        public string AveragePriceDisplay => AveragePrice.HasValue ? AveragePrice.Value.ToString("C", System.Globalization.CultureInfo.CurrentCulture) : "—";

        private string _thresholdText;
        public string ThresholdText
        {
            get => _thresholdText;
            set => SetProperty(ref _thresholdText, value);
        }

        public ObservableCollection<PriceResult> PriceRows { get; } = new ObservableCollection<PriceResult>();
        public ObservableCollection<TrackedProduct> TrackedProducts { get; } = new ObservableCollection<TrackedProduct>();
        public ObservableCollection<AppNotification> Notifications { get; } = new ObservableCollection<AppNotification>();

        public RelayCommand SearchCommand { get; }
        public RelayCommand RetryCommand { get; }
        public RelayCommand TrackCommand { get; }
        public RelayCommand RefreshTrackedCommand { get; }
        public RelayCommand<AppNotification> DismissNotificationCommand { get; }
        public RelayCommand<string> OpenLinkCommand { get; }
        public RelayCommand SaveApiKeyCommand { get; }

        private string _serpApiKeyInput;
        public string SerpApiKeyInput
        {
            get => _serpApiKeyInput;
            set => SetProperty(ref _serpApiKeyInput, value);
        }

        public string SerpApiKeyStatusText
        {
            get
            {
                if (!_serp.HasApiKey)
                    return "No SerpAPI key — paste your key below and click Save, set user env SERPAPI_KEY, or edit App.config.";
                if (SerpApiKeyResolver.IsFromEnvironment())
                    return "Using SERPAPI_KEY from the environment (takes priority over saved key).";
                return "SerpAPI key is configured. US Google Shopping (gl=us) is used for comparisons.";
            }
        }

        private void RefreshSerpKeyStatus()
        {
            RaisePropertyChanged(nameof(SerpApiKeyStatusText));
        }

        private void SaveApiKey()
        {
            try
            {
                Settings.Default.SerpApiKey = (SerpApiKeyInput ?? "").Trim();
                Settings.Default.Save();
            }
            catch (Exception ex)
            {
                PushNotification(NotificationKind.Error, "Settings", "Could not save API key: " + ex.Message);
                return;
            }

            _serp.SetApiKey(SerpApiKeyResolver.Resolve());
            RefreshSerpKeyStatus();
            PushNotification(NotificationKind.Info, "SerpAPI", "API key saved. Click Search or Retry to refresh prices.");

            if (!string.IsNullOrWhiteSpace(SearchQuery))
                _ = RunSearchFromQueryAsync();
        }

        private TrackedProduct _selectedTracked;
        public TrackedProduct SelectedTracked
        {
            get => _selectedTracked;
            set
            {
                if (SetProperty(ref _selectedTracked, value))
                    ApplyChartForSelectedTracked();
            }
        }

        public void OnBridgeClientCountChanged(int count)
        {
            RunOnUi(() =>
            {
                StatusText = count > 0
                    ? "Bridge: extension connected (" + count + ")"
                    : "Bridge: waiting for extension (ws://127.0.0.1:22345)";
            });
        }

        public void OnProductUrlFromExtension(string url)
        {
            _lastProcessedUrl = url;
            _ = ProcessProductUrlAsync(url, CancellationToken.None);
        }

        private async Task RetryLastAsync()
        {
            if (string.IsNullOrWhiteSpace(_lastProcessedUrl)) return;
            await ProcessProductUrlAsync(_lastProcessedUrl, CancellationToken.None).ConfigureAwait(false);
        }

        private async Task RunSearchFromQueryAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery)) return;
            _pipelineCts?.Cancel();
            _pipelineCts = new CancellationTokenSource();
            var ct = _pipelineCts.Token;
            await FetchPricesForQueryAsync(SearchQuery.Trim(), null, ct).ConfigureAwait(false);
        }

        public async Task ProcessProductUrlAsync(string productUrl, CancellationToken ct)
        {
            _pipelineCts?.Cancel();
            _pipelineCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            ct = _pipelineCts.Token;

            RunOnUi(() =>
            {
                ErrorMessage = null;
                IsLoadingPage = true;
                IsLoadingPrices = false;
                PriceRows.Clear();
                Recommendation = null;
                LowestPrice = null;
                AveragePrice = null;
                ClearSessionChart();
            });

            try
            {
                var product = await _pageService.LoadProductAsync(productUrl, ct).ConfigureAwait(false);
                RunOnUi(() =>
                {
                    CurrentProduct = product;
                    SearchQuery = product.Name;
                    IsLoadingPage = false;
                });

                _db.InsertSearchHistory(product.Name);
                _db.InsertViewedProduct(productUrl, product.Name);

                await FetchPricesForQueryAsync(product.Name, product, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                RunOnUi(() =>
                {
                    IsLoadingPage = false;
                    IsLoadingPrices = false;
                    ErrorMessage = "Could not load the product page. " + ex.Message;
                    PushNotification(NotificationKind.Error, "Product page", ErrorMessage);
                });
            }
        }

        private async Task FetchPricesForQueryAsync(string query, Product sessionProduct, CancellationToken ct)
        {
            RunOnUi(() =>
            {
                ErrorMessage = null;
                IsLoadingPrices = true;
                PriceRows.Clear();
                Recommendation = null;
                LowestPrice = null;
                AveragePrice = null;
            });

            if (!_serp.HasApiKey)
            {
                RunOnUi(() =>
                {
                    IsLoadingPrices = false;
                    ErrorMessage =
                        "SerpAPI key is missing. Paste your key in the sidebar and click Save, set the user environment variable SERPAPI_KEY, or add SerpApiKey to PriceDropCatcher.exe.config next to the executable.";
                    PushNotification(NotificationKind.Error, "SerpAPI", ErrorMessage);
                });
                return;
            }

            try
            {
                var rows = await _serp.SearchGoogleShoppingAsync(query, ct).ConfigureAwait(false);
                var comparison = PriceComparisonService.Build(rows);
                var rec = RecommendationEngine.Evaluate(comparison, sessionProduct?.PagePriceUsd);

                RunOnUi(() =>
                {
                    foreach (var r in comparison.Rows)
                        PriceRows.Add(r);
                    LowestPrice = comparison.LowestPrice;
                    AveragePrice = comparison.AveragePrice;
                    Recommendation = rec;
                    IsLoadingPrices = false;
                    if (comparison.Rows == null || comparison.Rows.Count == 0)
                    {
                        ErrorMessage =
                            "SerpAPI responded but returned no Amazon, Walmart, or eBay offers for this US search (gl=us). Try a shorter search text, or check the SerpAPI dashboard for quota/errors.";
                    }
                });

                AppendChartPoint(comparison.LowestPrice);

                if (comparison.BestDealRow != null)
                {
                    RunOnUi(() => PushNotification(NotificationKind.BestDeal, "Best deal",
                        comparison.BestDealRow.Platform + " — " + comparison.BestDealRow.PriceDisplay));
                }
            }
            catch (Exception ex)
            {
                RunOnUi(() =>
                {
                    IsLoadingPrices = false;
                    ErrorMessage = "SerpAPI request failed. " + ex.Message;
                    PushNotification(NotificationKind.Error, "Shopping search", ErrorMessage);
                });
            }
        }

        private void TrackCurrent()
        {
            if (CurrentProduct == null) return;
            decimal? th = null;
            if (!string.IsNullOrWhiteSpace(ThresholdText) &&
                decimal.TryParse(ThresholdText.Trim(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.CurrentCulture, out var t))
                th = t;

            var id = _db.SaveTrackedProduct(CurrentProduct.SourceUrl, CurrentProduct.Name, th);
            LoadTrackedFromDb();
            RunOnUi(() =>
            {
                SelectedTracked = TrackedProducts.FirstOrDefault(x => x.Id == id);
                PushNotification(NotificationKind.Info, "Tracking",
                    "Saved \"" + CurrentProduct.Name + "\"" + (th.HasValue ? " with threshold $" + th.Value.ToString("0.00") : "") + ".");
            });
        }

        private void LoadTrackedFromDb()
        {
            var list = _db.GetTrackedProducts();
            RunOnUi(() =>
            {
                TrackedProducts.Clear();
                foreach (var x in list) TrackedProducts.Add(x);
            });
        }

        private void PushNotification(NotificationKind kind, string title, string message)
        {
            Notifications.Insert(0, new AppNotification
            {
                Kind = kind,
                Title = title,
                Message = message,
                CreatedAtUtc = DateTime.UtcNow
            });
            while (Notifications.Count > 40)
                Notifications.RemoveAt(Notifications.Count - 1);
        }

        private void RunOnUi(Action a)
        {
            if (_dispatcher.CheckAccess()) a();
            else _dispatcher.Invoke(a);
        }

        private void ClearSessionChart()
        {
            var line = (LineSeries)SeriesCollection[0];
            var vals = (ChartValues<ObservableValue>)line.Values;
            vals.Clear();
        }

        private void AppendChartPoint(decimal? lowest)
        {
            if (!lowest.HasValue) return;
            RunOnUi(() =>
            {
                var line = (LineSeries)SeriesCollection[0];
                var vals = (ChartValues<ObservableValue>)line.Values;
                vals.Add(new ObservableValue((double)lowest.Value));
                while (vals.Count > 36)
                    vals.RemoveAt(0);
            });
        }

        private void ApplyChartForSelectedTracked()
        {
            if (SelectedTracked == null) return;
            var pts = _db.GetSnapshotsForTracked(SelectedTracked.Id, 36);
            RunOnUi(() =>
            {
                var line = (LineSeries)SeriesCollection[0];
                var vals = (ChartValues<ObservableValue>)line.Values;
                vals.Clear();
                foreach (var p in pts)
                    if (p.Min.HasValue)
                        vals.Add(new ObservableValue((double)p.Min.Value));
            });
        }

        private async Task RunMonitorTickAsync()
        {
            if (!_serp.HasApiKey) return;
            var tracked = _db.GetTrackedProducts();
            foreach (var t in tracked)
            {
                try
                {
                    var rows = await _serp.SearchGoogleShoppingAsync(t.Name, CancellationToken.None).ConfigureAwait(false);
                    var comparison = PriceComparisonService.Build(rows);
                    var newMin = comparison.LowestPrice;
                    var utc = DateTime.UtcNow;

                    _db.InsertPriceSnapshot(t.Id, newMin, comparison.AveragePrice, utc);
                    _db.UpdateTrackedPriceState(t.Id, newMin, utc);

                    if (t.LastMinPrice.HasValue && newMin.HasValue && newMin.Value < t.LastMinPrice.Value - 0.01m)
                    {
                        RunOnUi(() => PushNotification(NotificationKind.PriceDrop, "Price drop",
                            t.Name + " — was $" + t.LastMinPrice.Value.ToString("0.00") + ", now $" + newMin.Value.ToString("0.00")));
                    }

                    if (t.Threshold.HasValue && newMin.HasValue && newMin.Value <= t.Threshold.Value)
                    {
                        RunOnUi(() => PushNotification(NotificationKind.PriceDrop, "Threshold hit",
                            t.Name + " at or below your $" + t.Threshold.Value.ToString("0.00") + " target."));
                    }
                }
                catch
                {
                    // background: skip row
                }
            }

            RunOnUi(LoadTrackedFromDb);
            RunOnUi(() =>
            {
                if (SelectedTracked != null)
                {
                    var id = SelectedTracked.Id;
                    SelectedTracked = TrackedProducts.FirstOrDefault(x => x.Id == id);
                    ApplyChartForSelectedTracked();
                }
            });
        }
    }
}
