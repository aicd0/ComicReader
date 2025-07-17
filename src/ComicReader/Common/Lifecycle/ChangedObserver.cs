// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;

namespace ComicReader.Common.Lifecycle;

public class ChangedObserver<T> : IObserver<T> where T : IEquatable<T>
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
