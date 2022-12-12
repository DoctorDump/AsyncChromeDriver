// Copyright (c) Oleg Zudov. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json.Linq;
using Zu.ChromeDevTools.Runtime;
using Zu.WebBrowser.BasicTypes;

namespace Zu.Chrome.DriverCore
{
    internal static class ResultValueConverter
    {
        internal static RemoteObject GetResultOrThrow(EvaluateCommandResponse res)
        {
            if (res == null)
                throw new ApplicationException("EvaluateCommandResponse is null");
            if (res.ExceptionDetails != null)
            {
                throw new ApplicationException(
                    $"EvaluateCommand failed\n" +
                    $"{res.ExceptionDetails.Text}\n" +
                    $"Line {res.ExceptionDetails.LineNumber}:{res.ExceptionDetails.ColumnNumber}\n" +
                    $"Stack:\n" +
                    $"{res.ExceptionDetails.StackTrace}");
            }
            if (res.Result == null)
                throw new ApplicationException("EvaluateCommandResponse.Result is null");
            return res.Result;
        }

        internal static WebPoint ToWebPoint(object value)
        {
            var res = (value as JObject)?["value"];
            if (res != null) {
                var x = res["x"]?.Value<int>();
                var y = res["y"]?.Value<int>();
                if (x != null && y != null) return new WebPoint((int)x, (int)y);
            }
            return null;
        }

        internal static bool ToBool(this EvaluateCommandResponse res)
        {
            var value = GetResultOrThrow(res).Value;
            ThrowWhenBadStatus(value as JToken);
            return (value as JObject)?["value"]?.Value<bool>() == true;
        }

        internal static WebSize ToWebSize(this EvaluateCommandResponse res2)
        {
            var value = GetResultOrThrow(res2).Value;
            ThrowWhenBadStatus(value as JToken);
            var res = (value as JObject)?["value"];
            if (res != null) {
                var width = res["width"]?.Value<int>();
                var height = res["height"]?.Value<int>();
                if (width != null && height != null) return new WebSize((int)width, (int)height);
            }
            return null;
        }

        internal static WebRect ToWebRect(this EvaluateCommandResponse res2)
        {
            var value = GetResultOrThrow(res2).Value;
            ThrowWhenBadStatus(value as JToken);
            var res = (value as JObject)?["value"];
            if (res != null) {
                var x = res["x"]?.Value<int>() ?? res["left"]?.Value<int>();
                var y = res["y"]?.Value<int>() ?? res["top"]?.Value<int>();
                var width = res["width"]?.Value<int>();
                var height = res["height"]?.Value<int>();
                if (x != null && y != null && width != null && height != null) return new WebRect((int)x, (int)y, (int)width, (int)height);
            }
            return null;
        }

        internal static bool ValueIsNull(JToken res)
        {
            if (res == null) return true;
            if (res?["value"] is JValue && (res?["value"] as JValue)?.Value == null) return true;
            return false;
        }

        internal static string AsString(this EvaluateCommandResponse res)
        {
            var value = GetResultOrThrow(res).Value;
            ThrowWhenBadStatus(value as JToken);
            return ((string)(value as JObject)?["value"])?.Replace("\n", "\r\n").Replace("\r\r", "\r");
        }

        internal static JToken AsJToken(this EvaluateCommandResponse res)
        {
            var value = GetResultOrThrow(res).Value as JToken;
            ThrowWhenBadStatus(value);
            return value;
        }

        internal static string ToElementId(object value, string elementKey = "ELEMENT")
        {
            return (value as JObject)?["value"]?[elementKey]?.ToString();
        }

        internal static void ThrowWhenBadStatus(JToken json)
        {
            if (json is JArray) 
                return;
            
            var status = (json as JObject)?["status"]?.ToString();
            if (status == "0") 
                return;

            var value = (json as JObject)?["value"]?.ToString();

            if (value == null)
            {
                throw new WebBrowserException($"Status {status}, json {json}")
                {
                    Json = json
                };
            }

            var res = new WebBrowserException(value)
            {
                Json = json
            };
            if (status == "10" && value == "element is not attached to the page document")
            {
                res.Error = "stale element reference";
            }
            else if (status == "13" && value.EndsWith("is not defined"))
            {
                res.Error = "invalid operation";
            }
            else if (status == "32")
            {
                res.Error = "invalid selector";
            }
            else if (status == "17")
            {
                throw new InvalidOperationException(value);
            }
            else
            {
                throw new NotImplementedException($"{nameof(ThrowWhenBadStatus)}: {status}/{value}, json {json}");
            }

            throw res;
        }
    }
}