// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Threading;

using ComicReader.Common.DebugTools;
using ComicReader.Common.Lifecycle;

using Microsoft.UI.Xaml;

namespace ComicReader.Common;

class WindowManager<T> where T : Window
{
    private int _nextWindowId = 0;
    private readonly ConcurrentDictionary<int, WindowWrapper> _windows = [];

    public int RegisterWindow(T window)
    {
        int windowId = Interlocked.Increment(ref _nextWindowId);
        WindowWrapper wrapper = new() { Window = window };
        bool success = _windows.TryAdd(windowId, wrapper);
        DebugUtils.Assert(success);
        return windowId;
    }

    public void UnregisterWindow(int windowId)
    {
        bool success = _windows.TryRemove(windowId, out _);
        DebugUtils.Assert(success);
    }

    public T GetAnyWindow()
    {
        foreach (WindowWrapper wrapper in _windows.Values)
        {
            return wrapper.Window;
        }
        DebugUtils.Assert(false);
        return null;
    }

    public T GetWindow(int windowId)
    {
        if (_windows.TryGetValue(windowId, out WindowWrapper wrapper))
        {
            return wrapper.Window;
        }
        DebugUtils.Assert(false);
        return null;
    }

    public EventBus GetEventBus(int windowId)
    {
        if (_windows.TryGetValue(windowId, out WindowWrapper wrapper))
        {
            return wrapper.EventBus;
        }
        DebugUtils.Assert(false);
        return null;
    }

    private class WindowWrapper
    {
        public T Window { get; set; }
        public EventBus EventBus { get; } = new();
    }
}
