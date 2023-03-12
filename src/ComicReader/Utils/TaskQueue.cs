using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Xaml.Controls;

namespace ComicReader.Utils
{
    public enum TaskException
    {
        Cancellation,
        EmptySet,
        Failure,
        FileCorrupted,
        FileNotFound,
        IncorrectPassword,
        InvalidParameters,
        ItemExists,
        MaximumExceeded,
        NameCollision,
        NoPermission,
        NotImplemented,
        NotSupported,
        StopIteration,
        Success,
        Unknown,
        UnknownEnum,
    }

    public class TaskQueue
    {
        public static readonly TaskQueue DefaultQueue = new TaskQueue();

        private int _pendingTaskCount = 0;
        private Task<TaskException> _queue = Task.Factory.StartNew(() => TaskException.Success);

        public int PendingTaskCount => _pendingTaskCount;

        public void Enqueue(Func<Task<TaskException>, TaskException> ope)
        {
            Interlocked.Increment(ref _pendingTaskCount);
            lock (_queue)
            {
                _queue = _queue.ContinueWith(delegate(Task<TaskException> t)
                {
                    Interlocked.Decrement(ref _pendingTaskCount);
                    return t.Result;
                }).ContinueWith(ope);
            }
        }
    }
}
