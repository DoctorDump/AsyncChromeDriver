// Copyright (c) Oleg Zudov. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
// This file is based on or incorporates material from the Chromium Projects, licensed under the BSD-style license. More info in THIRD-PARTY-NOTICES file.
using System;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json.Linq;
using Zu.WebBrowser.BasicTypes;
using System.Linq;
using Zu.ChromeDevTools.Runtime;

namespace Zu.Chrome.DriverCore
{
    public class ElementUtils
    {
        public Session Session
        {
            get;
            private set;
        }

        public WebView WebView
        {
            get;
            private set;
        }

        public ElementUtils(WebView webView, Session session)
        {
            Session = session;
            WebView = webView;
        }

        public async Task<(bool Clickable, string Message)> VerifyElementClickable(string elementId, WebPoint location, CancellationToken cancellationToken = new CancellationToken())
        {
            var res = await WebView.CallFunction(atoms.IS_ELEMENT_CLICKABLE, $"{{\"{Session.GetElementKey()}\":\"{elementId}\"}}, {WebPointToJsonString(location)}", Session?.GetCurrentFrameId(), true, false, cancellationToken).ConfigureAwait(false);
            if (!(ResultValueConverter.GetResultOrThrow(res).Value is JObject valueJObject))
                throw new ApplicationException("Value is not JObject");
            var value = valueJObject["value"];
            if (value == null)
                throw new ApplicationException("value is null");
            // https://github.com/SeleniumHQ/selenium/blob/3832787933af19806d552546cf64157127f56b01/javascript/chrome-driver/atoms.js#L220
            var clickable = value["clickable"];
            if (clickable == null)
                throw new ApplicationException("value has no clickable");
            return clickable.Value<bool>() ? (true, "is clickable") : (false, value["message"].Value<string>());
        }

        public string WebPointToJsonString(WebPoint point)
        {
            return $"{{ \"x\": {point.X}, \"y\": {point.Y} }}";
        }

        public string WebRectToJsonString(WebRect rect)
        {
            return $"{{\"left\": {rect.X}, \"top\": {rect.Y}, \"width\": {rect.Width}, \"height\": {rect.Height} }}";
        }

        public async Task<string> ScrollElementIntoView(string elementId, CancellationToken cancellationToken = new CancellationToken())
        {
            // center is used because some overlays (like navigation bars or 'Accept our cookie policy' messaged) may hide target element.
            var func = "function(elem) { return elem.scrollIntoView({block: 'center', inline: 'nearest'}); }";
            return (await WebView.CallFunction(func, $"{{\"{Session.GetElementKey()}\":\"{elementId}\"}}", Session?.GetCurrentFrameId(), true, false, cancellationToken).ConfigureAwait(false)).AsString();
        }

        public async Task<WebPoint> ScrollElementRegionIntoViewHelper(string elementId, WebRect region, bool center = true, string clickableElementId = null, CancellationToken cancellationToken = new CancellationToken())
        {
            await ScrollElementIntoView(elementId, cancellationToken).ConfigureAwait(false);
            var res = await WebView.CallFunction(atoms.GET_LOCATION_IN_VIEW, $"{{\"{Session.GetElementKey()}\":\"{elementId}\"}}, {center.ToString().ToLower()}, {WebRectToJsonString(region)}", Session?.GetCurrentFrameId(), true, false, cancellationToken).ConfigureAwait(false);
            var value = ResultValueConverter.GetResultOrThrow(res).Value;
            var location = ResultValueConverter.ToWebPoint(value);
            if (location == null)
                throw new WebBrowserException($"Failed to get location in view on the current page view\n{value}", "invalid element state");

            if (clickableElementId != null)
            {
                var middle = location.Offset(region.Width / 2, region.Height / 2);
                var (isClickable, message) = await VerifyElementClickable(clickableElementId, middle, cancellationToken).ConfigureAwait(false);
                if (!isClickable)
                    throw new WebBrowserException(message, "invalid element state");
            }

            return location;
        }

        public async Task<string> GetActiveElement(CancellationToken cancellationToken = new CancellationToken())
        {
            var func = "function() { return document.activeElement || document.body }";
            var frameId = Session == null ? "" : Session.GetCurrentFrameId();
            var res = await WebView.CallFunction(func, null, frameId, true, false, cancellationToken).ConfigureAwait(false);
            return res.ToElementId(Session?.GetElementKey());
        //return res?.Result?.Value as JToken;
        }

        public async Task<bool> IsElementFocused(string elementId, CancellationToken cancellationToken = new CancellationToken())
        {
            var activeElement = await GetActiveElement(cancellationToken).ConfigureAwait(false);
            return activeElement == elementId;
        }

        public async Task<string> GetElementAttribute(string elementId, string attributeName, CancellationToken cancellationToken = new CancellationToken())
        {
            return (await WebView.CallFunction(atoms.GET_ATTRIBUTE, $"{{\"{Session.GetElementKey()}\":\"{elementId}\"}}, \"{attributeName}\"", Session?.GetCurrentFrameId(), true, false, cancellationToken).ConfigureAwait(false)).AsString();
        }

