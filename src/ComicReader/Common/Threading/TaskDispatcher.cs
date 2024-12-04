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
    private int _pendingTaskCount = 0;
    private int _runningTaskCount = 0;

    protected TaskDispatcher(string name)
    {
        _name = name;
    }

    public void Submit(string taskName, Action action)
    {
        ArgumentNullException.ThrowIfNull(taskName, nameof(taskName));
        ArgumentNullException.ThrowIfNull(action, nameof(action));

        var tag = LogTag.N(TAG, _name, taskName);
        long submitTime = GetCurrentMilliseconds();
        int pendingCount = Interlocked.Increment(ref _pendingTaskCount);
        int runningCount = _runningTaskCount;
        Logger.I(tag, $"submitted (running={runningCount},pending={pendingCount})");
        SubmitInternal(delegate
        {
            long startTime = GetCurrentMilliseconds();
            long timeUsed = GetCurrentMilliseconds() - submitTime;
            pendingCount = Interlocked.Decrement(ref _pendingTaskCount);
            runningCount = Interlocked.Increment(ref _runningTaskCount);
            Logger.I(tag, $"started (time={timeUsed},running={runningCount},pending={pendingCount})");
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
                        Logger.F(tag, $"exception occured in task {taskName}", e);
                    }
                }
            }
            finally
            {
                pendingCount = _pendingTaskCount;
                runningCount = Interlocked.Decrement(ref _runningTaskCount);
                timeUsed = GetCurrentMilliseconds() - startTime;
                Logger.I(tag, $"stopped (time={timeUsed},running={runningCount},pending={pendingCount})");
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
