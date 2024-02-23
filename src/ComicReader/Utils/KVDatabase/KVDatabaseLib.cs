using System;

namespace ComicReader.Utils.KVDatabase;

internal class KVDatabaseLib
{
    private readonly WeakReference<KVDatabaseMethod> mMethod;
    private readonly string mName;

    public KVDatabaseLib(KVDatabaseMethod method, string name)
    {
        mMethod = new WeakReference<KVDatabaseMethod>(method);
        mName = name;
    }

    public void SetString(string key, string value)
    {
        if (!mMethod.TryGetTarget(out KVDatabaseMethod method))
        {
            return;
        }

        method.SetString(mName, key, value);
    }

    public string GetString(string key)
    {
        if (!mMethod.TryGetTarget(out KVDatabaseMethod method))
        {
            return null;
        }

        return method.GetString(mName, key);
    }
}