        public async Task<WebPoint> GetElementClickableLocation(string elementId, CancellationToken cancellationToken = new CancellationToken())
        {
            var targetElementId = elementId;
            var tagName = await GetElementTagName(targetElementId, cancellationToken).ConfigureAwait(false);
            if (tagName == "area")
            {
                var func = "function (element) {" + "  var map = element.parentElement;" + "  if (map.tagName.toLowerCase() != 'map')" + "    throw new Error('the area is not within a map');" + "  var mapName = map.getAttribute('name');" + "  if (mapName == null)" + "    throw new Error ('area\\'s parent map must have a name');" + "  mapName = '#' + mapName.toLowerCase();" + "  var images = document.getElementsByTagName('img');" + "  for (var i = 0; i < images.length; i++) {" + "    if (images[i].useMap.toLowerCase() == mapName)" + "      return images[i];" + "  }" + "  throw new Error('no img is found for the area');" + "}";
                var frameId = Session == null ? "" : Session.GetCurrentFrameId();
                var res = await WebView.CallFunction(func, $"{{\"{Session?.GetElementKey()}\":\"{targetElementId}\"}}", frameId, true, false, cancellationToken).ConfigureAwait(false);
                targetElementId = res.ToElementId(Session?.GetElementKey());
            //return ResultValueConverter.ToWebPoint(res?.Result?.Value);
            }

            var isDisplayed = await IsElementDisplayed(targetElementId, cancellationToken).ConfigureAwait(false);
            if (isDisplayed)
            {
                var rect = await GetElementRegion(targetElementId, cancellationToken).ConfigureAwait(false);
                var location = await ScrollElementRegionIntoView(targetElementId, rect, true, elementId, cancellationToken).ConfigureAwait(false);
                return location.Offset(rect.Width / 2, rect.Height / 2);
            }

            throw new WebBrowserException("Element is not displayed on the current page view", "invalid element state");
        }

        public async Task<WebRect> GetElementRegion(string elementId, CancellationToken cancellationToken = new CancellationToken())
        {
            //var expression = $"({get_element_region.JsSource}).apply(null, {{\"{Session.GetElementKey()}\":\"{elementId}\"}})";
            //var frameId = Session == null ? "" : Session.GetCurrentFrameId();
            //var res = await webView.EvaluateScript(expression, frameId, true, cancellationToken);
            return (await WebView.CallFunction(get_element_region.JsSource, $"{{\"{Session.GetElementKey()}\":\"{elementId}\"}}", Session?.GetCurrentFrameId(), true, false, cancellationToken).ConfigureAwait(false)).ToWebRect();
        }

        public async Task<string> GetElementTagName(string elementId, CancellationToken cancellationToken = new CancellationToken())
        {
            var func = "function(elem) { return elem.tagName.toLowerCase(); }";
            return (await WebView.CallFunction(func, $"{{\"{Session.GetElementKey()}\":\"{elementId}\"}}", Session?.GetCurrentFrameId(), true, false, cancellationToken).ConfigureAwait(false)).AsString();
        }

        public async Task<string> GetElementText(string elementId, CancellationToken cancellationToken = new CancellationToken())
        {
            return (await WebView.CallFunction(atoms.GET_TEXT, $"{{\"{Session.GetElementKey()}\":\"{elementId}\"}}", Session?.GetCurrentFrameId(), true, false, cancellationToken).ConfigureAwait(false)).AsString();
        }

        public async Task<WebSize> GetElementSize(string elementId, CancellationToken cancellationToken = new CancellationToken())
        {
            return (await WebView.CallFunction(atoms.GET_SIZE, $"{{\"{Session.GetElementKey()}\":\"{elementId}\"}}", Session?.GetCurrentFrameId(), true, false, cancellationToken).ConfigureAwait(false)).ToWebSize();
        }

        public async Task<bool> IsElementDisplayed(string elementId, CancellationToken cancellationToken = new CancellationToken())
        {
            return (await WebView.CallFunction(atoms.IS_DISPLAYED, $"{{\"{Session.GetElementKey()}\":\"{elementId}\"}}", Session?.GetCurrentFrameId(), true, false, cancellationToken).ConfigureAwait(false)).ToBool();
        }

        public async Task<bool> IsElementEnabled(string elementId, CancellationToken cancellationToken = new CancellationToken())
        {
            return (await WebView.CallFunction(atoms.IS_ENABLED, $"{{\"{Session.GetElementKey()}\":\"{elementId}\"}}", Session?.GetCurrentFrameId(), true, false, cancellationToken).ConfigureAwait(false)).ToBool();
        }

