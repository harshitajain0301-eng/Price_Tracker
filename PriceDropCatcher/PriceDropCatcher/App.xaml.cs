using System.Windows;
using PriceDropCatcher.Communication;
using PriceDropCatcher.Data;
using PriceDropCatcher.Services;
using PriceDropCatcher.ViewModels;

namespace PriceDropCatcher
{
    public partial class App : Application
    {
        private const int WsPort = 22345;
        private const int DiscoveryPort = 38999;

        private WebSocketServer _wsServer;
        private HttpDiscoveryServer _discoveryServer;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var db = new SqliteDatabaseService();
            db.Initialize();

            var serp = new SerpApiService(SerpApiKeyResolver.Resolve());
            var pages = new ProductPageService();
            var vm = new MainViewModel(pages, serp, db);

            _wsServer = new WebSocketServer(WsPort);
            _discoveryServer = new HttpDiscoveryServer(DiscoveryPort, WsPort, "1.0");

            _wsServer.StartAsync();
            _discoveryServer.StartAsync();

            var mainWindow = new MainWindow(vm, _wsServer);
            MainWindow = mainWindow;
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { _discoveryServer?.Dispose(); } catch { }
            try { _wsServer?.Dispose(); } catch { }
            base.OnExit(e);
        }
    }
}
