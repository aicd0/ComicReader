// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

using Microsoft.UI.Dispatching;

namespace ComicReader.Common.Threading;

internal class MainThreadUtils
{
    public static Task RunInMainThread(Action action, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        DispatcherQueue? dispatcher = GetMainThreadDispatcher();
        if (dispatcher == null)
        {
            return Task.CompletedTask;
        }

        if (dispatcher.HasThreadAccess)
        {
            try
            {
                action();
                return Task.CompletedTask;
            }
            catch (Exception e)
            {
                return Task.FromException(e);
            }
        }

        var taskCompletionSource = new TaskCompletionSource<object?>();
        if (!dispatcher.TryEnqueue(priority, delegate
        {
            try
            {
                action();
                taskCompletionSource.SetResult(null);
            }
            catch (Exception e)
            {
                taskCompletionSource.SetException(e);
            }
        }))
        {
            taskCompletionSource.SetException(new InvalidOperationException("Failed to enqueue the operation"));
        }
        return taskCompletionSource.Task;
    }

    public static Task RunInMainThreadAsync(Func<Task> action, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        DispatcherQueue? dispatcher = GetMainThreadDispatcher();
        if (dispatcher == null)
        {
            return Task.CompletedTask;
        }

        if (dispatcher.HasThreadAccess)
        {
            try
            {
                return action();
            }
            catch (Exception e)
            {
                return Task.FromException(e);
            }
        }

        var taskCompletionSource = new TaskCompletionSource<object?>();
        if (!dispatcher.TryEnqueue(priority, async delegate
        {
            try
            {
                await action();
                taskCompletionSource.SetResult(null);
            }
            catch (Exception e)
            {
                taskCompletionSource.SetException(e);
            }
        }))
        {
            taskCompletionSource.SetException(new InvalidOperationException("Failed to enqueue the operation"));
        }
        return taskCompletionSource.Task;
    }

    public static Task PostInMainThreadAsync(Func<Task> action, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        DispatcherQueue? dispatcher = GetMainThreadDispatcher();
        if (dispatcher == null)
        {
            return Task.CompletedTask;
        }

        var taskCompletionSource = new TaskCompletionSource<object?>();
        if (!dispatcher.TryEnqueue(priority, async delegate
        {
            try
            {
                await action();
                taskCompletionSource.SetResult(null);
            }
            catch (Exception e)
            {
                taskCompletionSource.SetException(e);
            }
        }))
        {
            taskCompletionSource.SetException(new InvalidOperationException("Failed to enqueue the operation"));
        }
        return taskCompletionSource.Task;
    }

    public static bool IsMainThread()
    {
        DispatcherQueue? queue = GetMainThreadDispatcher();
        if (queue == null)
        {
            return false;
        }
        return queue.HasThreadAccess;
    }

    private static DispatcherQueue? GetMainThreadDispatcher()
    {
        MainWindow? window = App.WindowManager.GetAnyWindow();
        if (window == null)
        {
            return null;
        }
        return window.DispatcherQueue;
    }
}
