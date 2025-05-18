// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.DebugTools;
using ComicReader.Common.Lifecycle;
using ComicReader.Common.Threading;
using ComicReader.Data;
using ComicReader.Data.Comic;
using ComicReader.Data.SqlHelpers;
using ComicReader.ViewModels;

namespace ComicReader.Views.Home;

internal class HomePageViewModel : INotifyPropertyChanged
{
    private const string TAG = nameof(HomePageViewModel);

    public event PropertyChangedEventHandler? PropertyChanged;

    public MutableLiveData<FilterModel> FilterLiveData = new();
    public MutableLiveData<bool> GroupingEnabledLiveData = new();

    public ObservableCollectionPlus<ComicItemViewModel> UngroupedComicItems { get; set; } = [];
    public ObservableCollection<SimpleGroupViewModel<ComicItemViewModel>> GroupedComicItems { get; set; } = [];

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

    private ComicFilterModel.ExternalModel _filterModel = new();
    private readonly List<ComicItemViewModel> _comicItems = [];

    private readonly ITaskDispatcher _sharedDispatcher = TaskDispatcher.DefaultQueue;
    private int _updateLibrarySubmitted = 0;
    private int _updateFilterSubmitted = 0;
    private int _updateComicSubmitted = 0;

    private readonly List<ComicFilterModel.ViewTypeEnum> _viewTypes = [
        ComicFilterModel.ViewTypeEnum.Large,
        ComicFilterModel.ViewTypeEnum.Medium,
    ];

    private readonly List<ComicFilterModel.PropertyTypeEnum> _properties = [
        ComicFilterModel.PropertyTypeEnum.Title,
        ComicFilterModel.PropertyTypeEnum.Progress,
    ];

