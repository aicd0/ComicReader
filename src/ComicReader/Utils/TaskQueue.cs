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
        private const string TAG = "TaskQueue";
        public static readonly TaskQueue DefaultQueue = new TaskQueue("Default");

        private string _name;
        private int _pendingTaskCount = 0;
        private Task<TaskException> _queue = Task.Factory.StartNew(() => TaskException.Success);

        public TaskQueue(string name)
        {
            _name = name;
        }

        public void Enqueue(string name, Func<TaskException> ope)
        {
            Interlocked.Increment(ref _pendingTaskCount);
            lock (_queue)
            {
                _queue = _queue.ContinueWith(delegate (Task<TaskException> t)
                {
                    Interlocked.Decrement(ref _pendingTaskCount);
                    Log($"start: {name} (left={_pendingTaskCount})");
                    TaskException result = ope();
                    Log($"end: {name} (left={_pendingTaskCount})");
                    return result;
                });
            }
        }

        private void Log(string message)
        {
            Logger.I(TAG + "_" + _name, message);
        }
    }
}
