namespace ComicReader.Utils.Lifecycle;

internal interface IMutableLiveData<T> : ILiveData<T>
{
    void Emit(T value);
}