    /// <summary>
    /// Initializes the view model.
    /// </summary>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    public void Initialize()
    {
        ScheduleUpdateFilters(true);
        ScheduleUpdateLibrary();
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
                ScheduleUpdateFilters(false);
            }
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
                ScheduleUpdateFilters(false);
            }
        });
    }

    /// <summary>
    /// Selects the property to group comics by.
    /// </summary>
    /// <param name="model">The property model to group by.</param>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    public void SelectGroupBy(ComicFilterModel.ExternalPropertyModel? model)
    {
        if (model == null)
        {
            return;
        }

        _sharedDispatcher.Submit("SelectGroupBy", delegate
        {
            ComicFilterModel.ExternalFilterModel lastFilter = EnsureLastFilterNoLock();
            if (model.Equals(lastFilter.GroupBy))
            {
                lastFilter.GroupBy = null;
            }
            else
            {
                lastFilter.GroupBy = model;
            }
            _filterModel.LastFilterModified = true;
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
    /// Updates the comic library and refreshes the displayed items.
    /// </summary>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    public void UpdateLibrary()
    {
        ScheduleUpdateLibrary();
    }

    private void ScheduleUpdateLibrary()
    {
        if (Interlocked.CompareExchange(ref _updateLibrarySubmitted, 1, 0) == 1)
        {
            return;
        }
        _sharedDispatcher.Submit("UpdateLibrary", delegate
        {
            Interlocked.Exchange(ref _updateLibrarySubmitted, 0);
            UpdateLibraryNoLock().Wait();
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

    private async Task UpdateLibraryNoLock()
    {
        Logger.I(TAG, "UpdateLibraryNoLock");

        List<Tuple<long, DateTimeOffset>> records = [];
        await ComicData.EnqueueCommand(delegate
        {
            var command = new SelectCommand<ComicTable>(ComicTable.Instance);
            SelectCommand<ComicTable>.IToken<long> idToken = command.PutQueryInt64(ComicTable.ColumnId);
            SelectCommand<ComicTable>.IToken<DateTimeOffset> lastVisitToken = command.PutQueryDateTimeOffset(ComicTable.ColumnLastVisit);
            using SelectCommand<ComicTable>.IReader reader = command.AppendCondition(ComicTable.ColumnHidden, false).Execute();

            while (reader.Read())
            {
                records.Add(new Tuple<long, DateTimeOffset>
                (
                    idToken.GetValue(),
                    lastVisitToken.GetValue()
                ));
            }
        }, "HomeLoadLibrary");

        var comicItems = new List<ComicItemViewModel>();
        foreach (Tuple<long, DateTimeOffset> record in records)
        {
            ComicData comic = await ComicData.FromId(record.Item1, "HomeLoadComic");
            if (comic == null)
            {
                continue;
            }
            var model = new ComicItemViewModel();
            model.Update(comic);
            comicItems.Add(model);
        }

        _comicItems.Clear();
        _comicItems.AddRange(comicItems);

        ScheduleUpdateComics();
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

        ComicFilterModel.ExternalPropertyModel sortBy = lastFilter.SortBy;
        sortBy ??= new()
        {
            Type = ComicFilterModel.PropertyTypeEnum.Title,
        };
        lastFilter.SortBy = sortBy;

        ComicFilterModel.ExternalPropertyModel? groupBy = lastFilter.GroupBy;
        _ = ComicFilterModel.Instance.UpdateModel(_filterModel);

        // Update UI
        var viewTypeDropDown = new DropDownButtonModel<ComicFilterModel.ViewTypeEnum>
        {
            Name = ViewTypeToDisplayName(lastFilter.ViewType),
            Items = _viewTypes.ConvertAll(x => CreateMenuFlyoutItem(ViewTypeToDisplayName(x), x)),
        };

        List<ComicFilterModel.ExternalPropertyModel> properties = GetProperties();
        List<MenuFlyoutItemModel<SortByUIModel>> sortByItems = properties.ConvertAll(x => CreateToggleMenuFlyoutItem(PropertyToDisplayName(x), x.Type == sortBy.Type, new SortByUIModel
        {
            IsProperty = true,
            IsAscending = lastFilter.SortByAscending,
            Property = x,
        }));
        sortByItems.Add(CreateSeperatorMenuFlyoutItem<SortByUIModel>());
        sortByItems.Add(CreateToggleMenuFlyoutItem("Ascending", lastFilter.SortByAscending, new SortByUIModel
        {
            IsProperty = false,
            IsAscending = true,
        }));
        sortByItems.Add(CreateToggleMenuFlyoutItem("Descending", !lastFilter.SortByAscending, new SortByUIModel
        {
            IsProperty = false,
            IsAscending = false,
        }));
        var sortByDropDown = new DropDownButtonModel<SortByUIModel>
        {
            Name = "Sort by",
            Items = sortByItems,
        };

        var groupByDropDown = new DropDownButtonModel<ComicFilterModel.ExternalPropertyModel>
        {
            Name = "Group by",
            Items = properties.ConvertAll(x => CreateToggleMenuFlyoutItem(PropertyToDisplayName(x), groupBy == null ? false : x.Type == groupBy.Type, x)),
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

        ScheduleUpdateComics();
    }

    private async Task UpdateComicsNoLock()
    {
        Logger.I(TAG, "UpdateComicsNoLock");

        List<ComicItemViewModel> comics = [];
        comics.AddRange(_comicItems);
        await MainThreadUtils.RunInMainThread(delegate
        {
            _comicItems.Clear();
            _comicItems.AddRange(comics);
            LibraryEmptyVisible = comics.Count == 0;
            GroupingEnabledLiveData.Emit(false);

            C1<ComicItemViewModel>.UpdateCollection(UngroupedComicItems, comics,
                (ComicItemViewModel x, ComicItemViewModel y) =>
                x.Comic.Title == y.Comic.Title &&
                x.Rating == y.Rating &&
                x.Progress == y.Progress &&
                x.IsFavorite == y.IsFavorite);
        });
    }

    private ComicFilterModel.ExternalFilterModel EnsureLastFilterNoLock()
    {
        ComicFilterModel.ExternalFilterModel filter = _filterModel.LastFilter ?? CreateDefaultFilter();
        _filterModel.LastFilter = filter;
        return filter;
    }

    private List<ComicFilterModel.ExternalPropertyModel> GetProperties()
    {
        var properties = new List<ComicFilterModel.ExternalPropertyModel>();
        foreach (ComicFilterModel.PropertyTypeEnum propertyType in _properties)
        {
            properties.Add(new ComicFilterModel.ExternalPropertyModel
            {
                Type = propertyType,
            });
        }
        return properties;
    }

    private ComicFilterModel.ExternalFilterModel CreateDefaultFilter()
    {
        return new ComicFilterModel.ExternalFilterModel
        {
            Name = "Default",
            ViewType = ComicFilterModel.ViewTypeEnum.Large,
            SortBy = new ComicFilterModel.ExternalPropertyModel
            {
                Type = ComicFilterModel.PropertyTypeEnum.Title,
            },
            SortByAscending = true,
            GroupBy = null,
            Expression = "TRUE",
        };
    }

    private MenuFlyoutItemModel<T> CreateMenuFlyoutItem<T>(string name, T dataContext)
    {
        return new MenuFlyoutItemModel<T>
        {
            IsSeperator = false,
            Name = name,
            CanToggle = false,
            Toggled = false,
            DataContext = dataContext
        };
    }

    private MenuFlyoutItemModel<T> CreateToggleMenuFlyoutItem<T>(string name, bool toggled, T dataContext)
    {
        return new MenuFlyoutItemModel<T>
        {
            IsSeperator = false,
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
            Name = "",
            CanToggle = false,
            Toggled = false,
            DataContext = default
        };
    }

    private string PropertyToDisplayName(ComicFilterModel.ExternalPropertyModel property)
    {
        return property.Type switch
        {
            ComicFilterModel.PropertyTypeEnum.Title => "Title",
            ComicFilterModel.PropertyTypeEnum.Progress => "Progress",
            ComicFilterModel.PropertyTypeEnum.Tag => $"Tag: {property.Name}",
            _ => "Unknown"
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
        public DropDownButtonModel<ComicFilterModel.ExternalPropertyModel> GroupByDropDown { get; set; } = new();
        public DropDownButtonModel<string> FilterPresetDropDown { get; set; } = new();
    }

    public class DropDownButtonModel<T>
    {
        public string Name { get; set; } = "";
        public List<MenuFlyoutItemModel<T>> Items { get; set; } = [];
    }

    public class MenuFlyoutItemModel<T>
    {
        public bool IsSeperator { get; set; }
        public string Name { get; set; } = "";
        public bool CanToggle { get; set; }
        public bool Toggled { get; set; }
        public T? DataContext { get; set; }
    }

    public class SortByUIModel
    {
        public bool IsProperty { get; set; }
        public bool IsAscending { get; set; }
        public ComicFilterModel.ExternalPropertyModel Property { get; set; } = new();
    }
}
