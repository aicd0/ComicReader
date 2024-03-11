﻿using System;

namespace ComicReader.Utils;

internal class ChangedObserver<T> : IObserver<T> where T : IEquatable<T>
{
    private T _lastValue;
    private bool _initialized = false;
    private readonly Action<T> _action;

    public ChangedObserver(Action<T> action)
    {
        _action = action;
    }

    public void OnChanged(T value)
    {
        bool equal = _initialized && _lastValue?.Equals(value) == true;
        _initialized = true;
        _lastValue = value;

        if (!equal)
        {
            _action(value);
        }
    }
}
