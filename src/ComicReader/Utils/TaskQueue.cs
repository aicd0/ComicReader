using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ComicReader.Utils;

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
    private const int WATCH_DOG_DELAY = 5000;
    private const long WATCH_DOG_LOG_THRESHOLD = 5000;
    private const long WATCH_DOG_ALERT_THRESHOLD = 15000;

    public static readonly TaskQueue DefaultQueue = new TaskQueue("Default");
    private static int sWatchDogStarted = 0;
    private static ConcurrentDictionary<string, long> sStartedTasks = new();

    private string _name;
    private int _pendingTaskCount = 0;
    private Task<TaskException> _queue = Task.Factory.StartNew(() => TaskException.Success);

    public TaskQueue(string name)
    {
        _name = name;
    }

    public void Enqueue(string name, Func<TaskException> ope)
    {
        StartWatchDog();
        lock (_queue)
        {
            int pendingCount = Interlocked.Increment(ref _pendingTaskCount);
            string taskName = $"{_name}_{name}_{Random.Shared.NextInt64()}";
            Logger.I(TAG + "_" + _name, $"enqueue: {taskName} (pending={pendingCount})");
            _queue = _queue.ContinueWith(delegate (Task<TaskException> t)
            {
                long startTime = GetCurrentMilliseconds();
                sStartedTasks[taskName] = startTime;

                int pendingCount = Interlocked.Decrement(ref _pendingTaskCount);
                Logger.I(TAG + "_" + _name, $"start: {taskName} (pending={pendingCount})");
                TaskException result;
                try
                {
                    result = ope();
                }
                catch (Exception e)
                {
                    Logger.F(TAG, $"exception occured in task {taskName}", e);
                    result = TaskException.Unknown;
                }

                long timeUsed = GetCurrentMilliseconds() - startTime;
                Logger.I(TAG + "_" + _name, $"end: {taskName} (result={result}, timeUsed={timeUsed})");
                sStartedTasks.TryRemove(taskName, out long _);
                return result;
            });
        }
    }

    private static void StartWatchDog()
    {
        if (Interlocked.CompareExchange(ref sWatchDogStarted, 1, 0) != 0)
        {
            return;
        }
        Logger.I(TAG, "starting watch dog");
        C0.Run(async delegate
        {
            while (true)
            {
                await Task.Delay(WATCH_DOG_DELAY);
                CheckHangingTasks();
            }
        });
    }

    private static void CheckHangingTasks()
    {
        long now = GetCurrentMilliseconds();
        foreach (KeyValuePair<string, long> entry in sStartedTasks)
        {
            long timeUsed = now - entry.Value;
            if (timeUsed > WATCH_DOG_ALERT_THRESHOLD)
            {
                Logger.F(TAG, $"CheckHangingTasks(task={entry.Key},timeUsed={timeUsed})");
            }
            else if (timeUsed > WATCH_DOG_LOG_THRESHOLD)
            {
                Logger.I(TAG, $"CheckHangingTasks(task={entry.Key},timeUsed={timeUsed})");
            }
        }
    }

    private static long GetCurrentMilliseconds()
    {
        return DateTimeOffset.Now.ToUnixTimeMilliseconds();
    }
}
