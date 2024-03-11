using ComicReader.Utils;

namespace ComicReader.Views.Navigation;
internal interface INavigationPageAbility
{
    LiveData<bool> GetPreviewButtonToggledLiveData();

    LiveData<ReaderSettingDataModel> GetReaderSettingLiveData();

    LiveData<bool> GetIsExternalLiveData();

    LiveData<bool> GetIsFavoriteLiveData();

    LiveData<bool> GetIsPreviewModeLiveData();

    LiveData<bool> GetExpandInfoPaneLiveData();

    LiveData<bool> GetIsSidePaneOnLiveData();

    LiveData<bool> GetRefreshLiveData();

    void SetSearchBox(string text);
}
