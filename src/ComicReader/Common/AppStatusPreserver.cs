using ComicReader.Common.Constants;
using ComicReader.Common.Structs;
using ComicReader.Utils;
using ComicReader.Utils.KVDatabase;
using System;
using System.Text.Json;

namespace ComicReader.Common
{
    class AppStatusPreserver
    {
        private const string TAG = "AppStatusPreserver";
        private const string KEY = "last_app_status_data";

        private static AppStatusPreserver _instance = new AppStatusPreserver();

        public static AppStatusPreserver Instance { get => _instance; }

        private static LastAppStatusData _data;

        private void EnsureInitialized()
        {
            if (_data != null)
            {
                return;
            }
            _data = new LastAppStatusData();
            string serialized = KVDatabase.getInstance().getDefaultMethod().GetString(KVLib.APP, KEY);
            if (serialized != null)
            {
                try
                {
                    _data = JsonSerializer.Deserialize<LastAppStatusData>(serialized);
                }
                catch (Exception e)
                {
                    Debug.LogException(TAG, e);
                }
            }
        }

        private void Save()
        {
            string serialized = JsonSerializer.Serialize(_data);
            KVDatabase.getInstance().getDefaultMethod().SetString(KVLib.APP, KEY, serialized);
        }

        public long GetLastComic()
        {
            EnsureInitialized();
            return _data.ReadingComic;
        }

        public void SetReadingComic(long id)
        {
            System.Diagnostics.Debug.Assert(id >= 0);
            EnsureInitialized();
            if (_data.ReadingComic == id)
            {
                return;
            }
            _data.ReadingComic = id;
            Save();
        }

        public void UnsetReadingComic(long id)
        {
            System.Diagnostics.Debug.Assert(id >= 0);
            EnsureInitialized();
            if (_data.ReadingComic != id)
            {
                return;
            }
            _data.ReadingComic = -1;
            Save();
        }
    }
}
