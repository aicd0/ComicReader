// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.Constants;
using ComicReader.Utils;
using ComicReader.Utils.KVDatabase;

namespace ComicReader.Common;

static class AppStatusPreserver
{
    private const string TAG = "AppStatusPreserver";
    private const string KEY_READING_COMIC_ID = "reading_comic_id";

    public static void SetReadingComic(long id)
    {
        Logger.I(TAG, $"SetReadingComic(id={id})");
        System.Diagnostics.Debug.Assert(id >= 0);
        KVDatabase.GetInstance().GetDefaultMethod().SetLong(KVLib.APP, KEY_READING_COMIC_ID, id);
    }

    public static void UnsetReadingComic()
    {
        Logger.I(TAG, "UnsetReadingComic");
        KVDatabase.GetInstance().GetDefaultMethod().Remove(KVLib.APP, KEY_READING_COMIC_ID);
    }

    public static long GetReadingComic()
    {
        long id = KVDatabase.GetInstance().GetDefaultMethod().GetLong(KVLib.APP, KEY_READING_COMIC_ID, -1);
        Logger.I(TAG, $"GetReadingComic(id={id})");
        return id;
    }
}
