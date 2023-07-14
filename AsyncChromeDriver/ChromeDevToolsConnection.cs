// Copyright (c) Oleg Zudov. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Zu.ChromeDevTools;

namespace Zu.Chrome
{
    public class ChromeDevToolsConnection
    {
        private bool _isDisconnected;
        private ChromeSession _session;
        protected readonly ILogger<ChromeSession> _logger;

        public int Port {
            get;
            private set;
        }

        public virtual ChromeSession Session
        {
            get
            {
                if (_isDisconnected)
                    throw new Exception(nameof(ChromeDevToolsConnection) + " already disconnected");
                return _session;
            }
            set => _session = value;
        }

        #region Session properties
        //System.IO.File.WriteAllText("sess.txt", string.Join(Environment.NewLine, typeof(ChromeSession).GetProperties().Where(v => v.PropertyType.FullName.StartsWith("Zu.ChromeDevTools.")).Select(v => $"public {v.PropertyType.FullName.Substring("Zu.ChromeDevTools.".Length)} {v.Name} => Session.{v.Name};").ToList()))
        public ChromeDevTools.Accessibility.AccessibilityAdapter Accessibility => Session.Accessibility;
        public ChromeDevTools.Animation.AnimationAdapter Animation => Session.Animation;
        public ChromeDevTools.ApplicationCache.ApplicationCacheAdapter ApplicationCache => Session.ApplicationCache;
        public ChromeDevTools.Audits.AuditsAdapter Audits => Session.Audits;
        public ChromeDevTools.Browser.BrowserAdapter Browser => Session.Browser;
        public ChromeDevTools.CSS.CSSAdapter CSS => Session.CSS;
        public ChromeDevTools.CacheStorage.CacheStorageAdapter CacheStorage => Session.CacheStorage;
        public ChromeDevTools.DOM.DOMAdapter DOM => Session.DOM;
        public ChromeDevTools.DOMDebugger.DOMDebuggerAdapter DOMDebugger => Session.DOMDebugger;
        public ChromeDevTools.DOMSnapshot.DOMSnapshotAdapter DOMSnapshot => Session.DOMSnapshot;
        public ChromeDevTools.DOMStorage.DOMStorageAdapter DOMStorage => Session.DOMStorage;
        public ChromeDevTools.Database.DatabaseAdapter Database => Session.Database;
        public ChromeDevTools.DeviceOrientation.DeviceOrientationAdapter DeviceOrientation => Session.DeviceOrientation;
        public ChromeDevTools.Emulation.EmulationAdapter Emulation => Session.Emulation;
        public ChromeDevTools.HeadlessExperimental.HeadlessExperimentalAdapter HeadlessExperimental => Session.HeadlessExperimental;
        public ChromeDevTools.IO.IOAdapter IO => Session.IO;
        public ChromeDevTools.IndexedDB.IndexedDBAdapter IndexedDB => Session.IndexedDB;
        public ChromeDevTools.Input.InputAdapter Input => Session.Input;
        public ChromeDevTools.Inspector.InspectorAdapter Inspector => Session.Inspector;
        public ChromeDevTools.LayerTree.LayerTreeAdapter LayerTree => Session.LayerTree;
        public ChromeDevTools.Log.LogAdapter Log => Session.Log;
        public ChromeDevTools.Memory.MemoryAdapter Memory => Session.Memory;
        public ChromeDevTools.Network.NetworkAdapter Network => Session.Network;
        public ChromeDevTools.Overlay.OverlayAdapter Overlay => Session.Overlay;
        public ChromeDevTools.Page.PageAdapter Page => Session.Page;
        public ChromeDevTools.Performance.PerformanceAdapter Performance => Session.Performance;
        public ChromeDevTools.Security.SecurityAdapter Security => Session.Security;
        public ChromeDevTools.ServiceWorker.ServiceWorkerAdapter ServiceWorker => Session.ServiceWorker;
        public ChromeDevTools.Storage.StorageAdapter Storage => Session.Storage;
        public ChromeDevTools.SystemInfo.SystemInfoAdapter SystemInfo => Session.SystemInfo;
        public ChromeDevTools.Target.TargetAdapter Target => Session.Target;
        public ChromeDevTools.Tethering.TetheringAdapter Tethering => Session.Tethering;
        public ChromeDevTools.Tracing.TracingAdapter Tracing => Session.Tracing;
        public ChromeDevTools.Schema.SchemaAdapter Schema => Session.Schema;
        public ChromeDevTools.Runtime.RuntimeAdapter Runtime => Session.Runtime;
        public ChromeDevTools.Debugger.DebuggerAdapter Debugger => Session.Debugger;
        public ChromeDevTools.Console.ConsoleAdapter Console => Session.Console;
        public ChromeDevTools.Profiler.ProfilerAdapter Profiler => Session.Profiler;
        public ChromeDevTools.HeapProfiler.HeapProfilerAdapter HeapProfiler => Session.HeapProfiler;
        #endregion

        public ChromeDevToolsConnection(int port = 5999, ILogger<ChromeSession> logger = null)
        {
            Port = port;
            _logger = logger;
        }

        public virtual async Task Connect(CancellationToken ct)
        {
            var endpointUrl = await GetEndpointUrl(Port, ct).ConfigureAwait(false);
            Session = new ChromeSession(_logger, endpointUrl);
        }

        protected static async Task<string> GetEndpointUrl(int port, CancellationToken ct)
        {
            var webClient = new HttpClient();
            var uriBuilder = new UriBuilder { Scheme = "http", Host = "127.0.0.1", Port = port, Path = "/json" };

            var s = Stopwatch.StartNew();
            while (true)
            {
                try
                {
                    var remoteSessions = await webClient.GetStringAsync(uriBuilder.Uri).ConfigureAwait(false);

                    var sessions = JsonConvert.DeserializeObject<ChromeSessionInfo[]>(remoteSessions);
                    var endpointUrl = sessions.FirstOrDefault(session => session.Type == "page")?.WebSocketDebuggerUrl;
                    if (endpointUrl != null) 
                        return endpointUrl;
                    
                    if (s.Elapsed > TimeSpan.FromSeconds(10))
                        throw new Exception("Cannot get page session from Chrome");
                }
                catch (HttpRequestException ex)
                {
                    if ((ex.InnerException as WebException)?.Status != WebExceptionStatus.ConnectFailure || s.Elapsed > TimeSpan.FromSeconds(10))
                        throw;
                    // If browser starts slowly can get:
                    // System.Net.Http.HttpRequestException: Произошла ошибка при отправке запроса.
                    //   System.Net.WebException: Невозможно соединиться с удаленным сервером
                    //     System.Net.Sockets.SocketException: Подключение не установлено, т.к. конечный компьютер отверг запрос на подключение 127.0.0.1:14683
                }
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
        }

        public virtual void Disconnect()
        {
            Session?.Dispose();
            Session = null;
            _isDisconnected = true;
        }
        ////todo
    }
}