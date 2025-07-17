// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using Microsoft.UI.Xaml;

namespace ComicReader.Common.Lifecycle;

public interface ILiveData<T> : ILiveDataNoType
{
    public void Observe(FrameworkElement owner, Action<T> observer);

    public void ObserveSticky(FrameworkElement owner, Action<T> observer);

    public void Observe(FrameworkElement owner, IObserver<T> observer);

    public void ObserveSticky(FrameworkElement owner, IObserver<T> observer);

    public T GetValue();
}
