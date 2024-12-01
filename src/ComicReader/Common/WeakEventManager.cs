// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ComicReader.Common;

internal class WeakEventManager<T>
{
    private static readonly object sObject = new();

    private readonly ConditionalWeakTable<Action<T>, object> _handlers = [];

    public void Invoke(T arg)
    {
        List<Action<T>> handlers = [];
        foreach (KeyValuePair<Action<T>, object> pair in _handlers)
        {
            handlers.Add(pair.Key);
        }
        foreach (Action<T> handler in handlers)
        {
            handler.Invoke(arg);
        }
    }

    public void AddHandler(Action<T> handler)
    {
        _handlers.TryAdd(handler, sObject);
    }

    public void RemoveHandler(Action<T> handler)
    {
        _handlers.Remove(handler);
    }

    public void ClearHandlers()
    {
        _handlers.Clear();
    }
}

internal class WeakEventManager
{
    private static readonly object sObject = new();

    private readonly ConditionalWeakTable<Action, object> _handlers = [];

    public void Invoke()
    {
        List<Action> handlers = [];
        foreach (KeyValuePair<Action, object> pair in _handlers)
        {
            handlers.Add(pair.Key);
        }
        foreach (Action handler in handlers)
        {
            handler.Invoke();
        }
    }

    public void AddHandler(Action handler)
    {
        _handlers.TryAdd(handler, sObject);
    }

    public void RemoveHandler(Action handler)
    {
        _handlers.Remove(handler);
    }

    public void ClearHandlers()
    {
        _handlers.Clear();
    }
}
