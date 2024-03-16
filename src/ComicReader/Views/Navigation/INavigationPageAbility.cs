namespace ComicReader.Views.Navigation;

internal interface INavigationPageAbility
{
    public delegate void ExpandInfoPaneEventHandler();
    public delegate void GridViewModeChangedEventHandler(bool enabled);
    public delegate void FavoriteChangedEventHandler(bool isFavorite);
    public delegate void ReaderSettingsChangedEventHandler(ReaderSettingDataModel settings);

    void RegisterReaderSettingsChangedEventHandler(ReaderSettingsChangedEventHandler handler);

    void SetReaderSettings(ReaderSettingDataModel settings);

    void SetExternalComic(bool isExternal);

    void RegisterFavoriteChangedEventHandler(FavoriteChangedEventHandler onFavoriteChanged);

    void SetFavorite(bool isFavorite);

    void RegisterGridViewModeChangedHandler(GridViewModeChangedEventHandler handler);

    void SetGridViewMode(bool enabled);

    void RegisterExpandInfoPaneHandler(ExpandInfoPaneEventHandler handler);

    bool GetIsSidePaneOpen();

    void SetIsSidePaneOpen(bool isOpen);

    void SetSearchBox(string text);
}