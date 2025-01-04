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
    public static async Task RunInMainThread(Action action)
    {
        if (IsMainThread())
        {
            MeasureMainThreadExecutionTimeAction(action)();
            return;
        }

        DispatcherQueue queue = App.Window.DispatcherQueue;

        if (queue == null)
        {
            return;
        }

        await queue.EnqueueAsync(MeasureMainThreadExecutionTimeAction(action), DispatcherQueuePriority.Normal);
    }

    public static async Task RunInMainThreadAsync(Func<Task> action)
    {
        if (IsMainThread())
        {
            await action();
            return;
        }

        DispatcherQueue queue = App.Window.DispatcherQueue;

        if (queue == null)
        {
            return;
        }

        var completionSrc = new TaskCompletionSource<bool>();

        await queue.EnqueueAsync(async delegate
        {
            await action();
            completionSrc.SetResult(true);
        }, DispatcherQueuePriority.Normal);

        await completionSrc.Task;
    }

    public static bool IsMainThread()
    {
        DispatcherQueue queue = App.Window.DispatcherQueue;

        if (queue == null)
        {
            return false;
        }

        return queue.HasThreadAccess;
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

    private static void OnMainThreadExecutionTime(long timeUsed, StackTrace stackTrace)
    {
        if (timeUsed > 300)
        {
            Logger.I("MainThreadUtils", $"timeUsed={timeUsed}\n{stackTrace}");
        }
    }

    private static long GetCurrentMilliseconds()
    {
        return DateTimeOffset.Now.ToUnixTimeMilliseconds();
    }
}
