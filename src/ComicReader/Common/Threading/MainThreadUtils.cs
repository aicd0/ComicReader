// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

using CommunityToolkit.WinUI;

using Microsoft.UI.Dispatching;

namespace ComicReader.Common.Threading;

internal class MainThreadUtils
{
    public static async Task RunInMainThread(Action callback)
    {
        if (IsOnMainThread())
        {
            callback();
        }
        else
        {
            await App.Window.DispatcherQueue.EnqueueAsync(callback, DispatcherQueuePriority.Normal);
        }
    }

    public static async Task<T> RunInMainThread<T>(Func<T> callback)
    {
        if (IsOnMainThread())
        {
            return callback();
        }

        return await App.Window.DispatcherQueue.EnqueueAsync(callback, DispatcherQueuePriority.Normal);
    }

    public static async Task RunInMainThreadAsync(Func<Task> callback)
    {
        if (IsOnMainThread())
        {
            await callback();
            return;
        }

        var completionSrc = new TaskCompletionSource<bool>();
        await App.Window.DispatcherQueue.EnqueueAsync(async delegate
        {
            await callback();
            completionSrc.SetResult(true);
        }, DispatcherQueuePriority.Normal);
        await completionSrc.Task;
    }

    public static async Task<T> RunInMainThreadAsync<T>(Func<Task<T>> callback)
    {
        if (IsOnMainThread())
        {
            return await callback();
        }

        var completionSrc = new TaskCompletionSource<T>();
        await App.Window.DispatcherQueue.EnqueueAsync(async delegate
        {
            completionSrc.SetResult(await callback());
        }, DispatcherQueuePriority.Normal);
        return await completionSrc.Task;
    }

    private static bool IsOnMainThread()
    {
        return App.Window.DispatcherQueue.HasThreadAccess;
    }
}
