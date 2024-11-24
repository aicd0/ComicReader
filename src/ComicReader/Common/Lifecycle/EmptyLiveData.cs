// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using Microsoft.UI.Xaml.Controls;

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

    public void Observe(Page owner, Action<T> observer)
    {
    }

    public void Observe(Page owner, IObserver<T> observer)
    {
    }

    public void ObserveSticky(Page owner, Action<T> observer)
    {
    }

    public void ObserveSticky(Page owner, IObserver<T> observer)
    {
    }
}
