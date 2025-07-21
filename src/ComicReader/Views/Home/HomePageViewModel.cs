// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
using ComicReader.UserControls.ComicItemView;
using ComicReader.ViewModels;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Home;

internal partial class HomePageViewModel : INotifyPropertyChanged
{
    private const string TAG = nameof(HomePageViewModel);

    public event PropertyChangedEventHandler? PropertyChanged;

    public MutableLiveData<FilterModel> FilterLiveData = new();
    public MutableLiveData<bool> GroupingEnabledLiveData = new();
    public MutableLiveData<ComicFilterModel.ViewTypeEnum> ViewTypeLiveData = new();

    public ObservableCollection<ComicItemViewModel> UngroupedComicItems { get; set; } = [];
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

    private bool _isCommandBarMarkAsReadingEnabled = false;
    public bool IsCommandBarMarkAsReadingEnabled
    {
        get => _isCommandBarMarkAsReadingEnabled;
        set
        {
            _isCommandBarMarkAsReadingEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarMarkAsReadingEnabled)));
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
    public void SelectSortOrGroup(SortByUIModel? model)
    {
        if (model == null)
        {
            return;
        }

        _sharedDispatcher.Submit("SelectSortOrGroup", delegate
        {
            bool modified = false;
            ComicFilterModel.ExternalFilterModel lastFilter = EnsureLastFilterNoLock();
            if (model.IsSortBy)
            {
                switch (model.Type)
                {
                    case SortByMenuItemTypeEnum.Property:
                        if (model.Property != null && !model.Property.Equals(lastFilter.SortBy))
                        {
                            modified = true;
                            lastFilter.SortBy = model.Property;
                        }
                        break;
                    case SortByMenuItemTypeEnum.Ascending:
                        if (!lastFilter.SortByAscending)
                        {
                            modified = true;
                            lastFilter.SortByAscending = true;
                        }
                        break;
                    case SortByMenuItemTypeEnum.Descending:
                        if (lastFilter.SortByAscending)
                        {
                            modified = true;
                            lastFilter.SortByAscending = false;
                        }
                        break;
                    case SortByMenuItemTypeEnum.Function:
                        break;
                    default:
                        break;
                }
            }
            else
            {
                switch (model.Type)
                {
                    case SortByMenuItemTypeEnum.Property:
                        if ((model.Property != null && !model.Property.Equals(lastFilter.GroupBy)) ||
                            (lastFilter.GroupBy != null && !lastFilter.GroupBy.Equals(model.Property)))
                        {
                            modified = true;
                            lastFilter.GroupBy = model.Property;
                        }
                        break;
                    case SortByMenuItemTypeEnum.Ascending:
                        if (!lastFilter.GroupByAscending)
                        {
                            modified = true;
                            lastFilter.GroupByAscending = true;
                        }
                        break;
                    case SortByMenuItemTypeEnum.Descending:
                        if (lastFilter.GroupByAscending)
                        {
                            modified = true;
                            lastFilter.GroupByAscending = false;
                        }
                        break;
                    case SortByMenuItemTypeEnum.Function:
                        if (lastFilter.GroupSortingFunction != model.FunctionType)
                        {
                            modified = true;
                            lastFilter.GroupSortingFunction = model.FunctionType;
                        }
                        if (model.Property != null && !model.Property.Equals(lastFilter.GroupSortingProperty))
                        {
                            modified = true;
                            lastFilter.GroupSortingProperty = model.Property;
                        }
                        break;
                    default:
                        break;
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
            _selectedComicItems.Clear();
            UpdateCommandBarButtonsNoLock();
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
        _selectedComicItems.Clear();
        _selectedComicItems.AddRange(items);
        UpdateCommandBarButtonsNoLock();
    }

    /// <summary>
    /// Gets the selected comic items according to the triggering comic item.
    /// </summary>
    /// <param name="triggerItem">The triggering comic item.</param>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    public List<ComicItemViewModel> GetSelection(ComicItemViewModel triggerItem)
    {
        List<ComicItemViewModel> selection = [];
        if (_isSelectMode)
        {
            bool contained = false;
            foreach (ComicItemViewModel item in _selectedComicItems)
            {
                if (triggerItem.Comic == item.Comic)
                {
                    contained = true;
                    break;
                }
            }
            if (contained)
            {
                selection.AddRange(_selectedComicItems);
            }
            else
            {
                selection.Add(triggerItem);
            }
        }
        else
        {
            selection.Add(triggerItem);
        }
        return selection;
    }

    /// <summary>
    /// Applies an operation to a specific comic item.
    /// </summary>
    /// <param name="operationType">The operation type.</param>
    /// <param name="comic">The comic item to operate.</param>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    public void ApplyOperationToComic(ComicOperationType operationType, ComicItemViewModel comic)
    {
        List<ComicItemViewModel> selection = GetSelection(comic);
        _sharedDispatcher.Submit("ApplyOperationToSelection", delegate
        {
            BatchApplyOperation(operationType, selection);
            MainThreadUtils.RunInMainThread(() =>
            {
                UpdateCommandBarButtonsNoLock();
            });
        });
    }

    /// <summary>
    /// Applies a batch operation to the selected comic items.
    /// </summary>
    /// <param name="operationType">The operation type.</param>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    public void ApplyOperationToSelection(ComicOperationType operationType)
    {
        List<ComicItemViewModel> selectedItems = [.. _selectedComicItems];
        _sharedDispatcher.Submit("ApplyOperationToSelection", delegate
        {
            BatchApplyOperation(operationType, selectedItems);
            MainThreadUtils.RunInMainThread(() =>
            {
                UpdateCommandBarButtonsNoLock();
            });
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
                var model = new ComicItemViewModel(item);
                model.UpdateProgress(true);
                _comicItems.Add(model);
            }
            ScheduleUpdateComics();
        });
    }

    private void BatchApplyOperation(ComicOperationType operationType, List<ComicItemViewModel> models)
    {
        switch (operationType)
        {
            case ComicOperationType.Favorite:
                {
                    List<ComicItemViewModel> items = models.FindAll(x => !x.IsFavorite);
                    FavoriteModel.Instance.BatchAdd(items.ConvertAll(x => new FavoriteModel.FavoriteItem
                    {
                        Id = x.Comic.Id,
                        Title = x.Comic.Title,
                    }));
                    ModifyExistingItems(items, (item) => { item.IsFavorite = true; });
                }
                break;
            case ComicOperationType.Unfavorite:
                {
                    List<ComicItemViewModel> items = models.FindAll(x => x.IsFavorite);
                    FavoriteModel.Instance.BatchRemoveWithId(items.ConvertAll(x => x.Comic.Id));
                    ModifyExistingItems(items, (item) => { item.IsFavorite = false; });
                }
                break;
            case ComicOperationType.Hide:
                {
                    List<ComicItemViewModel> items = models.FindAll(x => !x.IsHide);
                    foreach (ComicItemViewModel item in items)
                    {
                        item.Comic.SaveHiddenAsync(true).Wait();
                    }
                    _searchEngine.Update();
                }
                break;
            case ComicOperationType.Unhide:
                {
                    List<ComicItemViewModel> items = models.FindAll(x => x.IsHide);
                    foreach (ComicItemViewModel item in items)
                    {
                        item.Comic.SaveHiddenAsync(false).Wait();
                    }
                    _searchEngine.Update();
                }
                break;
            case ComicOperationType.MarkAsRead:
                {
                    List<ComicItemViewModel> items = models.FindAll(x => !x.IsRead);
                    foreach (ComicItemViewModel item in items)
                    {
                        item.Comic.SetCompletionStateToCompleted().Wait();
                    }
                    ModifyExistingItems(items, (item) =>
                    {
                        item.CompletionState = ComicCompletionStatusEnum.Completed;
                        item.UpdateProgress(true);
                    });
                    ScheduleUpdateComics();
                }
                break;
            case ComicOperationType.MarkAsReading:
                {
                    List<ComicItemViewModel> items = models.FindAll(x => !x.IsReading);
                    foreach (ComicItemViewModel item in items)
                    {
                        item.Comic.SetCompletionStateToStarted().Wait();
                    }
                    ModifyExistingItems(items, (item) =>
                    {
                        item.CompletionState = ComicCompletionStatusEnum.Started;
                        item.UpdateProgress(true);
                    });
                    ScheduleUpdateComics();
                }
                break;
            case ComicOperationType.MarkAsUnread:
                {
                    List<ComicItemViewModel> items = models.FindAll(x => !x.IsUnread);
                    foreach (ComicItemViewModel item in items)
                    {
                        item.Comic.SetCompletionStateToNotStarted().Wait();
                    }
                    ModifyExistingItems(items, (item) =>
                    {
                        item.CompletionState = ComicCompletionStatusEnum.NotStarted;
                        item.UpdateProgress(true);
                    });
                    ScheduleUpdateComics();
                }
                break;
            default:
                break;
        }
    }

    private void ModifyExistingItems(IEnumerable<ComicItemViewModel> items, Action<ComicItemViewModel> action)
    {
        Dictionary<ComicModel, ComicItemViewModel> changedItems = [];
        foreach (ComicItemViewModel item in items)
        {
            ComicItemViewModel newItem = new(item);
            action(newItem);
            changedItems[item.Comic] = newItem;
        }
        ModifyExistingList(_comicItems, changedItems);
        _ = MainThreadUtils.RunInMainThread(delegate
        {
            ModifyExistingList(_selectedComicItems, changedItems);
            bool isGrouped = GroupingEnabledLiveData.GetValue();
            if (isGrouped)
            {
                foreach (ComicGroupViewModel group in GroupedComicItems)
                {
                    group.UpdateItems((oldItem) =>
                    {
                        if (changedItems.TryGetValue(oldItem.Comic, out ComicItemViewModel? newItem))
                        {
                            return newItem;
                        }
                        return null;
                    });
                }
            }
            else
            {
                ModifyExistingList(UngroupedComicItems, changedItems);
            }
        });
    }

    private void ModifyExistingList(IList<ComicItemViewModel> list, IReadOnlyDictionary<ComicModel, ComicItemViewModel> changedItems)
    {
        for (int i = 0; i < list.Count; i++)
        {
            ComicItemViewModel oldItem = list[i];
            if (changedItems.TryGetValue(oldItem.Comic, out ComicItemViewModel? newItem))
            {
                list[i] = newItem;
            }
        }
    }

    private void UpdateCommandBarButtonsNoLock()
    {
        bool allSelected = _selectedComicItems.Count == _comicItems.Count;
        bool favoriteEnabled = false;
        bool unfavoriteEnabled = false;
        bool hideEnabled = false;
        bool unhideEnabled = false;
        bool markAsReadEnabled = false;
        bool markAsReadingEnabled = false;
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

            if (!item.IsReading)
            {
                markAsReadingEnabled = true;
            }

            if (!item.IsUnread)
            {
                markAsUnreadEnabled = true;
            }
        }

        IsCommandBarSelectAllToggled = allSelected;
        IsCommandBarFavoriteEnabled = favoriteEnabled;
        IsCommandBarUnFavoriteEnabled = unfavoriteEnabled;
        IsCommandBarHideEnabled = hideEnabled;
        IsCommandBarUnHideEnabled = unhideEnabled;
        IsCommandBarMarkAsReadEnabled = markAsReadEnabled;
        IsCommandBarMarkAsReadingEnabled = markAsReadingEnabled;
        IsCommandBarMarkAsUnreadEnabled = markAsUnreadEnabled;
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
            _filterModel = ComicFilterModel.Instance.GetModel() ?? new();
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
        ComicFilterModel.Instance.UpdateModel(_filterModel);

        // Update expression
        _searchEngine.SetFilterExpresssion(lastFilter.Expression);

        // Update UI
        var viewTypeDropDown = new DropDownButtonModel<ComicFilterModel.ViewTypeEnum>
        {
            Name = StringResourceProvider.Instance.ViewType,
            Items = _viewTypes.ConvertAll(x => CreateToggleMenuFlyoutItem(ViewTypeToDisplayName(x), x == lastFilter.ViewType, x)),
        };

        List<ComicPropertyModel> properties = await ComicPropertyModel.GetProperties();
        var sortByDropDown = new MenuFlyoutItemModel<SortByUIModel>
        {
            Name = StringResourceProvider.Instance.Sort,
            SubItems = CreateSortByMenuItems(properties, lastFilter.SortBy, lastFilter.SortByAscending),
        };
        var groupByDropDown = new MenuFlyoutItemModel<SortByUIModel>
        {
            Name = StringResourceProvider.Instance.Group,
            SubItems = CreateGroupByMenuItems(properties, lastFilter.GroupBy, lastFilter.GroupByAscending, lastFilter.GroupSortingFunction, lastFilter.GroupSortingProperty),
        };
        var sortAndGroupDropDown = new DropDownButtonModel<SortByUIModel>
        {
            Name = StringResourceProvider.Instance.Sort + " & " + StringResourceProvider.Instance.Group,
            Items = [sortByDropDown, groupByDropDown],
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
            SortAndGroupDropDown = sortAndGroupDropDown,
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
            ComicPropertyModel? groupBy = filter.GroupBy;

            if (groupBy != null)
            {
                List<ComicPropertyModel.GroupItem<ComicItemViewModel>> groupItems = groupBy.GroupComics(comicItems, (x) => x.Comic,
                    filter.GroupByAscending, filter.GroupSortingFunction, filter.GroupSortingProperty);
                comicsGrouped = [];
                foreach (ComicPropertyModel.GroupItem<ComicItemViewModel> item in groupItems)
                {
                    List<ComicItemViewModel> sorted = SortComicItemsByProerty(item.Items, sortBy, filter.SortByAscending);
                    var group = new ComicGroupViewModel(item.Name, sorted, false)
                    {
                        Description = item.Description,
                    };
                    comicsGrouped.Add(group);
                }
            }
            else
            {
                comicsUngrouped = SortComicItemsByProerty(comicItems, sortBy, filter.SortByAscending);
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
                DiffUtils.UpdateCollection(GroupedComicItems, comicsGrouped, (ComicGroupViewModel x, ComicGroupViewModel y) => x.GroupName == y.GroupName && x.Description == y.Description);
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
        return property.SortComics(items, (x) => x.Comic, ascending);
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
            Name = StringResourceProvider.Instance.Default,
            ViewType = ComicFilterModel.ViewTypeEnum.Large,
            SortBy = new(),
            SortByAscending = true,
            GroupBy = null,
            GroupByAscending = true,
            Expression = "",
        };
    }

    private List<MenuFlyoutItemModel<SortByUIModel>> CreateSortByMenuItems(List<ComicPropertyModel> properties, ComicPropertyModel? selectedProperty, bool ascending)
    {
        List<MenuFlyoutItemModel<SortByUIModel>> items = [];
        items.AddRange(CreateSortByPropertyMenuItems(properties, selectedProperty, new SortByUIModel
        {
            Type = SortByMenuItemTypeEnum.Property,
            IsSortBy = true,
        }));
        if (selectedProperty != null)
        {
            items.Add(CreateSeperatorMenuFlyoutItem<SortByUIModel>());
            items.Add(CreateToggleMenuFlyoutItem(StringResourceProvider.Instance.Ascending, ascending, new SortByUIModel
            {
                Type = SortByMenuItemTypeEnum.Ascending,
                IsSortBy = true,
            }));
            items.Add(CreateToggleMenuFlyoutItem(StringResourceProvider.Instance.Descending, !ascending, new SortByUIModel
            {
                Type = SortByMenuItemTypeEnum.Descending,
                IsSortBy = true,
            }));
        }
        return items;
    }

    private List<MenuFlyoutItemModel<SortByUIModel>> CreateGroupByMenuItems(List<ComicPropertyModel> properties,
        ComicPropertyModel? selectedProperty, bool ascending, ComicFilterModel.FunctionTypeEnum sortingFunction, ComicPropertyModel? sortingProperty)
    {
        List<MenuFlyoutItemModel<SortByUIModel>> items = [];
        items.Add(CreateToggleMenuFlyoutItem(StringResourceProvider.Instance.None, selectedProperty is null, new SortByUIModel
        {
            Type = SortByMenuItemTypeEnum.Property,
            IsSortBy = false,
        }));
        items.AddRange(CreateSortByPropertyMenuItems(properties, selectedProperty, new SortByUIModel
        {
            Type = SortByMenuItemTypeEnum.Property,
            IsSortBy = false,
        }));
        if (selectedProperty != null)
        {
            items.Add(CreateSeperatorMenuFlyoutItem<SortByUIModel>());
            items.Add(CreateToggleMenuFlyoutItem(StringResourceProvider.Instance.Ascending, ascending, new SortByUIModel
            {
                Type = SortByMenuItemTypeEnum.Ascending,
                IsSortBy = false,
            }));
            items.Add(CreateToggleMenuFlyoutItem(StringResourceProvider.Instance.Descending, !ascending, new SortByUIModel
            {
                Type = SortByMenuItemTypeEnum.Descending,
                IsSortBy = false,
            }));
            items.Add(new MenuFlyoutItemModel<SortByUIModel>
            {
                Name = StringResourceProvider.Instance.SortingFunction,
                SubItems = CreateSortingFunctionMenuItems(properties, sortingFunction, sortingProperty, new SortByUIModel
                {
                    Type = SortByMenuItemTypeEnum.Function,
                    IsSortBy = false,
                }),
            });
        }
        return items;
    }

    private List<MenuFlyoutItemModel<SortByUIModel>> CreateSortingFunctionMenuItems(List<ComicPropertyModel> properties,
        ComicFilterModel.FunctionTypeEnum sortingFunction, ComicPropertyModel? sortingProperty, SortByUIModel templateModel)
    {
        string GetFunctionDisplayName(ComicFilterModel.FunctionTypeEnum function)
        {
            return function switch
            {
                ComicFilterModel.FunctionTypeEnum.None => StringResourceProvider.Instance.None,
                ComicFilterModel.FunctionTypeEnum.ItemCount => StringResourceProvider.Instance.FunctionItemCount,
                ComicFilterModel.FunctionTypeEnum.Average => StringResourceProvider.Instance.FunctionAverage,
                ComicFilterModel.FunctionTypeEnum.Sum => StringResourceProvider.Instance.FunctionSum,
                ComicFilterModel.FunctionTypeEnum.Max => StringResourceProvider.Instance.FunctionMax,
                ComicFilterModel.FunctionTypeEnum.Min => StringResourceProvider.Instance.FunctionMin,
                _ => "Unknown function"
            };
        }

        List<MenuFlyoutItemModel<SortByUIModel>> items = [];
        List<ComicFilterModel.FunctionTypeEnum> simpleFunctions = [
            ComicFilterModel.FunctionTypeEnum.None,
            ComicFilterModel.FunctionTypeEnum.ItemCount,
        ];
        List<ComicFilterModel.FunctionTypeEnum> propertyFunctions = [
            ComicFilterModel.FunctionTypeEnum.Average,
            ComicFilterModel.FunctionTypeEnum.Sum,
            ComicFilterModel.FunctionTypeEnum.Max,
            ComicFilterModel.FunctionTypeEnum.Min,
        ];
        foreach (ComicFilterModel.FunctionTypeEnum function in simpleFunctions)
        {
            items.Add(CreateToggleMenuFlyoutItem(GetFunctionDisplayName(function), function == sortingFunction, new SortByUIModel(templateModel)
            {
                FunctionType = function,
            }));
        }
        foreach (ComicFilterModel.FunctionTypeEnum function in propertyFunctions)
        {
            List<MenuFlyoutItemModel<SortByUIModel>> subItems = CreateSortByPropertyMenuItems(properties, function == sortingFunction ? sortingProperty : null, new SortByUIModel(templateModel)
            {
                FunctionType = function,
            });
            items.Add(new MenuFlyoutItemModel<SortByUIModel>
            {
                Name = GetFunctionDisplayName(function),
                SubItems = subItems,
            });
        }
        return items;
    }

    private List<MenuFlyoutItemModel<SortByUIModel>> CreateSortByPropertyMenuItems(List<ComicPropertyModel> properties,
        ComicPropertyModel? selectedProperty, SortByUIModel templateModel)
    {
        Dictionary<string, List<ComicPropertyModel>> propertyGroupMap = [];
        foreach (ComicPropertyModel property in properties)
        {
            string groupName = property.DisplayGroupName;
            if (!propertyGroupMap.TryGetValue(groupName, out List<ComicPropertyModel>? value))
            {
                value = [];
                propertyGroupMap[groupName] = value;
            }
            value.Add(property);
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
                items.Add(CreateToggleMenuFlyoutItem(p.DisplayName, p.Equals(selectedProperty), new SortByUIModel(templateModel)
                {
                    Property = p,
                }));
            }
        }
        foreach (KeyValuePair<string, List<ComicPropertyModel>> kvp in propertyGroupList)
        {
            List<MenuFlyoutItemModel<SortByUIModel>> subItems = [];
            foreach (ComicPropertyModel p in kvp.Value)
            {
                subItems.Add(CreateToggleMenuFlyoutItem(p.DisplayName, p.Equals(selectedProperty), new SortByUIModel(templateModel)
                {
                    Property = p,
                }));
            }
            items.Add(new MenuFlyoutItemModel<SortByUIModel>
            {
                Name = kvp.Key,
                SubItems = subItems,
            });
        }
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
            ComicFilterModel.ViewTypeEnum.Large => StringResourceProvider.Instance.ViewTypeLarge,
            ComicFilterModel.ViewTypeEnum.Medium => StringResourceProvider.Instance.ViewTypeMedium,
            _ => "Unknown"
        };
    }

    public class FilterModel
    {
        public DropDownButtonModel<ComicFilterModel.ViewTypeEnum> ViewTypeDropDown { get; set; } = new();
        public DropDownButtonModel<SortByUIModel> SortAndGroupDropDown { get; set; } = new();
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
        public SortByUIModel() { }

        public SortByUIModel(SortByUIModel source)
        {
            IsSortBy = source.IsSortBy;
            Type = source.Type;
            FunctionType = source.FunctionType;
            Property = source.Property;
        }

        public bool IsSortBy { get; set; }
        public SortByMenuItemTypeEnum Type { get; set; }
        public ComicFilterModel.FunctionTypeEnum FunctionType { get; set; } = ComicFilterModel.FunctionTypeEnum.None;
        public ComicPropertyModel? Property { get; set; }
    }

    public enum SortByMenuItemTypeEnum
    {
        Property,
        Ascending,
        Descending,
        Function,
    }
}
