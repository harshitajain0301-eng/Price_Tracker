using System.Windows;
using PriceDropCatcher.Communication;
using PriceDropCatcher.ViewModels;

namespace PriceDropCatcher
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel, WebSocketServer webSocketServer)
        {
            InitializeComponent();
            DataContext = viewModel;

            viewModel.OnBridgeClientCountChanged(webSocketServer?.ConnectedClientsCount ?? 0);

            if (webSocketServer == null) return;

            webSocketServer.ProductUrlReceived += (_, args) =>
            {
                Dispatcher.Invoke(() => viewModel.OnProductUrlFromExtension(args.ProductUrl, args.SuggestedProductName));
            };
            webSocketServer.ClientConnected += (_, __) =>
            {
                Dispatcher.Invoke(() => viewModel.OnBridgeClientCountChanged(webSocketServer.ConnectedClientsCount));
            };
            webSocketServer.ClientDisconnected += (_, __) =>
            {
                Dispatcher.Invoke(() => viewModel.OnBridgeClientCountChanged(webSocketServer.ConnectedClientsCount));
            };
        }
    }
}
