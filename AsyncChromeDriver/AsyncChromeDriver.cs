// Copyright (c) Oleg Zudov. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Zu.Chrome.DriverCore;
using Zu.WebBrowser.BasicTypes;
using Zu.WebBrowser.AsyncInteractions;
using System.IO;
using Zu.Chrome.DevTools;
using Zu.ChromeDevTools;
using Zu.WebBrowser.BrowserOptions;

namespace Zu.Chrome
{
    public class AsyncChromeDriver : IAsyncChromeDriver
    {
        #region IAsyncWebBrowserClient
        public IMouse Mouse => _mouse ?? (_mouse = new ChromeDriverMouse(this));

        public IKeyboard Keyboard => _keyboard ?? (_keyboard = new ChromeDriverKeyboard(this));

        public IOptions Options => _options ?? (_options = new ChromeDriverOptions(this));

        public IAlert Alert => _alert ?? (_alert = new ChromeDriverAlert(this));

        public ICoordinates Coordinates => _coordinates ?? (_coordinates = new ChromeDriverCoordinates(this));

        public ITakesScreenshot Screenshot => _screenshot ?? (_screenshot = new ChromeDriverScreenshot(this));

        public ITouchScreen TouchScreen => _touchScreen ?? (_touchScreen = new ChromeDriverTouchScreen(this));

        public INavigation Navigation => _navigation ?? (_navigation = new ChromeDriverNavigation(this));

        public IJavaScriptExecutor JavaScriptExecutor => _javaScriptExecutor ?? (_javaScriptExecutor = new ChromeDriverJavaScriptExecutor(this));

        public ITargetLocator TargetLocator => _targetLocator ?? (_targetLocator = new ChromeDriverTargetLocator(this));

        public IElements Elements => _elements ?? (_elements = new ChromeDriverElements(this));

        public IActionExecutor ActionExecutor => _actionExecutor ?? (_actionExecutor = new ChromeDriverActionExecutor(this));

        private ChromeDriverNavigation _navigation;
        private ChromeDriverTouchScreen _touchScreen;
        private ChromeDriverScreenshot _screenshot;
        private ChromeDriverCoordinates _coordinates;
        private ChromeDriverAlert _alert;
        private ChromeDriverOptions _options;
        private ChromeDriverKeyboard _keyboard;
        private ChromeDriverMouse _mouse;
        private ChromeDriverJavaScriptExecutor _javaScriptExecutor;
        private ChromeDriverTargetLocator _targetLocator;
        private ChromeDriverElements _elements;
        private ChromeDriverActionExecutor _actionExecutor;
        #endregion

        public bool IsConnected = false;
        public ChromeDevToolsConnection DevTools {
            get;
            set;
        }

        public FrameTracker FrameTracker {
            get;
            private set;
        }

        public DomTracker DomTracker {
            get;
            private set;
        }

        public Session Session {
            get;
            private set;
        }

        public WebView WebView {
            get;
            private set;
        }

        public ElementCommands ElementCommands {
            get;
            private set;
        }

        public ElementUtils ElementUtils {
            get;
            private set;
        }

        public WindowCommands WindowCommands {
            get;
            private set;
        }

        public ChromeDriverConfig Config {
            get;
            set;
        }

        public int Port {
            get => Config.Port;
            set => Config.Port = value;
        }

        public string UserDir {
            get => Config.UserDir;
            set => Config.SetUserDir(value);
        }

        public bool IsTempProfile {
            get => Config.IsTempProfile;
            set => Config.IsTempProfile = value;
        }

        public bool DoConnectWhenCheckConnected {
            get;
            set;
        } = true;

        static int _sessionId = 0;
        public ChromeProcessInfo ChromeProcess;
        private bool _isClosed = false;
        public delegate void DevToolsEventHandler(object sender, string methodName, JToken eventData);
        public event DevToolsEventHandler DevToolsEvent;
        public AsyncChromeDriver BrowserDevTools {
            get;
            set;
        }

        public ChromeDriverConfig BrowserDevToolsConfig {
            get;
            set;
        }

        static Random _rnd = new Random();
        public AsyncChromeDriver(bool openInTempDir = true) : this(11000 + _rnd.Next(2000))
        {
            Config.SetIsTempProfile(openInTempDir);
        }

        public AsyncChromeDriver(string profileDir, int port) : this(port)
        {
            UserDir = profileDir;
        }

        public AsyncChromeDriver(string profileDir) : this(11000 + _rnd.Next(2000))
        {
            UserDir = profileDir;
        }

