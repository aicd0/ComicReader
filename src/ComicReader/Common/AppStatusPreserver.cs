using ComicReader.Common.Constants;
using ComicReader.Utils.KVDatabase;

namespace ComicReader.Common
{
    static class AppStatusPreserver
    {
        private const string KEY_READING_COMIC_ID = "reading_comic_id";

        public static void SetReadingComic(long id)
        {
            System.Diagnostics.Debug.Assert(id >= 0);
            KVDatabase.GetInstance().GetDefaultMethod().SetLong(KVLib.APP, KEY_READING_COMIC_ID, id);
        }

        public static void UnsetReadingComic()
        {
            KVDatabase.GetInstance().GetDefaultMethod().Remove(KVLib.APP, KEY_READING_COMIC_ID);
        }

        public static long GetReadingComic()
        {
            return KVDatabase.GetInstance().GetDefaultMethod().GetLong(KVLib.APP, KEY_READING_COMIC_ID, -1);
        }
    }
}
