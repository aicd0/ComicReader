using ComicReader.Database;
using ComicReader.Utils;
using ComicReader.Views.Base;

namespace ComicReader.Views.Navigation;
internal class NavigationPageViewModel : BaseViewModel
{
    public LiveData<bool> IsPreviewButtonToggledLiveData { get; } = new(false);
    public LiveData<bool> RefreshLiveData { get; } = new();
    public LiveData<bool> IsFavoriteLiveData { get; } = new(false);
    public LiveData<bool> ExpandInfoPaneLiveData { get; } = new();
    public LiveData<bool> IsSidePaneOnLiveData { get; } = new(false);
    public LiveData<bool> IsExternalLiveData { get; } = new(true);
    public LiveData<ReaderSettingDataModel> ReaderSettingLiveData { get; } = new(AppDataRepository.ReaderSettings);
}