        public AsyncChromeDriver(DriverConfig config) : this(config as ChromeDriverConfig ?? new ChromeDriverConfig(config))
        {
        }

        public AsyncChromeDriver(ChromeDriverConfig config, ILogger<ChromeSession> logger = null)
        {
            Config = config;
            if (Config.Port == 0)
                Config.Port = 11000 + _rnd.Next(2000);
            if (Config.DoOpenWSProxy || Config.DoOpenBrowserDevTools) {
                if (Config.DevToolsConnectionProxyPort == 0)
                    Config.DevToolsConnectionProxyPort = 15000 + _rnd.Next(2000);
                DevTools = new BrowserDevTools.ChromeDevToolsConnectionProxy(Port, Config.DevToolsConnectionProxyPort, Config.WSProxyConfig, logger);
            } else
                DevTools = new ChromeDevToolsConnection(Port, logger);
            CreateDriverCore();
        }

        public AsyncChromeDriver(int port)
        {
            Config = new ChromeDriverConfig();
            Port = port;
            DevTools = new ChromeDevToolsConnection(Port);
            CreateDriverCore();
        }

        public void CreateDriverCore()
        {
            Session = new Session(_sessionId++, this);
            FrameTracker = new FrameTracker(DevTools);
            DomTracker = new DomTracker(DevTools);
            WebView = new WebView(DevTools, FrameTracker, this);
            //Mouse = new ChromeDriverMouse(WebView, Session);
            //Keyboard = new ChromeDriverKeyboard(WebView);
            //Options = new BrowserOptions();
            ElementUtils = new ElementUtils(WebView, Session);
            ElementCommands = new ElementCommands(this);
            WindowCommands = new WindowCommands(this);
        }

        public virtual async Task<string> Connect(CancellationToken ct = default)
        {
            // While having CancellationToken here - is always default, because SyncWebDriver.Open - has no CancellationToken at all.
            // So use manual timeout 1 minute that should be enough to start or to fail (Chrome may start slowly for first time).
            ct = CancellationTokenSource.CreateLinkedTokenSource(ct, new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token).Token;

            IsConnected = true;
            UnsubscribeDevToolsSessionEvent();
            DoConnectWhenCheckConnected = false;
            if (!Config.DoNotOpenChromeProfile) {
                ChromeProcess = await OpenChromeProfile(Config).ConfigureAwait(false);
                if (Config.IsTempProfile)
                    await Task.Delay(Config.TempDirCreateDelay, ct).ConfigureAwait(false);
            }

            await DevTools.Connect(ct).ConfigureAwait(false);
            SubscribeToDevToolsSessionEvent();
            await FrameTracker.Enable().ConfigureAwait(false);
            await DomTracker.Enable().ConfigureAwait(false);
            if (Config.DoOpenBrowserDevTools)
                await OpenBrowserDevTools().ConfigureAwait(false);
            return $"Connected to Chrome port {Port}";
        }

        public string GetBrowserDevToolsUrl()
        {
            var httpPort = Config?.WSProxyConfig?.DoProxyHttpTraffic == true ? Config.WSProxyConfig.HttpServerPort : Port;
            return "http://127.0.0.1:" + httpPort + "/devtools/inspector.html?ws=127.0.0.1:" + Config?.DevToolsConnectionProxyPort + "/WSProxy";
        }

        public virtual async Task OpenBrowserDevTools()
        {
            if (BrowserDevToolsConfig == null)
                BrowserDevToolsConfig = new ChromeDriverConfig();
            BrowserDevTools = new AsyncChromeDriver(BrowserDevToolsConfig);
            await BrowserDevTools.Navigation.GoToUrl(GetBrowserDevToolsUrl()).ConfigureAwait(false);
        }

