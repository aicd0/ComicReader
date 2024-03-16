namespace ComicReader.Utils;

internal class MutableLiveData<T> : LiveData<T>
{
    public MutableLiveData() : base() { }

    public MutableLiveData(T initialValue) : base(initialValue) { }

    public void Emit(T value)
    {
        EmitInternal(value);
    }
}
