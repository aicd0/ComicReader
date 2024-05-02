using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.Threading.Tasks;

namespace ComicReader.Utils;

internal class Threading
{
    public static async Task RunInMainThread(Action callback)
    {
        await App.Window.DispatcherQueue.EnqueueAsync(callback, DispatcherQueuePriority.Normal);
    }

    public static async Task<T> RunInMainThread<T>(Func<T> callback)
    {
        return await App.Window.DispatcherQueue.EnqueueAsync(callback, DispatcherQueuePriority.Normal);
    }

    public static async Task RunInMainThreadAsync(Func<Task> callback)
    {
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
        var completionSrc = new TaskCompletionSource<T>();
        await App.Window.DispatcherQueue.EnqueueAsync(async delegate
        {
            completionSrc.SetResult(await callback());
        }, DispatcherQueuePriority.Normal);
        return await completionSrc.Task;
    }
}
