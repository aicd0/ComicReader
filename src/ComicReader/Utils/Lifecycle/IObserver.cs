namespace ComicReader.Utils.Lifecycle;

internal interface IObserver<T>
{
    void OnChanged(T value);
}
