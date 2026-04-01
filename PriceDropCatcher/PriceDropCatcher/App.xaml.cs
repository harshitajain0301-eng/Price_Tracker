using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using PriceDropCatcher.Communication;

namespace PriceDropCatcher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private const int WsPort = 22345;
        private const int DiscoveryPort = 38999;

        private WebSocketServer _wsServer;
        private HttpDiscoveryServer _discoveryServer;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _wsServer = new WebSocketServer(WsPort);
            _discoveryServer = new HttpDiscoveryServer(DiscoveryPort, WsPort, "1.0");

            _wsServer.StartAsync();
            _discoveryServer.StartAsync();

            var mainWindow = new MainWindow(_wsServer);
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
