﻿using System;
using System.Threading.Tasks;
using Windows.UI.Core;
using BreadPlayer.Core.Interfaces;

namespace BreadPlayer.Dispatcher
{
    public class BreadDispatcher : IDispatcher
    {
        private CoreDispatcher _dispatcher;
        public BreadDispatcher(CoreDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }
        public async Task RunAsync(Action action)
        {
            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => action());
        }
        public bool HasThreadAccess => _dispatcher.HasThreadAccess;
    }
}
