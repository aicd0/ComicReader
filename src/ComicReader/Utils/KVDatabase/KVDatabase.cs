using System.Collections.Generic;

namespace ComicReader.Utils.KVDatabase
{
    internal class KVDatabase
    {
        private KVDatabase() { }

        public KVDatabaseMethod getDefaultMethod()
        {
            return KVDatabaseMethodLiteDB.GetInstance();
        }

        static KVDatabase mInstance;

        public static KVDatabase getInstance()
        {
            if (mInstance == null)
            {
                mInstance = new KVDatabase();
            }
            return mInstance;
        }
    }
}
