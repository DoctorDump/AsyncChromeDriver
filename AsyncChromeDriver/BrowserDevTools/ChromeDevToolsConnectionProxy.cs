// Copyright (c) Oleg Zudov. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Zu.ChromeDevTools;
using System;
using System.IO;
using System.Threading.Tasks;
using Zu.ChromeWebSocketProxy;
using System.Threading;

namespace Zu.Chrome.BrowserDevTools
{
    public class ChromeDevToolsConnectionProxy : ChromeDevToolsConnection
    {
        private ChromeWSProxyConfig _wsProxyConfig;
        private HttpServer _httpServer;
        private string _proxyEndpointUrl;

        public int Port2 { get; set; }

        public ProxyWS Proxy  { get; private set; }

        public ChromeDevToolsConnectionProxy(int port = 5999, int port2 = 5888, ChromeDevTools.ILogger<ChromeSession> logger = null) : base(port, logger)
        {
            Port2 = port2;
        }

        static Random _rnd = new Random();
        public ChromeDevToolsConnectionProxy(int port = 5999, int port2 = 5888, ChromeWSProxyConfig wsProxyConfig = null, ChromeDevTools.ILogger<ChromeSession> logger = null) : this(port, port2, logger)
        {
            _wsProxyConfig = wsProxyConfig ?? new ChromeWSProxyConfig();
            if (_wsProxyConfig.HttpServerPort == 0)
                _wsProxyConfig.HttpServerPort = 18000 + _rnd.Next(3000);
        }

        //public bool Connected { get; set; } = false;
        public override async Task Connect(CancellationToken ct)
        {
            var endpointUrl = await GetEndpointUrl(Port, ct).ConfigureAwait(false);
            //var dir = wsProxyConfig.DevToolsFilesDir ?? Directory.GetCurrentDirectory();
            //await LoadDevToolsFiles(dir);
            if (_wsProxyConfig?.DoProxyHttpTraffic == true)
            {
                if (_wsProxyConfig.DevToolsFilesDir == null)
                    _wsProxyConfig.DevToolsFilesDir = Directory.GetCurrentDirectory();
                _wsProxyConfig.ChromePort = Port;
                _httpServer = new HttpServer(_wsProxyConfig); // dir, wsProxyConfig.HTTPServerPort, Port);
            }

            _proxyEndpointUrl = await OpenProxyWS(endpointUrl).ConfigureAwait(false);
            Session = new ChromeSession(_logger, _proxyEndpointUrl);
        }

        private async Task<string> OpenProxyWS(string endpointUrl)
        {
            if (Proxy == null)
            {
                Proxy = new ProxyWS(endpointUrl, Port2);
                Proxy.OnlyLocalConnections = _wsProxyConfig.OnlyLocalConnections;
                await Proxy.Open().ConfigureAwait(false);
            }

            return Proxy.ProxyEndpointUrl;
        }

        public void Close()
        {
            _httpServer?.Stop();
            Proxy?.Stop();
        }

        public override void Disconnect()
        {
            Close();
            Session?.Dispose();
            Session = null;
        }

    }
}