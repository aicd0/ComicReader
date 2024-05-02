namespace ComicReader.Utils.Lifecycle;

internal class MutableLiveData<T> : LiveData<T>, IMutableLiveData<T>
{
    public MutableLiveData() : base() { }

    public MutableLiveData(T initialValue) : base(initialValue) { }

    public void Emit(T value)
    {
        EmitInternal(value);
    }
}
