namespace ComicReader.Router;
internal static class RouterConstants
{
    public const string SCHEME_APP_NO_PREFIX = "comicreader";
    public const string SCHEME_APP = SCHEME_APP_NO_PREFIX + "://";

    public const string HOST_READER = "reader";
    public const string HOST_HOME = "home";
    public const string HOST_SEARCH = "search";
    public const string HOST_SETTING = "setting";
    public const string HOST_HELP = "help";
    public const string HOST_FAVORITE = "favorite";
    public const string HOST_HISTORY = "history";
    public const string HOST_NAVIGATION = "navigation";

    public const string ARG_COMIC_ID = "comic_id";
    public const string ARG_KEYWORD = "keyword";
}
