// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

namespace ComicReader.Common.Flow;

internal class InputOperation<T>(T initial, FlowScope scope) : IFlowOperation<T> where T : IEquatable<T>
{
    private readonly object _lock = new();
    private readonly FlowScope _scope = scope;
    private T _value = initial;

    private event Action ValueChanged;

    public void RegisterObserver(Action observer)
    {
        ValueChanged += observer;
    }

    public T CalculateValue()
    {
        return _value;
    }

    public T CalculateIntermediateValue()
    {
        return _value;
    }

    public void Set(T value, bool isIntermediate)
    {
        _scope.AcquireCommitLock();

        bool notify = false;

        lock (_lock)
        {
            if (!_value.Equals(value))
            {
                _value = value;
                notify = true;
            }
        }

        if (notify)
        {
            ValueChanged?.Invoke();
        }

        _scope.ReleaseCommitLock();

        if (!isIntermediate)
        {
            _scope.Commit();
        }
    }
}
