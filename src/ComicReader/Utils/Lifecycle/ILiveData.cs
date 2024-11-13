// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Utils.Lifecycle;

internal interface ILiveData<T> : ILiveDataNoType
{
    public void Observe(Page owner, Action<T> observer);

    public void ObserveSticky(Page owner, Action<T> observer);

    public void Observe(Page owner, IObserver<T> observer);

    public void ObserveSticky(Page owner, IObserver<T> observer);

    public T GetValue();
}
