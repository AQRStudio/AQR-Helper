using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace AQRLiteHelper.Bridge
{
    internal sealed class BridgeRequestQueue
    {
        private readonly ConcurrentQueue<QueuedWork> _queue = new ConcurrentQueue<QueuedWork>();

        public Task<T> Enqueue<T>(Func<T> work)
        {
            var queued = new QueuedWork();
            var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            queued.Execute = () =>
            {
                try
                {
                    completion.TrySetResult(work());
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            };

            _queue.Enqueue(queued);
            return completion.Task;
        }

        public void ProcessPending(int maxItems = 8)
        {
            var processed = 0;
            while (processed < maxItems && _queue.TryDequeue(out var item))
            {
                item.Execute();
                processed++;
            }
        }

        private sealed class QueuedWork
        {
            public Action Execute { get; set; } = () => { };
        }
    }
}
