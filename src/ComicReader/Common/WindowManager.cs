// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

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
        Logger.Assert(success, "B62A8795DA9036E2");
        return windowId;
    }

    public void UnregisterWindow(int windowId)
    {
        bool success = _windows.TryRemove(windowId, out _);
        Logger.Assert(success, "1A3BA06AD5A4351E");
    }

    public T GetAnyWindow()
    {
        foreach (WindowWrapper wrapper in _windows.Values)
        {
            return wrapper.Window;
        }
        Logger.AssertNotReachHere("0C81B273522AB715");
        return null;
    }

    public T GetWindow(int windowId)
    {
        if (_windows.TryGetValue(windowId, out WindowWrapper wrapper))
        {
            return wrapper.Window;
        }
        Logger.AssertNotReachHere("6046D73C153C55AB");
        return null;
    }

    public EventBus GetEventBus(int windowId)
    {
        if (_windows.TryGetValue(windowId, out WindowWrapper wrapper))
        {
            return wrapper.EventBus;
        }
        Logger.AssertNotReachHere("2399192BB68F8CC4");
        return null;
    }

    private class WindowWrapper
    {
        public T Window { get; set; }
        public EventBus EventBus { get; } = new();
    }
}
