// Copyright (C) 2015 Tom Deseyn
// Licensed under GNU LGPL, Version 2.1. See LICENSE in the project root for license information.

using System;
using System.Threading;

namespace Tmds.SockJS
{
    delegate void CancellableTimerCallback(object state, CancellableTimer timer);
    class CancellableTimer
    {
        private readonly CancellableTimerCallback _callback;
        private Timer _timer;

        public static CancellableTimer Schedule(CancellableTimerCallback callback, object state, TimeSpan dueTime)
        {
            return new CancellableTimer(callback, state, (int)dueTime.TotalMilliseconds);
        }

        public void Cancel()
        {
            IsCancelled = true;
            _timer?.Dispose();
            _timer = null;
        }

        public bool IsCancelled { get; private set; }

        private CancellableTimer(CancellableTimerCallback callback, object state, int dueTime)
        {
            _callback = callback;
            _timer = new Timer(OnTimeout, state, dueTime, -1);
        }

        private void OnTimeout(object state)
        {
            if (IsCancelled)
            {
                return;
            }
            _callback(state, this);
            _timer?.Dispose();
            _timer = null;
        }
    }
}