        public async Task CheckConnected(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!DoConnectWhenCheckConnected)
                return;
            DoConnectWhenCheckConnected = false;
            if (!IsConnected) {
                await Connect(cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<ChromeProcessInfo> OpenChromeProfile(ChromeDriverConfig config)
        {
            ChromeProcessInfo res = null;
            await Task.Run(() => res = ChromeProfilesWorker.OpenChromeProfile(config)).ConfigureAwait(false); // userDir, Port, isHeadless));
            return res;
        }

        public void CloseSync()
        {
            BrowserDevTools?.CloseSync();
            if (IsConnected) {
                DevTools.Disconnect();
                IsConnected = false;
            }

            if (ChromeProcess?.Proc != null && !ChromeProcess.Proc.HasExited) {
                try {
                    ChromeProcess.Proc.CloseMainWindow();
                    ChromeProcess.Proc.Close();
                } catch {
                    try {
                        ChromeProcess.Proc.Kill();
                    } catch {
                        // ignored
                    }
                }

                try {
                    while (!ChromeProcess.Proc.HasExited) {
                        Thread.Sleep(250);
                    }
                } catch {
                    // ignored
                }
            }

            ChromeProcess?.Proc?.Dispose();
            ChromeProcess?.ProcWithJobObject?.TerminateProc();

            ChromeProcess = null;
            Thread.Sleep(1000);
            if (IsTempProfile && !string.IsNullOrWhiteSpace(UserDir)) {
                try {
                    if (Directory.Exists(UserDir))
                        Directory.Delete(UserDir, true);
                } catch {
                    Thread.Sleep(3000);
                    try {
                        if (Directory.Exists(UserDir))
                            Directory.Delete(UserDir, true);
                    } catch {
                        // ignored
                    }
                }
            }
        }

        public async Task<string> Close(CancellationToken cancellationToken = default(CancellationToken))
        {
            try {
                if (BrowserDevTools != null)
                    await BrowserDevTools.Close(cancellationToken).ConfigureAwait(false);
            } catch {
                // ignored
            }

            if (IsConnected)
                await Disconnect().ConfigureAwait(false);
            if (ChromeProcess?.Proc != null && !ChromeProcess.Proc.HasExited) {
                try {
                    ChromeProcess.Proc.CloseMainWindow();
                    ChromeProcess.Proc.Close();
                } catch {
                    try {
                        ChromeProcess.Proc.Kill();
                    } catch {
                        // ignored
                    }
                }

                try {
                    while (!ChromeProcess.Proc.HasExited) {
                        await Task.Delay(250).ConfigureAwait(false);
                    }
                } catch {
                    //
                }
            }

            ChromeProcess?.Proc?.Dispose();
            if (ChromeProcess?.ProcWithJobObject != null) {
                ChromeProcess.ProcWithJobObject.TerminateProc();
            }

            ChromeProcess = null;
            await Task.Delay(1000).ConfigureAwait(false);
            if (IsTempProfile && !string.IsNullOrWhiteSpace(UserDir)) {
                try {
                    if (Directory.Exists(UserDir))
                        Directory.Delete(UserDir, true);
                } catch {
                    await Task.Delay(3000).ConfigureAwait(false);
                    try {
                        if (Directory.Exists(UserDir))
                            Directory.Delete(UserDir, true);
                    } catch {
                        // ignored
                    }
                }
            }

            return "ok";
        }

        public async Task<string> GetPageSource(CancellationToken cancellationToken = default(CancellationToken))
        {
            var res = await WindowCommands.GetPageSource(null, cancellationToken).ConfigureAwait(false);
            return res;
        }

        public async Task<string> GetTitle(CancellationToken cancellationToken = default(CancellationToken))
        {
            var res = await WindowCommands.GetTitle(null, cancellationToken).ConfigureAwait(false);
            return res;
        }

        protected void SubscribeToDevToolsSessionEvent()
        {
            DevTools.Session.DevToolsEvent += DevToolsSessionEvent;
        }

        protected void UnsubscribeDevToolsSessionEvent()
        {
            if (DevTools.Session != null)
                DevTools.Session.DevToolsEvent -= DevToolsSessionEvent;
        }

        private void DevToolsSessionEvent(object sender, string methodName, JToken eventData)
        {
            DevToolsEvent?.Invoke(sender, methodName, eventData);
        }

        public async Task Disconnect(CancellationToken cancellationToken = default(CancellationToken))
        {
            await Task.Run(() => DevTools.Disconnect()).ConfigureAwait(false);
            IsConnected = false;
            //DoConnectWhenCheckConnected = true;
        }

        public async Task<DevToolsCommandResult> SendDevToolsCommand(DevToolsCommandData commandData, CancellationToken cancellationToken = default(CancellationToken))
        {
            try {
                var res = await DevTools.Session.SendCommand(commandData.CommandName, commandData.Params, cancellationToken, commandData.MillisecondsTimeout).ConfigureAwait(false);
                return new DevToolsCommandResult { Id = commandData.Id, Result = res };
            } catch (Exception ex) {
                return new DevToolsCommandResult { Id = commandData.Id, Error = ex.ToString() };
            }
        }
    }
}