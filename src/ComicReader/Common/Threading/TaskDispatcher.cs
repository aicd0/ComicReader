// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

using ComicReader.Common.DebugTools;

namespace ComicReader.Common.Threading;

internal abstract class TaskDispatcher : ITaskDispatcher
{
    private const string TAG = "TaskDispatcher";

    public static readonly ITaskDispatcher DefaultQueue = Factory.NewQueue("DefaultQueue");
    public static readonly ITaskDispatcher LongRunningThreadPool = new ThreadPoolDispatcher("LongRunningThreadPool", TaskCreationOptions.LongRunning);

    private readonly string _name;
    private readonly LogTag _submitTag;
    private readonly LogTag _startTag;
    private readonly LogTag _endTag;
    private int _pendingTaskCount = 0;
    private int _runningTaskCount = 0;

    protected TaskDispatcher(string name)
    {
        _name = name;
        _submitTag = LogTag.N(TAG, "submit", _name);
        _startTag = LogTag.N(TAG, "start", _name);
        _endTag = LogTag.N(TAG, "end", _name);
    }

    public void Submit(string taskName, Action action)
    {
        ArgumentNullException.ThrowIfNull(taskName, nameof(taskName));
        ArgumentNullException.ThrowIfNull(action, nameof(action));

        long submitTime = GetCurrentMilliseconds();
        {
            int pendingCount = Interlocked.Increment(ref _pendingTaskCount);
            int runningCount = _runningTaskCount;
            Logger.I(_submitTag, $"task={taskName},running={runningCount},pending={pendingCount}");
        }

        SubmitInternal(delegate
        {
            long startTime = GetCurrentMilliseconds();
            {
                long since0 = GetCurrentMilliseconds() - submitTime;
                int pendingCount = Interlocked.Decrement(ref _pendingTaskCount);
                int runningCount = Interlocked.Increment(ref _runningTaskCount);
                Logger.I(_startTag, $"task={taskName},since0={since0},running={runningCount},pending={pendingCount}");
            }

            try
            {
                if (DebugUtils.DebugModeStrict)
                {
                    action();
                }
                else
                {
                    try
                    {
                        action();
                    }
                    catch (Exception e)
                    {
                        Logger.F(_endTag, $"exception occured in task {taskName}", e);
                    }
                }
            }
            finally
            {
                int pendingCount = _pendingTaskCount;
                int runningCount = Interlocked.Decrement(ref _runningTaskCount);
                long time = GetCurrentMilliseconds();
                long since0 = time - submitTime;
                long since1 = time - startTime;
                Logger.I(_endTag, $"task={taskName},since0={since0},since1={since1},running={runningCount},pending={pendingCount}");
            }
        });
    }

    protected abstract void SubmitInternal(Action action);

    private static long GetCurrentMilliseconds()
    {
        return DateTimeOffset.Now.ToUnixTimeMilliseconds();
    }

    private class QueueDispatcher : TaskDispatcher
    {
        private Task _queue = Task.Factory.StartNew(() => TaskException.Success);

        public QueueDispatcher(string name) : base(name)
        {
        }

        protected override void SubmitInternal(Action action)
        {
            lock (_queue)
            {
                _queue = _queue.ContinueWith(delegate
                {
                    action();
                }, TaskCreationOptions.PreferFairness, TaskScheduler.Default);
            }
        }
    }

    private class ThreadPoolDispatcher : TaskDispatcher, IDisposable
    {
        private readonly CancellationTokenSource _source = new();
        private readonly TaskCreationOptions _creationOptions;

        public ThreadPoolDispatcher(string name, TaskCreationOptions creationOptions) : base(name)
        {
            _creationOptions = creationOptions;
        }

        protected override void SubmitInternal(Action action)
        {
            Task.Factory.StartNew(action, _source.Token, _creationOptions, TaskScheduler.Default);
        }

        public void Dispose()
        {
            _source.Dispose();
        }
    }

    public static class Factory
    {
        public static ITaskDispatcher NewQueue(string name)
        {
            return new QueueDispatcher(name);
        }

        public static ITaskDispatcher NewThreadPool(string name)
        {
            return new ThreadPoolDispatcher(name, TaskCreationOptions.PreferFairness);
        }
    }
}
