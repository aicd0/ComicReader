// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.Helpers.Navigation;

internal static class RouterConstants
{
    public const string SCHEME_APP_NO_PREFIX = "comicreader";
    public const string SCHEME_APP = SCHEME_APP_NO_PREFIX + "://";

    public const string HOST_MAIN = "main";
    public const string HOST_READER = "reader";
    public const string HOST_HOME = "home";
    public const string HOST_SEARCH = "search";
    public const string HOST_SETTING = "setting";
    public const string HOST_HELP = "help";
    public const string HOST_FAVORITE = "favorite";
    public const string HOST_HISTORY = "history";
    public const string HOST_NAVIGATION = "navigation";
    public const string HOST_DEV_TOOLS = "dev_tools";

    public const string ARG_WINDOW_ID = "window_id";
    public const string ARG_COMIC_ID = "comic_id";
    public const string ARG_COMIC_TOKEN = "comic_token";
    public const string ARG_KEYWORD = "keyword";
}
