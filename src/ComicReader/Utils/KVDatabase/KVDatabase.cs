namespace ComicReader.Utils.KVDatabase
{
    internal class KVDatabase
    {
        private KVDatabase() { }

        public KVDatabaseMethod GetDefaultMethod()
        {
            return KVDatabaseMethodLiteDB.GetInstance();
        }

        static KVDatabase mInstance;

        public static KVDatabase GetInstance()
        {
            mInstance ??= new KVDatabase();
            return mInstance;
        }
    }
}
