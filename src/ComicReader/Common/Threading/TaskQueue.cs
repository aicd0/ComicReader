// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ComicReader.Common.DebugTools;

namespace ComicReader.Common.Threading;

public class TaskQueue
{
    private const string TAG = "TaskQueue";
    private const int WATCH_DOG_DELAY = 5000;
    private const long WATCH_DOG_ALERT_THRESHOLD = 10000;

    public static readonly TaskQueue DefaultQueue = new("Default");
    public static readonly TaskQueue LongRunningQueue = new("LongRunning");

    private static int sWatchDogStarted = 0;
    private static readonly ConcurrentDictionary<string, long> sStartedTasks = new();

    private readonly string _name;
    private int _pendingTaskCount = 0;
    private Task<TaskException> _queue = Task.Factory.StartNew(() => TaskException.Success);

    public TaskQueue(string name)
    {
        _name = name;
    }

    public void Enqueue(string name, Func<TaskException> ope)
    {
        StartWatchDog();
        var tag = LogTag.N(TAG, _name, name);
        lock (_queue)
        {
            int pendingCount = Interlocked.Increment(ref _pendingTaskCount);
            string taskName = $"{_name}_{name}_{Random.Shared.NextInt64()}";
            Logger.I(tag, $"enqueue: pending={pendingCount}");
            _queue = _queue.ContinueWith(delegate (Task<TaskException> t)
            {
                long startTime = GetCurrentMilliseconds();
                sStartedTasks[taskName] = startTime;

                int pendingCount = Interlocked.Decrement(ref _pendingTaskCount);
                Logger.I(tag, $"start: pending={pendingCount}");
                TaskException result;

                if (DebugUtils.DebugMode)
                {
                    result = ope();
                }
                else
                {
                    try
                    {
                        result = ope();
                    }
                    catch (Exception e)
                    {
                        Logger.F(tag, $"exception occured in task {taskName}", e);
                        result = TaskException.Unknown;
                    }
                }

                long timeUsed = GetCurrentMilliseconds() - startTime;
                Logger.I(tag, $"end: result={result}, timeUsed={timeUsed}");
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
                Logger.I(TAG, $"CheckHangingTasks(task={entry.Key},timeUsed={timeUsed})");
            }
        }
    }

    private static long GetCurrentMilliseconds()
    {
        return DateTimeOffset.Now.ToUnixTimeMilliseconds();
    }
}
