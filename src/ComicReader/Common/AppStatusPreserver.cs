// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.DebugTools;
using ComicReader.Common.KVStorage;

namespace ComicReader.Common;

static class AppStatusPreserver
{
    private const string TAG = "AppStatusPreserver";
    private const string KEY_DEFAULT_ARCHIVE_CODE_PAGE = "default_archive_code_page";
    private const string KEY_ANTI_ALIASING_ENABLED = "anti_aliasing_enabled";
    private const string KEY_READING_COMIC_ID = "reading_comic_id";

    public static int DefaultArchiveCodePage
    {
        get
        {
            return (int)KVDatabase.GetDefaultMethod().GetLong(GlobalConstants.KV_DB_APP, KEY_DEFAULT_ARCHIVE_CODE_PAGE, -1);
        }
        set
        {
            KVDatabase.GetDefaultMethod().SetLong(GlobalConstants.KV_DB_APP, KEY_DEFAULT_ARCHIVE_CODE_PAGE, value);
        }
    }

    public static bool AntiAliasingEnabled
    {
        get
        {
            return KVDatabase.GetDefaultMethod().GetBoolean(GlobalConstants.KV_DB_APP, KEY_ANTI_ALIASING_ENABLED, false);
        }
        set
        {
            KVDatabase.GetDefaultMethod().SetBoolean(GlobalConstants.KV_DB_APP, KEY_ANTI_ALIASING_ENABLED, value);
        }
    }

    public static void SetReadingComic(long id)
    {
        Logger.I(TAG, $"SetReadingComic(id={id})");
        DebugUtils.Assert(id >= 0);
        KVDatabase.GetDefaultMethod().SetLong(GlobalConstants.KV_DB_APP, KEY_READING_COMIC_ID, id);
    }

    public static void UnsetReadingComic()
    {
        Logger.I(TAG, "UnsetReadingComic");
        KVDatabase.GetDefaultMethod().Remove(GlobalConstants.KV_DB_APP, KEY_READING_COMIC_ID);
    }

    public static long GetReadingComic()
    {
        long id = KVDatabase.GetDefaultMethod().GetLong(GlobalConstants.KV_DB_APP, KEY_READING_COMIC_ID, -1);
        Logger.I(TAG, $"GetReadingComic(id={id})");
        return id;
    }
}
