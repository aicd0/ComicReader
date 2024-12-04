// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.DebugTools;
using ComicReader.Common.KVStorage;

namespace ComicReader.Common;

static class AppStatusPreserver
{
    private const string TAG = "AppStatusPreserver";
    private const string KEY_DEBUG_MODE = "debug_mode";
    private const string KEY_DEFAULT_ARCHIVE_CODE_PAGE = "default_archive_code_page";
    private const string KEY_ANTI_ALIASING_ENABLED = "anti_aliasing_enabled";
    private const string KEY_READING_COMIC_ID = "reading_comic_id";

    public static bool DebugMode
    {
        get
        {
#if DEBUG
            bool defaultState = true;
#else
            bool defaultState = false;
#endif
            return KVDatabase.GetDefaultMethod().GetBoolean(KVLib.APP, KEY_DEBUG_MODE, defaultState);
        }
        set
        {
            KVDatabase.GetDefaultMethod().SetBoolean(KVLib.APP, KEY_DEBUG_MODE, value);
        }
    }

    public static int DefaultArchiveCodePage
    {
        get
        {
            return (int)KVDatabase.GetDefaultMethod().GetLong(KVLib.APP, KEY_DEFAULT_ARCHIVE_CODE_PAGE, -1);
        }
        set
        {
            KVDatabase.GetDefaultMethod().SetLong(KVLib.APP, KEY_DEFAULT_ARCHIVE_CODE_PAGE, value);
        }
    }

    public static bool AntiAliasingEnabled
    {
        get
        {
            return KVDatabase.GetDefaultMethod().GetBoolean(KVLib.APP, KEY_ANTI_ALIASING_ENABLED, false);
        }
        set
        {
            KVDatabase.GetDefaultMethod().SetBoolean(KVLib.APP, KEY_ANTI_ALIASING_ENABLED, value);
        }
    }

    public static void SetReadingComic(long id)
    {
        Logger.I(TAG, $"SetReadingComic(id={id})");
        DebugUtils.Assert(id >= 0);
        KVDatabase.GetDefaultMethod().SetLong(KVLib.APP, KEY_READING_COMIC_ID, id);
    }

    public static void UnsetReadingComic()
    {
        Logger.I(TAG, "UnsetReadingComic");
        KVDatabase.GetDefaultMethod().Remove(KVLib.APP, KEY_READING_COMIC_ID);
    }

    public static long GetReadingComic()
    {
        long id = KVDatabase.GetDefaultMethod().GetLong(KVLib.APP, KEY_READING_COMIC_ID, -1);
        Logger.I(TAG, $"GetReadingComic(id={id})");
        return id;
    }
}
