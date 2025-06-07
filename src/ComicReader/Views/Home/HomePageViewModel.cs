// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.Lifecycle;
using ComicReader.Common.Threading;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.SDK.Common.Algorithm;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Threading;
using ComicReader.ViewModels;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Home;

internal class HomePageViewModel : INotifyPropertyChanged
{
    private const string TAG = nameof(HomePageViewModel);

    public event PropertyChangedEventHandler? PropertyChanged;

    public MutableLiveData<FilterModel> FilterLiveData = new();
    public MutableLiveData<bool> GroupingEnabledLiveData = new();
    public MutableLiveData<ComicFilterModel.ViewTypeEnum> ViewTypeLiveData = new();

    public ObservableCollectionPlus<ComicItemViewModel> UngroupedComicItems { get; set; } = [];
    public ObservableCollection<ComicGroupViewModel> GroupedComicItems { get; set; } = [];

    private bool _libraryEmptyVisible = false;
    public bool LibraryEmptyVisible
    {
        get => _libraryEmptyVisible;
        set
        {
            _libraryEmptyVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LibraryEmptyVisible)));
        }
    }

    private bool _isSelectMode = false;
    public bool IsSelectMode
    {
        get => _isSelectMode;
        set
        {
            _isSelectMode = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs($"{nameof(IsSelectMode)}"));
        }
    }

    private ListViewSelectionMode _comicItemSelectionMode = ListViewSelectionMode.None;
    public ListViewSelectionMode ComicItemSelectionMode
    {
        get => _comicItemSelectionMode;
        set
        {
            _comicItemSelectionMode = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs($"{nameof(ComicItemSelectionMode)}"));
        }
    }

    private bool _isCommandBarSelectAllToggled = false;
    public bool IsCommandBarSelectAllToggled
    {
        get => _isCommandBarSelectAllToggled;
        set
        {
            _isCommandBarSelectAllToggled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarSelectAllToggled)));
        }
    }

    private bool _isCommandBarFavoriteEnabled = false;
    public bool IsCommandBarFavoriteEnabled
    {
        get => _isCommandBarFavoriteEnabled;
        set
        {
            _isCommandBarFavoriteEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarFavoriteEnabled)));
        }
    }

    private bool _isCommandBarUnFavoriteEnabled = false;
    public bool IsCommandBarUnFavoriteEnabled
    {
        get => _isCommandBarUnFavoriteEnabled;
        set
        {
            _isCommandBarUnFavoriteEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarUnFavoriteEnabled)));
        }
    }

    private bool _isCommandBarHideEnabled = false;
    public bool IsCommandBarHideEnabled
    {
        get => _isCommandBarHideEnabled;
        set
        {
            _isCommandBarHideEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarHideEnabled)));
        }
    }

    private bool _isCommandBarUnHideEnabled = false;
    public bool IsCommandBarUnHideEnabled
    {
        get => _isCommandBarUnHideEnabled;
        set
        {
            _isCommandBarUnHideEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarUnHideEnabled)));
        }
    }

    private bool _isCommandBarMarkAsReadEnabled = false;
    public bool IsCommandBarMarkAsReadEnabled
    {
        get => _isCommandBarMarkAsReadEnabled;
        set
        {
            _isCommandBarMarkAsReadEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarMarkAsReadEnabled)));
        }
    }

    private bool _isCommandBarMarkAsUnreadEnabled = false;
    public bool IsCommandBarMarkAsUnreadEnabled
    {
        get => _isCommandBarMarkAsUnreadEnabled;
        set
        {
            _isCommandBarMarkAsUnreadEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarMarkAsUnreadEnabled)));
        }
    }

    private readonly ComicSearchEngine _searchEngine = new();
    private ComicFilterModel.ExternalModel _filterModel = new();
    private readonly List<ComicItemViewModel> _comicItems = [];
    private readonly List<ComicItemViewModel> _selectedComicItems = [];

    private readonly ITaskDispatcher _sharedDispatcher = TaskDispatcher.DefaultQueue;
    private int _updateFilterSubmitted = 0;
    private int _updateComicSubmitted = 0;

    private readonly List<ComicFilterModel.ViewTypeEnum> _viewTypes = [
        ComicFilterModel.ViewTypeEnum.Large,
        ComicFilterModel.ViewTypeEnum.Medium,
    ];

    /// <summary>
    /// Initializes the view model.
    /// </summary>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    public void Initialize()
    {
        _searchEngine.SetResultCallback(OnComicSearchResult);
        _searchEngine.Update();
        ScheduleUpdateFilters(true);
    }

    /// <summary>
    /// Reloads and applies the current filters.
    /// </summary>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    public void UpdateFilters()
    {
        ScheduleUpdateFilters(true);
    }

    /// <summary>
    /// Gets the current filter model.
    /// </summary>
    /// <returns>The current <see cref="ComicFilterModel.ExternalFilterModel"/>.</returns>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    public async Task<ComicFilterModel.ExternalFilterModel> GetFilter()
    {
        return await ThreadingUtils.Submit(_sharedDispatcher, "GetFilter", () =>
        {
            return _filterModel?.LastFilter?.Clone() ?? new();
        });
    }

    /// <summary>
    /// Selects the view type for displaying comics.
    /// </summary>
    /// <param name="viewType">The view type to select.</param>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    public void SelectViewType(ComicFilterModel.ViewTypeEnum viewType)
    {
        _sharedDispatcher.Submit("SelectViewType", delegate
        {
            bool modified = false;
            ComicFilterModel.ExternalFilterModel lastFilter = EnsureLastFilterNoLock();
            if (lastFilter.ViewType != viewType)
            {
                modified = true;
                lastFilter.ViewType = viewType;
            }
            if (modified)
            {
                _filterModel.LastFilterModified = true;
            }
            ScheduleUpdateFilters(false);
        });
    }

    /// <summary>
    /// Selects the sorting method for comics.
    /// </summary>
    /// <param name="model">The sorting model to apply.</param>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    public void SelectSortBy(SortByUIModel? model)
    {
        if (model == null)
        {
            return;
        }

        _sharedDispatcher.Submit("SelectSortBy", delegate
        {
            bool modified = false;
            ComicFilterModel.ExternalFilterModel lastFilter = EnsureLastFilterNoLock();
            if (model.IsProperty)
            {
                if (!model.Property.Equals(lastFilter.SortBy))
                {
                    modified = true;
                    lastFilter.SortBy = model.Property;
                }
            }
            else
            {
                if (lastFilter.SortByAscending != model.IsAscending)
                {
                    modified = true;
                    lastFilter.SortByAscending = model.IsAscending;
                }
            }
            if (modified)
            {
                _filterModel.LastFilterModified = true;
            }
            ScheduleUpdateFilters(false);
        });
    }

    /// <summary>
    /// Selects the property to group comics by.
    /// </summary>
    /// <param name="model">The property model to group by.</param>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    public void SelectGroupBy(SortByUIModel? model)
    {
        if (model == null)
        {
            return;
        }

        _sharedDispatcher.Submit("SelectGroupBy", delegate
        {
            bool modified = false;
            ComicFilterModel.ExternalFilterModel lastFilter = EnsureLastFilterNoLock();
            if (model.IsProperty)
            {
                modified = true;
                if (model.Property.Equals(lastFilter.GroupBy))
                {
                    lastFilter.GroupBy = null;
                }
                else
                {
                    lastFilter.GroupBy = model.Property;
                }
            }
            else
            {
                if (lastFilter.GroupByAscending != model.IsAscending)
                {
                    modified = true;
                    lastFilter.GroupByAscending = model.IsAscending;
                }
            }
            if (modified)
            {
                _filterModel.LastFilterModified = true;
            }
            ScheduleUpdateFilters(false);
        });
    }

    /// <summary>
    /// Selects a filter preset by name.
    /// </summary>
    /// <param name="name">The name of the filter preset.</param>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    public void SelectFilterPreset(string? name)
    {
        name ??= "";
        _sharedDispatcher.Submit("SelectFilterPreset", delegate
        {
            ComicFilterModel.ExternalFilterModel? filter = _filterModel.Filters.Find(x => x.Name == name);
            if (filter == null)
            {
                return;
            }
            _filterModel.LastFilter = filter.Clone();
            _filterModel.LastFilterModified = false;
            ScheduleUpdateFilters(false);
        });
    }

    /// <summary>
    /// Set the selection mode.
    /// </summary>
    /// <param name="enabled">Is selection mode enabled.</param>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    public void SetSelectionMode(bool enabled)
    {
        if (IsSelectMode == enabled)
        {
            return;
        }
        IsSelectMode = enabled;
        ComicItemSelectionMode = enabled ? ListViewSelectionMode.Multiple : ListViewSelectionMode.None;
        if (enabled)
        {
            _sharedDispatcher.Submit("SetSelectionMode", delegate
            {
                _selectedComicItems.Clear();
                UpdateCommandBarButtonsNoLock();
            });
        }
    }

    /// <summary>
    /// Sets the selected comic items.
    /// </summary>
    /// <param name="items">The selected comic items.</param>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    public void SetSelection(List<ComicItemViewModel> items)
    {
        _sharedDispatcher.Submit("SetSelection", delegate
        {
            _selectedComicItems.Clear();
            _selectedComicItems.AddRange(items);
            UpdateCommandBarButtonsNoLock();
        });
    }

    /// <summary>
    /// Applies a batch operation to the selected comic items.
    /// </summary>
    /// <param name="operationType">The operation type.</param>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    public void ApplyOperationToSelection(BatchOperationType operationType)
    {
        _sharedDispatcher.Submit("ApplyOperationToSelection", delegate
        {
            switch (operationType)
            {
                case BatchOperationType.Favorite:
                    {
                        List<ComicItemViewModel> items = _selectedComicItems.FindAll(x => !x.IsFavorite);
                        FavoriteModel.Instance.BatchAdd(items.ConvertAll(x => new FavoriteModel.FavoriteItem
                        {
                            Id = x.Comic.Id,
                            Title = x.Comic.Title,
                        })).Wait();
                        _ = MainThreadUtils.RunInMainThread(delegate
                        {
                            foreach (ComicItemViewModel item in items)
                            {
                                item.IsFavorite = true;
                            }
                        });
                    }
                    break;
                case BatchOperationType.UnFavorite:
                    {
                        List<ComicItemViewModel> items = _selectedComicItems.FindAll(x => x.IsFavorite);
                        FavoriteModel.Instance.BatchRemoveWithId(items.ConvertAll(x => x.Comic.Id)).Wait();
                        _ = MainThreadUtils.RunInMainThread(delegate
                        {
                            foreach (ComicItemViewModel item in items)
                            {
                                item.IsFavorite = false;
                            }
                        });
                    }
                    break;
                case BatchOperationType.Hide:
                    {
                        List<ComicItemViewModel> items = _selectedComicItems.FindAll(x => !x.IsHide);
                        foreach (ComicItemViewModel item in items)
                        {
                            item.Comic.SaveHiddenAsync(true).Wait();
                        }
                        ScheduleUpdateComics();
                    }
                    break;
                case BatchOperationType.UnHide:
                    {
                        List<ComicItemViewModel> items = _selectedComicItems.FindAll(x => x.IsHide);
                        foreach (ComicItemViewModel item in items)
                        {
                            item.Comic.SaveHiddenAsync(false).Wait();
                        }
                        ScheduleUpdateComics();
                    }
                    break;
                case BatchOperationType.MarkAsRead:
                    {
                        List<ComicItemViewModel> items = _selectedComicItems.FindAll(x => !x.IsRead);
                        foreach (ComicItemViewModel item in items)
                        {
                            item.Comic.SetAsRead();
                        }
                        _ = MainThreadUtils.RunInMainThread(delegate
                        {
                            foreach (ComicItemViewModel item in items)
                            {
                                item.UpdateProgress(true);
                            }
                        });
                    }
                    break;
                case BatchOperationType.MarkAsUnread:
                    {
                        List<ComicItemViewModel> items = _selectedComicItems.FindAll(x => !x.IsUnread);
                        foreach (ComicItemViewModel item in items)
                        {
                            item.Comic.SetAsUnread();
                        }
                        _ = MainThreadUtils.RunInMainThread(delegate
                        {
                            foreach (ComicItemViewModel item in items)
                            {
                                item.UpdateProgress(true);
                            }
                        });
                    }
                    break;
                default:
                    break;
            }
            UpdateCommandBarButtonsNoLock();
        });
    }

    /// <summary>
    /// Search the comics by keywords.
    /// </summary>
    /// <param name="searchText">The search text.</param>
    public void SetSearchText(string searchText)
    {
        _searchEngine.SetSearchText(searchText);
    }

    /// <summary>
    /// Updates the comic library and refreshes the displayed items.
    /// </summary>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    public void UpdateLibrary()
    {
        _searchEngine.Update();
    }

    private void OnComicSearchResult(IReadOnlyList<ComicModel> items)
    {
        _sharedDispatcher.Submit("OnComicSearchResult", delegate
        {
            _comicItems.Clear();
            foreach (ComicModel item in items)
            {
                var model = new ComicItemViewModel();
                model.Update(item);
                _comicItems.Add(model);
            }
            ScheduleUpdateComics();
        });
    }

    private void UpdateCommandBarButtonsNoLock()
    {
        bool allSelected = _selectedComicItems.Count == _comicItems.Count;
        bool favoriteEnabled = false;
        bool unfavoriteEnabled = false;
        bool hideEnabled = false;
        bool unhideEnabled = false;
        bool markAsReadEnabled = false;
        bool markAsUnreadEnabled = false;

        foreach (ComicItemViewModel item in _selectedComicItems)
        {
            if (item.IsFavorite)
            {
                unfavoriteEnabled = true;
            }
            else
            {
                favoriteEnabled = true;
            }

            if (item.IsHide)
            {
                unhideEnabled = true;
            }
            else
            {
                hideEnabled = true;
            }

            if (!item.IsRead)
            {
                markAsReadEnabled = true;
            }

            if (!item.IsUnread)
            {
                markAsUnreadEnabled = true;
            }
        }

        _ = MainThreadUtils.RunInMainThread(delegate
        {
            IsCommandBarSelectAllToggled = allSelected;
            IsCommandBarFavoriteEnabled = favoriteEnabled;
            IsCommandBarUnFavoriteEnabled = unfavoriteEnabled;
            IsCommandBarHideEnabled = hideEnabled;
            IsCommandBarUnHideEnabled = unhideEnabled;
            IsCommandBarMarkAsReadEnabled = markAsReadEnabled;
            IsCommandBarMarkAsUnreadEnabled = markAsUnreadEnabled;
        });
    }

    private void ScheduleUpdateFilters(bool reloadFromDatabase)
    {
        if (Interlocked.CompareExchange(ref _updateFilterSubmitted, 1, 0) == 1)
        {
            return;
        }
        _sharedDispatcher.Submit("UpdateFilters", delegate
        {
            Interlocked.Exchange(ref _updateFilterSubmitted, 0);
            UpdateFiltersNoLock(reloadFromDatabase).Wait();
        });
    }

    private void ScheduleUpdateComics()
    {
        if (Interlocked.CompareExchange(ref _updateComicSubmitted, 1, 0) == 1)
        {
            return;
        }
        _sharedDispatcher.Submit("UpdateComics", delegate
        {
            Interlocked.Exchange(ref _updateComicSubmitted, 0);
            UpdateComicsNoLock().Wait();
        });
    }

    private async Task UpdateFiltersNoLock(bool reloadFromDatabase)
    {
        Logger.I(TAG, "UpdateFiltersNoLock");

        // Validate and save
        if (reloadFromDatabase || _filterModel == null)
        {
            _filterModel = await ComicFilterModel.Instance.GetModel() ?? new();
        }
        List<ComicFilterModel.ExternalFilterModel> filters = _filterModel.Filters;
        if (filters == null || filters.Count == 0)
        {
            filters = [CreateDefaultFilter()];
            _filterModel.Filters = filters;
        }
        filters.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

        ComicFilterModel.ExternalFilterModel? lastFilter = _filterModel.LastFilter ?? filters[0].Clone();
        _filterModel.LastFilter = lastFilter;

        ComicPropertyModel sortBy = lastFilter.SortBy;
        sortBy ??= new();
        lastFilter.SortBy = sortBy;

        ComicPropertyModel? groupBy = lastFilter.GroupBy;
        _ = ComicFilterModel.Instance.UpdateModel(_filterModel);

        // Update UI
        var viewTypeDropDown = new DropDownButtonModel<ComicFilterModel.ViewTypeEnum>
        {
            Name = "View",
            Items = _viewTypes.ConvertAll(x => CreateToggleMenuFlyoutItem(ViewTypeToDisplayName(x), x == lastFilter.ViewType, x)),
        };

        List<ComicPropertyModel> properties = await ComicPropertyModel.GetProperties();
        var sortByDropDown = new DropDownButtonModel<SortByUIModel>
        {
            Name = "Sort",
            Items = CreateSortByMenuItems(properties, lastFilter.SortBy, lastFilter.SortByAscending),
        };

        var groupByDropDown = new DropDownButtonModel<SortByUIModel>
        {
            Name = "Group",
            Items = CreateSortByMenuItems(properties, lastFilter.GroupBy, lastFilter.GroupByAscending),
        };

        string lastFilterName = lastFilter.Name ?? "";
        if (_filterModel.LastFilterModified)
        {
            lastFilterName += " *";
        }
        var filterPresetDropDown = new DropDownButtonModel<string>
        {
            Name = lastFilterName,
            Items = filters.ConvertAll(x => CreateMenuFlyoutItem(x.Name, x.Name)),
        };

        var uiModel = new FilterModel
        {
            ViewTypeDropDown = viewTypeDropDown,
            SortByDropDown = sortByDropDown,
            GroupByDropDown = groupByDropDown,
            FilterPresetDropDown = filterPresetDropDown,
        };
        FilterLiveData.Emit(uiModel);
        ViewTypeLiveData.Emit(lastFilter.ViewType);

        ScheduleUpdateComics();
    }

    private async Task UpdateComicsNoLock()
    {
        Logger.I(TAG, "UpdateComicsNoLock");

        IReadOnlyList<ComicItemViewModel> comicItems = _comicItems;
        bool isEmpty = comicItems.Count == 0;
        List<ComicItemViewModel>? comicsUngrouped = null;
        List<ComicGroupViewModel>? comicsGrouped = null;

        if (isEmpty)
        {
            comicsUngrouped = [];
        }
        else
        {
            ComicFilterModel.ExternalFilterModel filter = _filterModel.LastFilter ?? CreateDefaultFilter();
            ComicPropertyModel sortBy = filter.SortBy;
            bool sortByAscending = filter.SortByAscending;
            ComicPropertyModel? groupBy = filter.GroupBy;
            bool groupByAscending = filter.GroupByAscending;

            if (groupBy != null)
            {
                Dictionary<string, List<ComicItemViewModel>> groupMap = [];
                foreach (ComicItemViewModel item in comicItems)
                {
                    string groupName = groupBy.GetPropertyAsGroupName(item.Comic);
                    if (!groupMap.TryGetValue(groupName, out List<ComicItemViewModel>? group))
                    {
                        group = [];
                        groupMap[groupName] = group;
                    }
                    group.Add(item);
                }
                comicsGrouped = [];
                foreach (KeyValuePair<string, List<ComicItemViewModel>> p in groupMap)
                {
                    List<ComicItemViewModel> sorted = SortComicItemsByProerty(p.Value, sortBy, sortByAscending);
                    var group = new ComicGroupViewModel(p.Key, sorted, false);
                    comicsGrouped.Add(group);
                }
                if (groupByAscending)
                {
                    comicsGrouped.Sort((x, y) => x.GroupName.CompareTo(y.GroupName));
                }
                else
                {
                    comicsGrouped.Sort((x, y) => y.GroupName.CompareTo(x.GroupName));
                }
            }
            else
            {
                comicsUngrouped = SortComicItemsByProerty(comicItems, sortBy, sortByAscending);
            }
        }

        await MainThreadUtils.RunInMainThread(delegate
        {
            LibraryEmptyVisible = isEmpty;

            Func<ComicItemViewModel, ComicItemViewModel, bool> comparer = (ComicItemViewModel x, ComicItemViewModel y) =>
                x.Comic.Title == y.Comic.Title &&
                x.Rating == y.Rating &&
                x.Progress == y.Progress &&
                x.IsFavorite == y.IsFavorite;
            if (comicsGrouped != null)
            {
                Dictionary<string, ComicGroupViewModel> groupMap = [];
                foreach (ComicGroupViewModel item in comicsGrouped)
                {
                    groupMap[item.GroupName] = item;
                }
                foreach (ComicGroupViewModel item in GroupedComicItems)
                {
                    if (groupMap.TryGetValue(item.GroupName, out ComicGroupViewModel? group))
                    {
                        item.UpdateItems(group.Items, comparer);
                    }
                }
                DiffUtils.UpdateCollection(GroupedComicItems, comicsGrouped, (ComicGroupViewModel x, ComicGroupViewModel y) => x.GroupName == y.GroupName);
                GroupingEnabledLiveData.Emit(true);
            }
            else if (comicsUngrouped != null)
            {
                DiffUtils.UpdateCollection(UngroupedComicItems, comicsUngrouped, comparer);
                GroupingEnabledLiveData.Emit(false);
            }
        });
    }

    private List<ComicItemViewModel> SortComicItemsByProerty(IReadOnlyList<ComicItemViewModel> items, ComicPropertyModel property, bool ascending)
    {
        var paired = items
            .Select(x => new KeyValuePair<IComparable, ComicItemViewModel>(property.GetPropertyAsComparable(x.Comic), x))
            .ToList();
        if (ascending)
        {
            paired.Sort((x, y) => x.Key.CompareTo(y.Key));
        }
        else
        {
            paired.Sort((x, y) => y.Key.CompareTo(x.Key));
        }
        return [.. paired.Select(x => x.Value)];
    }

    private ComicFilterModel.ExternalFilterModel EnsureLastFilterNoLock()
    {
        ComicFilterModel.ExternalFilterModel filter = _filterModel.LastFilter ?? CreateDefaultFilter();
        _filterModel.LastFilter = filter;
        return filter;
    }

    private ComicFilterModel.ExternalFilterModel CreateDefaultFilter()
    {
        return new ComicFilterModel.ExternalFilterModel
        {
            Name = "Default",
            ViewType = ComicFilterModel.ViewTypeEnum.Large,
            SortBy = new(),
            SortByAscending = true,
            GroupBy = null,
            GroupByAscending = true,
            Expression = "TRUE",
        };
    }

    private List<MenuFlyoutItemModel<SortByUIModel>> CreateSortByMenuItems(List<ComicPropertyModel> properties, ComicPropertyModel? selectedProperty, bool ascending)
    {
        Dictionary<string, List<ComicPropertyModel>> propertyGroupMap = new();
        foreach (ComicPropertyModel property in properties)
        {
            string groupName = property.DisplayGroupName;
            if (!propertyGroupMap.ContainsKey(groupName))
            {
                propertyGroupMap[groupName] = [];
            }
            propertyGroupMap[groupName].Add(property);
        }

        List<ComicPropertyModel>? plainProperties = null;
        List<KeyValuePair<string, List<ComicPropertyModel>>> propertyGroupList = [];
        foreach (KeyValuePair<string, List<ComicPropertyModel>> kvp in propertyGroupMap)
        {
            if (kvp.Key == "")
            {
                plainProperties = kvp.Value;
                continue;
            }
            propertyGroupList.Add(kvp);
            kvp.Value.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName));
        }
        propertyGroupList.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));

        List<MenuFlyoutItemModel<SortByUIModel>> items = [];
        if (plainProperties != null)
        {
            foreach (ComicPropertyModel p in plainProperties)
            {
                items.Add(CreateToggleMenuFlyoutItem(p.DisplayName, p.Equals(selectedProperty), new SortByUIModel
                {
                    IsProperty = true,
                    Property = p,
                }));
            }
        }
        foreach (KeyValuePair<string, List<ComicPropertyModel>> kvp in propertyGroupList)
        {
            List<MenuFlyoutItemModel<SortByUIModel>> subItems = [];
            foreach (ComicPropertyModel p in kvp.Value)
            {
                subItems.Add(CreateToggleMenuFlyoutItem(p.DisplayName, p.Equals(selectedProperty), new SortByUIModel
                {
                    IsProperty = true,
                    Property = p,
                }));
            }
            items.Add(new MenuFlyoutItemModel<SortByUIModel>
            {
                Name = kvp.Key,
                SubItems = subItems,
            });
        }
        items.Add(CreateSeperatorMenuFlyoutItem<SortByUIModel>());
        items.Add(CreateToggleMenuFlyoutItem("Ascending", ascending, new SortByUIModel
        {
            IsProperty = false,
            IsAscending = true,
        }));
        items.Add(CreateToggleMenuFlyoutItem("Descending", !ascending, new SortByUIModel
        {
            IsProperty = false,
            IsAscending = false,
        }));
        return items;
    }

    private MenuFlyoutItemModel<T> CreateMenuFlyoutItem<T>(string name, T dataContext)
    {
        return new MenuFlyoutItemModel<T>
        {
            Name = name,
            DataContext = dataContext
        };
    }

    private MenuFlyoutItemModel<T> CreateToggleMenuFlyoutItem<T>(string name, bool toggled, T dataContext)
    {
        return new MenuFlyoutItemModel<T>
        {
            Name = name,
            CanToggle = true,
            Toggled = toggled,
            DataContext = dataContext
        };
    }

    private MenuFlyoutItemModel<T> CreateSeperatorMenuFlyoutItem<T>()
    {
        return new MenuFlyoutItemModel<T>
        {
            IsSeperator = true,
        };
    }

    private string ViewTypeToDisplayName(ComicFilterModel.ViewTypeEnum viewType)
    {
        return viewType switch
        {
            ComicFilterModel.ViewTypeEnum.Large => "Large",
            ComicFilterModel.ViewTypeEnum.Medium => "Medium",
            _ => "Unknown"
        };
    }

    public class FilterModel
    {
        public DropDownButtonModel<ComicFilterModel.ViewTypeEnum> ViewTypeDropDown { get; set; } = new();
        public DropDownButtonModel<SortByUIModel> SortByDropDown { get; set; } = new();
        public DropDownButtonModel<SortByUIModel> GroupByDropDown { get; set; } = new();
        public DropDownButtonModel<string> FilterPresetDropDown { get; set; } = new();
    }

    public class DropDownButtonModel<T>
    {
        public string Name { get; set; } = "";
        public List<MenuFlyoutItemModel<T>> Items { get; set; } = [];
    }

    public class MenuFlyoutItemModel<T>
    {
        public bool IsSeperator { get; set; } = false;
        public string Name { get; set; } = "";
        public bool CanToggle { get; set; } = false;
        public bool Toggled { get; set; } = false;
        public List<MenuFlyoutItemModel<T>>? SubItems { get; set; } = null;
        public T? DataContext { get; set; } = default;
    }

    public class SortByUIModel
    {
        public bool IsProperty { get; set; }
        public bool IsAscending { get; set; }
        public ComicPropertyModel Property { get; set; } = new();
    }

    public enum BatchOperationType
    {
        Favorite,
        UnFavorite,
        Hide,
        UnHide,
        MarkAsRead,
        MarkAsUnread,
    }
}
