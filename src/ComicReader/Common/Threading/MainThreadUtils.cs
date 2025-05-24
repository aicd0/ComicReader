// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

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

        DispatcherQueue queue = GetDispatcherQueue();
        if (queue == null)
        {
            return;
        }

        await queue.EnqueueAsync(MeasureMainThreadExecutionTimeAction(action), DispatcherQueuePriority.Normal);
    }

    public static async Task RunInMainThreadAsync(Func<Task> action, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        if (IsMainThread())
        {
            await action();
            return;
        }

        DispatcherQueue queue = GetDispatcherQueue();
        if (queue == null)
        {
            return;
        }

        var completionSrc = new TaskCompletionSource<bool>();

        await queue.EnqueueAsync(async delegate
        {
            await action();
            completionSrc.SetResult(true);
        }, priority);

        await completionSrc.Task;
    }

    public static bool IsMainThread()
    {
        DispatcherQueue queue = GetDispatcherQueue();
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
            long startTime = GetCurrentTick();
            action();
            long timeUsed = GetCurrentTick() - startTime;
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

    private static long GetCurrentTick()
    {
        return Environment.TickCount64;
    }

    private static DispatcherQueue GetDispatcherQueue()
    {
        MainWindow window = App.WindowManager.GetAnyWindow();
        if (window == null)
        {
            return null;
        }
        return window.DispatcherQueue;
    }
}
