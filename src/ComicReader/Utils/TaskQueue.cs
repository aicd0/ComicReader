using System;
using System.Threading;
using System.Threading.Tasks;

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

        private bool _logRoutineStarted = false;
        private int _lastLogCount = -1;
        private bool _logPendingTask = false;
        public bool LogPendingTask {
            get => _logPendingTask;
            set
            {
                if (_logPendingTask != value)
                {
                    _logPendingTask = value;
                    if (value)
                    {
                        StartLogRoutine();
                    }
                }
            }
        }

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

        private void StartLogRoutine()
        {
            if (_logRoutineStarted)
            {
                return;
            }
            Utils.C0.Run(async delegate
            {
                while (_logPendingTask)
                {
                    if (_lastLogCount != _pendingTaskCount)
                    {
                        _lastLogCount = _pendingTaskCount;
                        System.Diagnostics.Debug.WriteLine("PendingTaskCount: " + _pendingTaskCount.ToString());
                    }
                    await Task.Delay(1000);
                }
                _logRoutineStarted = false;
            });
        }
    }
}
