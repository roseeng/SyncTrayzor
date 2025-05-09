﻿using NLog;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SyncTrayzor.Syncthing
{
    public interface ISyncthingPoller : IDisposable
    {
        void Start();
        void Stop();
    }

    public abstract class SyncthingPoller : ISyncthingPoller
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly TimeSpan pollingInterval;
        private readonly TimeSpan erroredWaitInterval;

        private readonly object runningLock = new();
        private CancellationTokenSource cancelCts;
        private bool _running;

        public void Start()
        {
            lock (runningLock)
            {
                if (_running)
                    return;

                cancelCts = new CancellationTokenSource();
                _running = true;
                StartInternal(cancelCts.Token);
            }
        }

        public void Stop()
        {
            CancellationTokenSource ctsToCancel;
            lock (runningLock)
            {
                if (!_running)
                    return;

                _running = false;
                ctsToCancel = cancelCts;
                cancelCts = null;
            }

            if (ctsToCancel != null)
                ctsToCancel.Cancel();
        }

        public SyncthingPoller(TimeSpan pollingInterval)
            : this(pollingInterval, TimeSpan.FromMilliseconds(1000))
        { }

        public SyncthingPoller(TimeSpan pollingInterval, TimeSpan erroredWaitInterval)
        {
            this.pollingInterval = pollingInterval;
            this.erroredWaitInterval = erroredWaitInterval;
        }

        protected virtual async void StartInternal(CancellationToken cancellationToken)
        {
            OnStart();

            // We're aborted by the CTS
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    bool errored = await DoWithErrorHandlingAsync(async () =>
                    {
                        await PollAsync(cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();

                        if (pollingInterval.Ticks > 0)
                            await Task.Delay(pollingInterval, cancellationToken);
                    }, cancellationToken);

                    if (errored)
                    {
                        try
                        {
                            await Task.Delay(erroredWaitInterval, cancellationToken);
                        }
                        catch (OperationCanceledException)
                        { }
                    }
                }
            }
            finally
            {
                OnStop();
            }
        }

        protected async Task<bool> DoWithErrorHandlingAsync(Func<Task> action, CancellationToken cancellationToken)
        {
            bool errored = false;

            try
            {
                await action();
            }
            catch (HttpRequestException)
            {
                errored = true;
            }
            catch (IOException)
            {
                // Socket forcibly closed. Could be a restart, could be a termination. We'll have to continue and quit if we're stopped
                errored = true;
            }
            catch (OperationCanceledException e)
            {
                // We can get cancels from tokens other than ours...
                // If it was ours, then the while loop will abort shortly
                if (e.CancellationToken != cancellationToken)
                    errored = true;
            }
            catch (Exception e)
            {
                // Anything else?
                // We can't abort, as then the exception will be lost. So log it, and keep going
                logger.Warn(e, "Unexpected exception while polling");
                errored = true;
            }

            return errored;
        }

        protected virtual void OnStart() { }
        protected virtual void OnStop() { }
        protected abstract Task PollAsync(CancellationToken cancellationToken);

        public void Dispose()
        {
            Stop();
        }
    }
}
