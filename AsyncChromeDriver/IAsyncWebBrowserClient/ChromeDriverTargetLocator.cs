// Copyright (c) Oleg Zudov. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zu.Chrome.DriverCore;
using Zu.WebBrowser.AsyncInteractions;

namespace Zu.Chrome
{
    public class ChromeDriverTargetLocator : ITargetLocator
    {
        private readonly AsyncChromeDriver _asyncChromeDriver;
        public ChromeDriverTargetLocator(AsyncChromeDriver asyncChromeDriver)
        {
            _asyncChromeDriver = asyncChromeDriver;
        }

        public Task<string> GetWindowHandle(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<List<string>> GetWindowHandles(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<string> SwitchToActiveElement(CancellationToken cancellationToken = default (CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task<IAlert> SwitchToAlert(CancellationToken cancellationToken = default (CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task SwitchToDefaultContent(CancellationToken cancellationToken = default (CancellationToken))
        {
            throw new NotImplementedException();
        }

        public async Task SwitchToFrame(int frameIndex, CancellationToken cancellationToken = default (CancellationToken))
        {
            var script = "function(xpath) {" + "  return document.evaluate(xpath, document, null, " + "      XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue;" + "}";
            var xpath = $"(/html/body//iframe|/html/frameset//frame)[{frameIndex + 1}]";
            var args = new List<object>{xpath}; //$"\"{xpath}\"" };

            var frame = await _asyncChromeDriver.WebView.GetFrameByFunction(_asyncChromeDriver.Session.GetCurrentFrameId(), script, args, cancellationToken).ConfigureAwait(false);
            var argsJson = Newtonsoft.Json.JsonConvert.SerializeObject(args);
            var res = await _asyncChromeDriver.WebView.CallFunction(script, argsJson, _asyncChromeDriver.Session.GetCurrentFrameId(), true, false, cancellationToken).ConfigureAwait(false);
            var elementId = res.ToElementId(_asyncChromeDriver.Session.GetElementKey());
            await SwitchToFrame(frame, elementId, cancellationToken);
        }

        public async Task SwitchToFrame(string frameName, CancellationToken cancellationToken = default (CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(frameName))
            {
                _asyncChromeDriver.Session?.SwitchToTopFrame();
                return;
            }

            var script = "function(xpath) {" + "  return document.evaluate(xpath, document, null, " + "      XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue;" + "}";
            var xpath = $"(/html/body//iframe|/html/frameset//frame)[@name=\"{frameName}\" or @id=\"{frameName}\"]";
            var args = new List<object>{xpath};

            var frame = await _asyncChromeDriver.WebView.GetFrameByFunction(_asyncChromeDriver.Session.GetCurrentFrameId(), script, args, cancellationToken).ConfigureAwait(false);
            var argsJson = Newtonsoft.Json.JsonConvert.SerializeObject(args);
            var res = await _asyncChromeDriver.WebView.CallFunction(script, argsJson, _asyncChromeDriver.Session.GetCurrentFrameId(), true, false, cancellationToken).ConfigureAwait(false);
            var elementId = res.ToElementId(_asyncChromeDriver.Session.GetElementKey());
            await SwitchToFrame(frame, elementId, cancellationToken);
        }

        public async Task SwitchToFrameByElement(string elementId, CancellationToken cancellationToken = default (CancellationToken))
        {
            var script = "function(elem) { return elem; }";
            var args = new List<object>
            {
                new Dictionary<string, string>
                {
                    [_asyncChromeDriver.Session.GetElementKey()] = elementId
                }
            };
            
            var frame = await _asyncChromeDriver.WebView.GetFrameByFunction(_asyncChromeDriver.Session.GetCurrentFrameId(), script, args, cancellationToken).ConfigureAwait(false);
            await SwitchToFrame(frame, elementId, cancellationToken);
        }

        private async Task SwitchToFrame(string frame, string frameElementId, CancellationToken cancellationToken)
        {
            var chromeDriverId = Util.GenerateId();
            var kSetFrameIdentifier = "function(frame, id) {" +
                                      "  frame.setAttribute('cd_frame_id_', id);" +
                                      "}";
            var argsJson = $"{_asyncChromeDriver.Session.GetElementJsonString(frameElementId)}, \"{chromeDriverId}\"";
            var res2 = await _asyncChromeDriver.WebView.CallFunction(kSetFrameIdentifier, argsJson,
                _asyncChromeDriver.Session.GetCurrentFrameId(), true, false, cancellationToken).ConfigureAwait(false);
            _asyncChromeDriver.Session.SwitchToSubFrame(frame, chromeDriverId);
        }

        public Task SwitchToParentFrame(CancellationToken cancellationToken = default (CancellationToken))
        {
            _asyncChromeDriver.Session?.SwitchToParentFrame();
            return Task.CompletedTask;
        }

        public Task SwitchToWindow(string windowName, CancellationToken cancellationToken = default (CancellationToken))
        {
            throw new NotImplementedException();
        }
    }
}