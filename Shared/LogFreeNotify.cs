using System;
using System.Threading;

namespace OpenTap.Shared
{
    /// <summary>
    /// Synchronization device that makes it possible to push notifications from many threads to one handler thread. Pushing notifications is as fast as possible and must not lock the thread or in general use slow synchronization calls.
    /// </summary>
    class LockFreeFuzzyNotify : IDisposable
    {
        ManualResetEvent evt = new ManualResetEvent(false);
        long currentMarker;
        long lastMarker;
        
        int padding = 1;
        public void PushNotification()
        {
            var readMarker = Interlocked.Add(ref currentMarker, 1) - padding;
            if (Interlocked.Read(ref lastMarker) > readMarker)
            {
                lock (evt)
                    evt.Set();
            }
        }
        void onmiss()
        {
            padding++;
        }
        void processNotifications()
        {
            while (true)
            {
                bool missed = false;
                if (!evt.WaitOne(300))
                    missed = true;

                bool update;
                lock (evt)
                {
                    var prev = lastMarker;
                    Interlocked.Exchange(ref lastMarker, Interlocked.Read(ref currentMarker));
                    update = prev != lastMarker;
                    evt.Reset();
                }
                if (update)
                {
                    if (missed)
                    {
                        onmiss();
                        OnMiss?.Invoke();
                    }
                    OnNotifyAsync?.Invoke();
                }

            }
        }

        public void Dispose()
        {
            workerThread.Abort();
        }

        /// <summary> Invoked when a notification has been detected. This event is fired asynchronously.</summary>
        public Action OnNotifyAsync = () => { };
        /// <summary> A 'miss' is the situation when, due to race conditions, a notification was pushed without activating the event. Idealy this should never happen, but the lock-free nature of this class makes it hard to avoid.</summary>
        public Action OnMiss = () => { };
        TapThread workerThread;

        /// <summary> </summary>
        /// <param name="onNotifyAsync"> Notification callback</param>
        public LockFreeFuzzyNotify(Action onNotifyAsync)
        {
            workerThread = TapThread.Start(processNotifications);
            OnNotifyAsync = onNotifyAsync;
        }

        internal void Flush()
        {
            evt.Set();
        }
    }
}