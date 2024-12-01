// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReader.Common.DebugTools;

namespace ComicReader.Common.Flow;

internal abstract class OutputFlow<T> : IFlow<T> where T : IEquatable<T>
{
    protected readonly FlowScope _scope;

    private readonly object _lock = new();
    private readonly IFlowOperation<T> _operation;
    private readonly SyncHandler _syncHandler;
    private readonly WeakEventManager _intermediateValueChangedEventManager = new();
    private T _value;
    private T _intermediateValue;

    protected OutputFlow(FlowScope scope, IFlowOperation<T> operation)
    {
        _scope = scope;
        _operation = operation;
        _syncHandler = new(this);
        _value = operation.CalculateValue();
        _intermediateValue = _value;

        operation.RegisterObserver(OnParentValueChanged);

        scope.AcquireCommitLock();
        OnParentValueChanged();
        scope.ReleaseCommitLock();
    }

    public event IFlow<T>.ValueChangedEventHandler ValueChanged;

    public T Get()
    {
        return _value;
    }

    protected T GetIntermediate()
    {
        return _intermediateValue;
    }

    protected void ObserveIntermediate(Action observer)
    {
        _intermediateValueChangedEventManager.AddHandler(observer);
    }

    private void OnParentValueChanged()
    {
        bool notify = false;
        T value = _operation.CalculateIntermediateValue();

        lock (_lock)
        {
            if (!_intermediateValue.Equals(value))
            {
                _intermediateValue = value;
                notify = true;
            }

            if (_value.Equals(_intermediateValue))
            {
                _scope.RemoveSyncHandler(_syncHandler);
            }
            else
            {
                _scope.AddSyncHandler(_syncHandler);
            }
        }

        if (notify)
        {
            _intermediateValueChangedEventManager?.Invoke();
        }
    }

    private class SyncHandler(OutputFlow<T> flow) : FlowScope.ISyncHandler
    {
        public void BeforeCommit()
        {
            DebugUtils.Assert(!flow._value.Equals(flow._intermediateValue));
            flow._value = flow._intermediateValue;
        }

        public void OnCommit()
        {
            flow.ValueChanged?.Invoke(flow._value);
        }
    }
}
