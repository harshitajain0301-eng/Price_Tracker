using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows;
using PriceDropCatcher.Communication;
using PriceDropCatcher.Models;

namespace PriceDropCatcher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly WebSocketServer _wsServer;

        public MainWindow(WebSocketServer wsServer)
        {
            _wsServer = wsServer;
            InitializeComponent();
            var vm = new MainWindowViewModel();
            DataContext = vm;

            if (_wsServer != null)
            {
                vm.StatusText = "Bridge: waiting for extension (127.0.0.1:22345)";
                _wsServer.ProductTracked += (_, product) =>
                {
                    Dispatcher.Invoke(() => vm.AddOrSelect(product));
                };

                _wsServer.ClientConnected += (_, __) =>
                {
                    Dispatcher.Invoke(() => vm.StatusText = $"Bridge: connected ({_wsServer.ConnectedClientsCount} client)");
                };
                _wsServer.ClientDisconnected += (_, __) =>
                {
                    Dispatcher.Invoke(() => vm.StatusText = $"Bridge: waiting for extension (127.0.0.1:22345)");
                };
            }
            else
            {
                vm.StatusText = "Bridge: unavailable";
            }
        }
    }

    internal class MainWindowViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<TrackedProduct> Products { get; } = new ObservableCollection<TrackedProduct>();

        private TrackedProduct _selectedProduct;
        public TrackedProduct SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (Equals(_selectedProduct, value)) return;
                _selectedProduct = value;
                OnPropertyChanged(nameof(SelectedProduct));
                OnPropertyChanged(nameof(SelectedPriceText));
            }
        }

        private string _statusText;
        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText == value) return;
                _statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public string SelectedPriceText
        {
            get
            {
                if (SelectedProduct == null) return "";
                if (string.IsNullOrWhiteSpace(SelectedProduct.Price)) return "—";
                if (!string.IsNullOrWhiteSpace(SelectedProduct.Currency)) return $"{SelectedProduct.Price} {SelectedProduct.Currency}";
                return SelectedProduct.Price;
            }
        }

        public ICommand CopyUrlCommand { get; }
        public ICommand RemoveSelectedCommand { get; }

        public MainWindowViewModel()
        {
            CopyUrlCommand = new RelayCommand(() =>
            {
                if (SelectedProduct?.Url == null) return;
                Clipboard.SetText(SelectedProduct.Url);
            }, () => SelectedProduct?.Url != null);

            RemoveSelectedCommand = new RelayCommand(() =>
            {
                if (SelectedProduct == null) return;
                var toRemove = SelectedProduct;
                Products.Remove(toRemove);
                SelectedProduct = Products.Count > 0 ? Products[0] : null;
            }, () => SelectedProduct != null);
        }

        public void AddOrSelect(TrackedProduct product)
        {
            if (product == null || string.IsNullOrWhiteSpace(product.Url)) return;

            Products.Insert(0, product);
            SelectedProduct = product;

            OnPropertyChanged(nameof(SelectedPriceText));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    internal class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object parameter) => _execute();

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
