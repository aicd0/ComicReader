using System;

namespace ComicReader.Utils.KVDatabase
{
    internal class KVDatabaseLib
    {
        private WeakReference<KVDatabaseMethod> mMethod;
        private string mName;

        public KVDatabaseLib(KVDatabaseMethod method, string name)
        {
            mMethod = new WeakReference<KVDatabaseMethod>(method);
            mName = name;
        }

        public void setString(string key, string value)
        {
            if (!mMethod.TryGetTarget(out KVDatabaseMethod method))
            {
                return;
            }
            method.SetString(mName, key, value);
        }

        public string getString(string key)
        {
            if (!mMethod.TryGetTarget(out KVDatabaseMethod method))
            {
                return null;
            }
            return method.GetString(mName, key);
        }
    }
}
