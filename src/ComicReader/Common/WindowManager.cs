// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Threading;

using ComicReader.Common.DebugTools;

using Microsoft.UI.Xaml;

namespace ComicReader.Common;

class WindowManager<T> where T : Window
{
    private int _nextWindowId = 0;
    private readonly ConcurrentDictionary<int, T> _windows = [];

    public int RegisterWindow(T window)
    {
        int windowId = Interlocked.Increment(ref _nextWindowId);
        bool success = _windows.TryAdd(windowId, window);
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
        foreach (T window in _windows.Values)
        {
            return window;
        }
        DebugUtils.Assert(false);
        return null;
    }

    public T GetWindow(int windowId)
    {
        if (_windows.TryGetValue(windowId, out T window))
        {
            return window;
        }
        DebugUtils.Assert(false);
        return null;
    }
}
