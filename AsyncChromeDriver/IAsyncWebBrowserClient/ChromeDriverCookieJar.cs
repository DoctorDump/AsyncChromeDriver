﻿// Copyright (c) Oleg Zudov. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Zu.WebBrowser.AsyncInteractions;
using Zu.WebBrowser.BasicTypes;
using Zu.WebBrowser.BrowserOptions;

namespace Zu.Chrome
{
    internal class ChromeDriverCookieJar: ICookieJar
    {
        private IAsyncChromeDriver asyncChromeDriver;

        public ChromeDriverCookieJar(IAsyncChromeDriver asyncChromeDriver)
        {
            this.asyncChromeDriver = asyncChromeDriver;
        }

        public Task<ReadOnlyCollection<Cookie>> AllCookies => throw new System.NotImplementedException();

        public Task AddCookie(Cookie cookie, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new System.NotImplementedException();
        }

        public Task DeleteAllCookies(CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new System.NotImplementedException();
        }

        public Task DeleteCookie(Cookie cookie, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new System.NotImplementedException();
        }

        public Task DeleteCookieNamed(string name, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new System.NotImplementedException();
        }

        public Task<Cookie> GetCookieNamed(string name, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new System.NotImplementedException();
        }
    }
}