        public async Task<bool> IsOptionElementSelected(string elementId, CancellationToken cancellationToken = new CancellationToken())
        {
            return (await WebView.CallFunction(atoms.IS_SELECTED, $"{{\"{Session.GetElementKey()}\":\"{elementId}\"}}", Session?.GetCurrentFrameId(), true, false, cancellationToken).ConfigureAwait(false)).ToBool();
        }

        public async Task<bool> IsOptionElementTogglable(string elementId, CancellationToken cancellationToken = new CancellationToken())
        {
            //var expression = $"({is_option_element_toggleable.JsSource}).apply(null, {{\"{Session.GetElementKey()}\":\"{elementId}\"}})";
            //var frameId = Session == null ? "" : Session.GetCurrentFrameId();
            //var res = await webView.EvaluateScript(expression, frameId, true, cancellationToken);
            return (await WebView.CallFunction(is_option_element_toggleable.JsSource, $"{{\"{Session.GetElementKey()}\":\"{elementId}\"}}", Session?.GetCurrentFrameId(), true, false, cancellationToken).ConfigureAwait(false)).ToBool();
        }

        public async Task<bool> SetOptionElementSelected(string elementId, bool selected = true, CancellationToken cancellationToken = new CancellationToken())
        {
            return (await WebView.CallFunction(atoms.CLICK, $"{{\"{Session.GetElementKey()}\":\"{elementId}\"}}, {selected.ToString().ToLower()}", Session?.GetCurrentFrameId(), true, false, cancellationToken).ConfigureAwait(false)).ToBool();
        }

        public async Task ToggleOptionElement(string elementId, CancellationToken cancellationToken = new CancellationToken())
        {
            var isSelected = await IsOptionElementSelected(elementId, cancellationToken).ConfigureAwait(false);
            await SetOptionElementSelected(elementId, !isSelected, cancellationToken).ConfigureAwait(false);
        }

        public async Task ScrollElementIntoView(string elementId, WebPoint offset = null, CancellationToken cancellationToken = new CancellationToken())
        {
            var region = await GetElementRegion(elementId, cancellationToken).ConfigureAwait(false);
        //var location = await ScrollElementRegionIntoView(elementId, region, false);
        //if(offset != null)
        //{
        //    location = location.Offset(offset.X, offset.Y);
        //}
        //else
        //{
        //    location = location.Offset(region.Size.Width / 2, region.Size.Height / 2);
        //}
        }

        public async Task<WebPoint> ScrollElementRegionIntoView(string elementId, WebRect region, bool center, string clickableElementId = null, CancellationToken cancellationToken = new CancellationToken())
        {
            var regionOffset = await ScrollElementRegionIntoViewHelper(elementId, region, center, clickableElementId, cancellationToken).ConfigureAwait(false);
            if (Session?.Frames?.Any() == true)
            {
                var func = "function(xpath) {" + "  return document.evaluate(xpath, document, null," + "      XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue;" + "}";
                foreach (var frame in Session.Frames)
                {
                    var args = $"\"//*[@cd_frame_id_ = '{frame.CromeFrameId}']\"";
                    var res = await WebView.CallFunction(func, args, Session?.GetCurrentFrameId(), cancellationToken: cancellationToken).ConfigureAwait(false);
                    //todo
                    var frameElementId = res.Result?.Value?.ToString();
                    var border = await GetElementBorder(frameElementId, cancellationToken).ConfigureAwait(false);
                    var regionOffset2 = region.Offset(border.X, border.Y);
                    var location = await ScrollElementRegionIntoViewHelper(frameElementId, new WebRect(regionOffset, region.Size()), center, frameElementId, cancellationToken).ConfigureAwait(false);
                    return location;
                }
            }

            return regionOffset;
        }

        public async Task<WebPoint> GetElementBorder(string elementId, CancellationToken cancellationToken = new CancellationToken())
        {
            var borderLeftStr = await GetElementEffectiveStyle(elementId, "border-left-width", cancellationToken).ConfigureAwait(false);
            var borderTopStr = await GetElementEffectiveStyle(elementId, "border-top-width", cancellationToken).ConfigureAwait(false);
            if (int.TryParse(borderLeftStr, out int x) && int.TryParse(borderTopStr, out int y))
            {
                return new WebPoint(x, y);
            }

            return null;
        }

        public async Task<string> GetElementEffectiveStyle(string elementId, string property, CancellationToken cancellationToken = new CancellationToken())
        {
            return (await WebView.CallFunction(atoms.GET_EFFECTIVE_STYLE, $"{{\"{Session.GetElementKey()}\":\"{elementId}\"}}, \"{property}\"", Session?.GetCurrentFrameId(), true, false, cancellationToken).ConfigureAwait(false)).AsString();
        }

        public async Task<bool> IsElementAttributeEqualToIgnoreCase(string elementId, string attributeName, string attributeValue, CancellationToken cancellationToken = new CancellationToken())
        {
            var attr = await GetElementAttribute(elementId, attributeName, cancellationToken).ConfigureAwait(false);
            return string.Equals(attr, attributeValue, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}