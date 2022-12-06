// Copyright (c) Oleg Zudov. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
// This file is based on or incorporates material from the Chromium Projects, licensed under the BSD-style license. More info in THIRD-PARTY-NOTICES file.
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Globalization;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Zu.ChromeDevTools.DOM;
using Zu.ChromeDevTools.Input;
using Zu.ChromeDevTools.Page;
using Zu.ChromeDevTools.Runtime;
using Zu.WebBrowser.BasicTypes;

namespace Zu.Chrome.DriverCore
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class WebView
    {
        public ChromeDevToolsConnection DevTools;
        private FrameTracker FrameTracker;
        private AsyncChromeDriver _asyncChromeDriver;
        public WebView(ChromeDevToolsConnection devTools, FrameTracker frameTracker, AsyncChromeDriver asyncChromeDriver)
        {
            DevTools = devTools;
            FrameTracker = frameTracker;
            _asyncChromeDriver = asyncChromeDriver;
        }

        public async Task<EvaluateCommandResponse> CallFunction(string function, /*JToken*/
        string /*[]*/ args, string frame = null, bool returnByValue = true, bool w3c = false, CancellationToken cancellationToken = default (CancellationToken))
        {
            var expression = $"({call_function.JsSource}).apply(null, [null, {function}, [{args}], {w3c.ToString().ToLower()}])";
            var res = await EvaluateScript(expression, frame, returnByValue, cancellationToken).ConfigureAwait(false);
            //var res = await devTools?.Session.Runtime.CallFunctionOn(new CallFunctionOnCommand
            //{
            //    FunctionDeclaration = function,
            //    Arguments = args,
            //});
            return res;
        }

        public async Task<EvaluateCommandResponse> CallFunctionInContext(string function, string args, long ? contextId = null, bool returnByValue = true, bool w3c = false, CancellationToken cancellationToken = default (CancellationToken))
        {
            var expression = $"({call_function.JsSource}).apply(null, [null, {function}, [{args}], {w3c.ToString().ToLower()}])";
            var res = await EvaluateScriptInContext(expression, contextId, returnByValue, cancellationToken).ConfigureAwait(false);
            return res;
        }

        public async Task<string> CallFunctionInContextAndGetObject(string function, string args, long ? contextId = null, bool w3c = false, CancellationToken cancellationToken = default (CancellationToken))
        {
            var res = await CallFunctionInContext(function, args, contextId, false, w3c, cancellationToken).ConfigureAwait(false);
            return res?.Result?.ObjectId;
        }

        public async Task<object> CallUserAsyncFunction(string function, /*JToken*/
        string /*[]*/ args, TimeSpan? scriptTimeout, string frame = null, bool returnByValue = true, bool w3c = false, CancellationToken cancellationToken = default (CancellationToken))
        {
            var asyncArgsList = new List<string>{"\"return (" + function + ").apply(null, arguments);\"", $"[{args}]", "true", scriptTimeout == null || scriptTimeout == default (TimeSpan) ? "0" : ((TimeSpan)scriptTimeout).TotalMilliseconds.ToString()};
            var asyncArgs = string.Join(", ", asyncArgsList);
            //var expression = $"({execute_async_script.JsSource}).apply(null, [null, {function}, [{args}], {w3c.ToString().ToLower()}])";
            //var res = await EvaluateScript(expression, frame, returnByValue, cancellationToken);
            var r1 = await CallFunction(execute_async_script.JsSource, asyncArgs, _asyncChromeDriver.Session?.GetCurrentFrameId()).ConfigureAwait(false);
            var kDocUnloadError = "document unloaded while waiting for result";
            var kJavaScriptError = 17;
            string kQueryResult = string.Format("function() {{" + "  var info = document.$chrome_asyncScriptInfo;" + "  if (!info)" + "    return {{status: {0}, value: '{1}' }};" + "  var result = info.result;" + "  if (!result)" + "    return {{status: 0}};" + "  delete info.result;" + "  return result;" + "}}", kJavaScriptError, kDocUnloadError);
            while (true)
            {
                var queryValue = await CallFunction(kQueryResult, "", frame).ConfigureAwait(false);
                if (queryValue.Result?.Value != null)
                {
                    return queryValue.Result.Value;
                }

                await Task.Delay(100).ConfigureAwait(false);
            }
        }

        public async Task<EvaluateCommandResponse> EvaluateScript(string expression, string frame = null, bool returnByValue = true, CancellationToken cancellationToken = default (CancellationToken))
        {
            if (_asyncChromeDriver != null)
                await _asyncChromeDriver.CheckConnected().ConfigureAwait(false);
            var contextId = string.IsNullOrWhiteSpace(frame) ? null : (long ? )FrameTracker.GetContextIdForFrame(frame);
            return await DevTools.Runtime.Evaluate(new EvaluateCommand{Expression = expression, ContextId = contextId, ReturnByValue = returnByValue}, cancellationToken).ConfigureAwait(false);
        }

        public async Task<EvaluateCommandResponse> EvaluateScriptInContext(string expression, long ? contextId = null, bool returnByValue = true, CancellationToken cancellationToken = default (CancellationToken))
        {
            if (_asyncChromeDriver != null)
                await _asyncChromeDriver.CheckConnected().ConfigureAwait(false);
            return await DevTools.Runtime.Evaluate(new EvaluateCommand{Expression = expression, ContextId = contextId, ReturnByValue = returnByValue}, cancellationToken).ConfigureAwait(false);
        }

        public async Task<string> EvaluateScriptAndGetObject(string expression, string frame = null, //bool returnByValue = true,
        CancellationToken cancellationToken = default (CancellationToken))
        {
            var res = await EvaluateScript(expression, frame, false, cancellationToken).ConfigureAwait(false);
            return ResultValueConverter.GetResultOrThrow(res).ObjectId;
        }

        public async Task<string> EvaluateScriptAndGetObjectInContext(string expression, long ? contextId = null, //bool returnByValue = true,
        CancellationToken cancellationToken = default (CancellationToken))
        {
            var res = await EvaluateScriptInContext(expression, contextId, false, cancellationToken).ConfigureAwait(false);
            return ResultValueConverter.GetResultOrThrow(res).ObjectId;
        }

        public async Task<string> GetUrl(CancellationToken cancellationToken = default (CancellationToken))
        {
            if (_asyncChromeDriver != null)
                await _asyncChromeDriver.CheckConnected(cancellationToken).ConfigureAwait(false);
            var res = await DevTools.Page.GetNavigationHistory(new GetNavigationHistoryCommand(), cancellationToken).ConfigureAwait(false);
            return res.Entries?.ElementAtOrDefault((int)res.CurrentIndex)?.Url;
        }

        public async Task<NavigateCommandResponse> Load(string url, int ? timeout = null, CancellationToken cancellationToken = default (CancellationToken))
        {
            if (_asyncChromeDriver != null)
                await _asyncChromeDriver.CheckConnected(cancellationToken).ConfigureAwait(false);
            var res = await DevTools.Page.Navigate(new NavigateCommand{Url = url}, cancellationToken, timeout).ConfigureAwait(false);
            return res;
        }

        public async Task<string> Reload(CancellationToken cancellationToken = default (CancellationToken))
        {
            if (_asyncChromeDriver != null)
                await _asyncChromeDriver.CheckConnected().ConfigureAwait(false);
            var res = await DevTools.Page.Reload(new ReloadCommand(), cancellationToken).ConfigureAwait(false);
            return res.ToString();
        }

        public async Task<NavigateToHistoryEntryCommandResponse> TraverseHistory(int delta, CancellationToken cancellationToken = default (CancellationToken))
        {
            if (_asyncChromeDriver != null)
                await _asyncChromeDriver.CheckConnected().ConfigureAwait(false);
            var res = await DevTools.Page.GetNavigationHistory(new GetNavigationHistoryCommand(), cancellationToken).ConfigureAwait(false);
            if (delta == -1)
            {
                if (res.CurrentIndex > 0)
                {
                    return await DevTools.Page.NavigateToHistoryEntry(new NavigateToHistoryEntryCommand{EntryId = res.Entries[res.CurrentIndex + delta].Id}, cancellationToken).ConfigureAwait(false);
                //return await EvaluateScript("window.history.back();");
                }
                else
                {
                    return null;
                }
            }
            else if (delta == 1)
                if (res.CurrentIndex + 1 < res.Entries.Count())
                {
                    return await DevTools.Page.NavigateToHistoryEntry(new NavigateToHistoryEntryCommand{EntryId = res.Entries[res.CurrentIndex + delta].Id}, cancellationToken).ConfigureAwait(false);
                //return await EvaluateScript("window.history.forward();");
                }
                else
                {
                    return null;
                }
            else
                return null;
        //else throw new ArgumentOutOfRangeException(nameof(delta));
        }

        internal string SetFileInputFiles(string elementId, string keys, CancellationToken cancellationToken = default (CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task<EvaluateCommandResponse> TraverseHistoryWithJavaScript(int delta, CancellationToken cancellationToken = default (CancellationToken))
        {
            if (delta == -1)
                return EvaluateScript("window.history.back();", null, true, cancellationToken);
            else if (delta == 1)
                return EvaluateScript("window.history.forward();", null, true, cancellationToken);
            else
                return null;
        //else throw new ArgumentOutOfRangeException(nameof(delta));    
        }

        public async Task DispatchKeyEvents(string keys, CancellationToken cancellationToken = default (CancellationToken))
        {
            if (_asyncChromeDriver != null)
                await _asyncChromeDriver.CheckConnected().ConfigureAwait(false);

            // Sometimes only first symbol entered with DispatchKeyEvent and the rest symbols are ignored for unknown reason (maybe some additional page script works incorrectly)
            // But InsertText works correctly!
            if (!keys.Any(k => Keys.KeyToVirtualKeyCode.ContainsKey(k)))
            {
                await DevTools.Input.InsertText(new InsertTextCommand { Text = keys }, cancellationToken).ConfigureAwait(false);
                return;
            }

            foreach (var key in keys)
            {
                //var index = (int)key - 0xE000U; > 0
                if (Keys.KeyToVirtualKeyCode.ContainsKey(key))
                {
                    var virtualKeyCode = Keys.KeyToVirtualKeyCode[key];
                    //if (index == 7 || index == 6)
                    //{
                    //    await DevTools?.Session.Input.DispatchKeyEvent(new DispatchKeyEventCommand
                    //    {
                    //        Type = "char",
                    //        Text = Convert.ToString("\r", CultureInfo.InvariantCulture)
                    //    });
                    //}
                    //else
                    //{
                    if (virtualKeyCode == 0)
                        continue;
                    var res = await DevTools.Input.DispatchKeyEvent(new DispatchKeyEventCommand
                    {
                        Type = "rawKeyDown", //NativeVirtualKeyCode = virtualKeyCode,
                        WindowsVirtualKeyCode = virtualKeyCode,
                    }, cancellationToken).ConfigureAwait(false);
                    await DevTools.Input.DispatchKeyEvent(new DispatchKeyEventCommand
                    {
                        Type = "keyUp", //NativeVirtualKeyCode = virtualKeyCode,
                        WindowsVirtualKeyCode = virtualKeyCode,
                    }, cancellationToken).ConfigureAwait(false);
                //}
                }
                else
                {
                    await DevTools.Input
                        .DispatchKeyEvent(new DispatchKeyEventCommand { Type = "char", Text = Convert.ToString(key, CultureInfo.InvariantCulture) },
                            cancellationToken).ConfigureAwait(false);
                }
            }
        }

        public async Task<string> GetFrameByFunction(string frame, string function, List<string> args, CancellationToken cancellationToken = default (CancellationToken))
        {
            long ? contextId = FrameTracker.GetContextIdForFrame(frame);
            try
            {
                var nodeId = await GetNodeIdFromFunction(contextId, function, args, cancellationToken).ConfigureAwait(false);
                return await _asyncChromeDriver.DomTracker.GetFrameIdForNode(nodeId).ConfigureAwait(false);
            }
            catch
            {
                throw;
            }
        }

        public async Task<int> GetNodeIdFromFunction(long ? contextId, string function, List<string> args, CancellationToken cancellationToken = default (CancellationToken))
        {
            var argsJson = Newtonsoft.Json.JsonConvert.SerializeObject(args);
            var w3 = _asyncChromeDriver.Session.W3CCompliant ? "true" : "false";
            var expression = $"({call_function.JsSource}).apply(null, [null, {function}, {argsJson}, {w3}, true])";
            try
            {
                var elementId = await EvaluateScriptAndGetObjectInContext(expression, null /*context_id*/, cancellationToken).ConfigureAwait(false);
                //var element_id = await CallFunctionInContextAndGetObject(function, argsJson, context_id, asyncChromeDriver.Session.w3c_compliant, cancellationToken);
                long ? nodeId = null;
                var nodeResp = await DevTools.DOM.RequestNode(new RequestNodeCommand{ObjectId = elementId}, cancellationToken).ConfigureAwait(false);
                nodeId = nodeResp?.NodeId;
                if (nodeId == null)
                    throw new Exception("DOM.requestNode missing int 'nodeId'");
                var releaseResp = await DevTools.Runtime.ReleaseObject(new ReleaseObjectCommand{ObjectId = elementId}, cancellationToken).ConfigureAwait(false);
                return (int)nodeId;
            }
            catch
            {
                throw;
            }
        }
    }
}