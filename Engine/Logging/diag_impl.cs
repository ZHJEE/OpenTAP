//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace OpenTap.Diagnostic
{
    /// <summary>
    /// Default timestamper.
    /// </summary>
    [Display("Local and Accurate", "Delivers timestamping that will be accurate to the system time over longer durations.")]
    public class AccurateStamper : ILogTimestampProvider
    {
        /// <summary> Prints a friendly name. </summary>
        /// <returns></returns>
        public override string ToString() => "Local and Accurate";
        long ILogTimestampProvider.Timestamp()
        {
            return DateTime.UtcNow.Ticks;
        }

        Stopwatch sw = Stopwatch.StartNew();
        long UtcOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).Ticks;
        const long ticksPerSecond = 10000000; // TimeSpan.FromSeconds(1).Ticks;
        long ILogTimestampProvider.ConvertToTicks(long timestamp)
        {
            if (sw.ElapsedTicks > ticksPerSecond)
            {
                UtcOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).Ticks;
                sw.Restart();
            }

            return (timestamp + UtcOffset);
        }
    }

    internal class LogContext : ILogContext
    {
        private int BufferSize;

        internal bool IsAsync;

        internal LogQueue LogQueue;

        internal bool HasListeners;

        internal List<ILogListener> Listeners;

        private long ProcessedMessages;

        internal long OutstandingMessages => LogQueue.PostedMessages - ProcessedMessages;

        public LogContext(bool startProcessor = true)
        {
            LogQueue = new LogQueue();
            Listeners = new List<ILogListener>();

            timestamper = new AccurateStamper();

            if (startProcessor)
            {
                notify = new Shared.LockFreeFuzzyNotify(processLog);
            }
        }

        OpenTap.Shared.LockFreeFuzzyNotify notify;
        private static LogContext EmptyLogContext()
        {
            return new LogContext(false) { HasListeners = false };
        }

        List<ILogListener> copy = new List<ILogListener>();
        Event[] bunch = new Event[0];
        void processLog()
        {
            int count = LogQueue.DequeueBunch(ref bunch);

            if (count > 0)
            {
                lock (Listeners)
                {
                    copy.Clear();
                    copy.AddRange(Listeners);
                }

                if (copy.Count > 0)
                {
                    if (timestamper != null)
                        for (int i = 0; i < bunch.Length; i++)
                            bunch[i].Timestamp = timestamper.ConvertToTicks(bunch[i].Timestamp);


                    foreach (var listener in copy)
                    {
                        try
                        {
                            using (var events = new EventCollection(bunch))
                            {
                                listener.EventsLogged(events);
                            }
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }

                Interlocked.Add(ref ProcessedMessages, count);
            }
            TapThread.Sleep(10);
        }

        private ILogTimestampProvider timestamper;

        public ILog CreateLog(string source)
        {
            return new Log(this, source);
        }

        public void RemoveLog(ILog logSource)
        {
            var log = logSource as Log;
            if (log != null)
            {
                log.Context = EmptyLogContext();
            }
        }

        public void AttachListener(ILogListener listener)
        {
            lock (Listeners)
            {
                Listeners.Add(listener);
                HasListeners = true;
            }
        }

        public void DetachListener(ILogListener listener)
        {
            lock (Listeners)
            {
                Listeners.Remove(listener);
                HasListeners = Listeners.Count > 0;
            }
        }

        public bool Flush(int timeoutMs = 0)
        {
            long posted = LogQueue.PostedMessages;

            notify.Flush();
            
            if (timeoutMs == 0)
            {
                while ((ProcessedMessages - posted) < 0)
                {
                    Thread.Yield();
                    notify.Flush();
                }

                return true;
            }
            else
            {
                var sw = Stopwatch.StartNew();

                while (((ProcessedMessages - posted) < 0) && (sw.ElapsedMilliseconds < timeoutMs))
                {
                    Thread.Yield();
                    notify.Flush();
                }

                return (ProcessedMessages - posted) < 0;
            }
        }

        public bool Flush(TimeSpan timeout) => Flush((int)timeout.TotalMilliseconds);

        public bool Async
        {
            get => IsAsync;
            set
            {
                if (IsAsync != value) IsAsync = value;
            }
        }

        public int MessageBufferSize
        {
            get => BufferSize; 
            set => BufferSize = value;
        }

        public ILogTimestampProvider Timestamper { get => timestamper; set => timestamper = value; }

        internal void InjectEvent(Event @event)
        {
            lock (Listeners)
            {
                using (EventCollection eventCollection = new EventCollection(new Event[] { @event }))
                {
                    Listeners.ForEach(l => l.EventsLogged(eventCollection));
                }
            }
        }

        internal class Log : ILog
        {
            private readonly string _source;

            internal LogContext Context;

            public Log(LogContext context, string source)
            {
                Context = context;
                _source = source;
            }

            public void LogEvent(int eventType, string message)
            {
                if (Context.HasListeners)
                {
                    long timestamp = 0;
                    var timestamper = Context.Timestamper;
                    if (timestamper != null)
                        timestamp = timestamper.Timestamp();

                    if (!Context.IsAsync)
                    {
                        Context.InjectEvent(new Event(0, eventType, message, _source, timestamp));
                    }
                    else
                    {
                        int msgCnt = Context.BufferSize;
                        if (msgCnt > 0)
                        {
                            while (Context.OutstandingMessages > msgCnt)
                                Thread.Sleep(1);
                        }

                        Context.LogQueue.Enqueue(_source, message, timestamp, 0, eventType);
                        Context.notify.PushNotification();
                    }
                }
            }

            public void LogEvent(int eventType, string message, params object[] args)
            {
                if (message == null)
                    throw new ArgumentNullException("message");
                if (args == null)
                    throw new ArgumentNullException("args");
                if (Context.HasListeners)
                {
                    long timestamp = 0;
                    var timestamper = Context.Timestamper;
                    if (timestamper != null)
                        timestamp = timestamper.Timestamp();

                    if (!Context.IsAsync)
                    {
                        Context.InjectEvent(new Event(0, eventType, string.Format(message, args), _source, timestamp));
                    }
                    else
                    {
                        int msgCnt = Context.BufferSize;
                        if (msgCnt > 0)
                        {
                            while (Context.OutstandingMessages > msgCnt)
                                Thread.Sleep(1);
                        }

                        Context.LogQueue.Enqueue(_source, string.Format(message, args), timestamp, 0, eventType);
                        Context.notify.PushNotification();
                    }
                }
            }

            public void LogEvent(int eventType, long durationNs, string message)
            {
                if (Context.HasListeners)
                {
                    long timestamp = 0;
                    var timestamper = Context.Timestamper;
                    if (timestamper != null)
                        timestamp = timestamper.Timestamp();

                    if (!Context.IsAsync)
                    {
                        Context.InjectEvent(new Event(durationNs, eventType, message, _source, timestamp));
                    }
                    else
                    {
                        int msgCnt = Context.BufferSize;
                        if (msgCnt > 0)
                        {
                            while (Context.OutstandingMessages > msgCnt)
                                Thread.Sleep(1);
                        }

                        Context.LogQueue.Enqueue(_source, message, timestamp, durationNs, eventType);
                        Context.notify.PushNotification();
                    }
                }
            }

            public void LogEvent(int eventType, long durationNs, string message, params object[] args)
            {
                if (message == null)
                    throw new ArgumentNullException("message");
                if (args == null)
                    throw new ArgumentNullException("args");
                if (Context.HasListeners)
                {
                    long timestamp = 0;
                    var timestamper = Context.Timestamper;
                    if (timestamper != null)
                        timestamp = timestamper.Timestamp();

                    if (!Context.IsAsync)
                    {
                        Context.InjectEvent(new Event(durationNs, eventType, string.Format(message, args), _source, timestamp));
                    }
                    else
                    {
                        int msgCnt = Context.BufferSize;
                        if (msgCnt > 0)
                        {
                            while (Context.OutstandingMessages > msgCnt)
                                Thread.Sleep(1);
                        }

                        Context.LogQueue.Enqueue(_source, string.Format(message, args), timestamp, durationNs, eventType);
                        Context.notify.PushNotification();
                    }
                }
            }

            string ILog.Source => _source;
        }
    }
}
