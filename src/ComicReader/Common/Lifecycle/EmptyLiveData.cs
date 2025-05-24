// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;

using Microsoft.UI.Xaml;

namespace ComicReader.Common.Lifecycle;

internal class EmptyLiveData<T> : IMutableLiveData<T>
{
    public void Clear()
    {
    }

    public void Emit(T value)
    {
    }

    public T GetValue()
    {
        return default;
    }

    public void Observe(FrameworkElement owner, Action<T> observer)
    {
    }

    public void Observe(FrameworkElement owner, IObserver<T> observer)
    {
    }

    public void ObserveSticky(FrameworkElement owner, Action<T> observer)
    {
    }

    public void ObserveSticky(FrameworkElement owner, IObserver<T> observer)
    {
    }
}
