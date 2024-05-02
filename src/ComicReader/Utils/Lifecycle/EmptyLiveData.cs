using Microsoft.UI.Xaml.Controls;
using System;

namespace ComicReader.Utils.Lifecycle;

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
