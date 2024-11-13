// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Utils.Lifecycle;
using ComicReader.Views.Base;

namespace ComicReader.Views.Navigation;
internal class NavigationPageViewModel : BaseViewModel
{
    private readonly MutableLiveData<bool> _gridViewModeEnabled = new(false);
    public LiveData<bool> GridViewModeEnabledLiveData => _gridViewModeEnabled;

    private readonly MutableLiveData<bool> _isFavoriteLiveData = new(false);
    public LiveData<bool> IsFavoriteLiveData => _isFavoriteLiveData;

    public void SetIsFavorite(bool isFavorite)
    {
        _isFavoriteLiveData.Emit(isFavorite);
    }

    public void SetGridViewMode(bool enabled)
    {
        _gridViewModeEnabled.Emit(enabled);
    }
}
