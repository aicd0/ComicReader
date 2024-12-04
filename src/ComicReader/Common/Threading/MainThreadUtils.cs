// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Threading.Tasks;

using ComicReader.Common.DebugTools;

using CommunityToolkit.WinUI;

using Microsoft.UI.Dispatching;

namespace ComicReader.Common.Threading;

internal class MainThreadUtils
{
    public static void MeasureMainThreadExecutionTime(Action action)
    {
        if (!IsMainThread())
        {
            throw new InvalidOperationException("Not in main thread");
        }

        MeasureMainThreadExecutionTimeAction(action)();
    }

    public static async Task RunInMainThread(Action action)
    {
        if (IsMainThread())
        {
            MeasureMainThreadExecutionTimeAction(action)();
        }
        else
        {
            await App.Window.DispatcherQueue.EnqueueAsync(MeasureMainThreadExecutionTimeAction(action), DispatcherQueuePriority.Normal);
        }
    }

    public static async Task<T> RunInMainThread<T>(Func<T> action)
    {
        if (IsMainThread())
        {
            return MeasureMainThreadExecutionTimeAction(action)();
        }

        return await App.Window.DispatcherQueue.EnqueueAsync(MeasureMainThreadExecutionTimeAction(action), DispatcherQueuePriority.Normal);
    }

    public static async Task RunInMainThreadAsync(Func<Task> action)
    {
        if (IsMainThread())
        {
            await action();
            return;
        }

        var completionSrc = new TaskCompletionSource<bool>();
        await App.Window.DispatcherQueue.EnqueueAsync(async delegate
        {
            await action();
            completionSrc.SetResult(true);
        }, DispatcherQueuePriority.Normal);
        await completionSrc.Task;
    }

    public static async Task<T> RunInMainThreadAsync<T>(Func<Task<T>> action)
    {
        if (IsMainThread())
        {
            return await action();
        }

        var completionSrc = new TaskCompletionSource<T>();
        await App.Window.DispatcherQueue.EnqueueAsync(async delegate
        {
            completionSrc.SetResult(await action());
        }, DispatcherQueuePriority.Normal);
        return await completionSrc.Task;
    }

    public static bool IsMainThread()
    {
        return App.Window.DispatcherQueue.HasThreadAccess;
    }

    private static Action MeasureMainThreadExecutionTimeAction(Action action)
    {
        if (!DebugUtils.DebugModeStrict)
        {
            return action;
        }

        StackTrace stackTrace = new();
        return delegate
        {
            long startTime = GetCurrentMilliseconds();
            action();
            long timeUsed = GetCurrentMilliseconds() - startTime;
            OnMainThreadExecutionTime(timeUsed, stackTrace);
        };
    }

    private static Func<T> MeasureMainThreadExecutionTimeAction<T>(Func<T> action)
    {
        if (!DebugUtils.DebugModeStrict)
        {
            return action;
        }

        StackTrace stackTrace = new();
        return delegate
        {
            long startTime = GetCurrentMilliseconds();
            T result = action();
            long timeUsed = GetCurrentMilliseconds() - startTime;
            OnMainThreadExecutionTime(timeUsed, stackTrace);
            return result;
        };
    }

    private static void OnMainThreadExecutionTime(long timeUsed, StackTrace stackTrace)
    {
        if (timeUsed > 300)
        {
            Logger.I("MainThreadUtils", $"timeUsed={timeUsed}\n{stackTrace}");
            DebugUtils.Assert(false);
        }
    }

    private static long GetCurrentMilliseconds()
    {
        return DateTimeOffset.Now.ToUnixTimeMilliseconds();
    }
}
