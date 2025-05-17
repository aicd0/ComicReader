// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;

using ComicReader.Common.Lifecycle;
using ComicReader.Data;

namespace ComicReader.Views.Home;

internal class HomePageViewModel
{
    public MutableLiveData<FilterModel> FilterLiveData = new();

    private ComicFilterModel.ExternalModel _filterModel = new();

    private readonly List<ComicFilterModel.ViewTypeEnum> _viewTypes = [
        ComicFilterModel.ViewTypeEnum.Large,
        ComicFilterModel.ViewTypeEnum.Medium,
    ];

    private readonly List<ComicFilterModel.PropertyTypeEnum> _properties = [
        ComicFilterModel.PropertyTypeEnum.Title,
        ComicFilterModel.PropertyTypeEnum.Progress,
    ];

    public void Initialize()
    {
        _ = InitializeInternal();
    }

    public void UpdateFilters()
    {
        _ = ReloadFilters();
    }

    public ComicFilterModel.ExternalFilterModel GetFilter()
    {
        return _filterModel?.LastFilter ?? new();
    }

    public void SelectViewType(ComicFilterModel.ViewTypeEnum viewType)
    {
        bool modified = false;

        ComicFilterModel.ExternalFilterModel lastFilter = EnsureLastFilter();
        if (lastFilter.ViewType != viewType)
        {
            modified = true;
            lastFilter.ViewType = viewType;
        }

        if (modified)
        {
            _filterModel.LastFilterModified = true;
        }
        UpdateFiltersInternal();
    }

    public void SelectSortBy(SortByUIModel? model)
    {
        if (model == null)
        {
            return;
        }

        bool modified = false;

        ComicFilterModel.ExternalFilterModel lastFilter = EnsureLastFilter();
        if (model.IsProperty)
        {
            if (lastFilter.SortBy != model.Property)
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

        UpdateFiltersInternal();
    }

    public void SelectGroupBy(ComicFilterModel.ExternalPropertyModel? model)
    {
        if (model == null)
        {
            return;
        }

        ComicFilterModel.ExternalFilterModel lastFilter = EnsureLastFilter();
        if (lastFilter.GroupBy != model)
        {
            lastFilter.GroupBy = model;
        }
        else
        {
            lastFilter.GroupBy = null;
        }

        _filterModel.LastFilterModified = true;
        UpdateFiltersInternal();
    }

    public void SelectFilterPreset(string? name)
    {
        name ??= "";
        ComicFilterModel.ExternalFilterModel? filter = _filterModel.Filters.Find(x => x.Name == name);
        if (filter == null)
        {
            return;
        }
        _filterModel.LastFilter = filter.Clone();

        _filterModel.LastFilterModified = false;
        UpdateFiltersInternal();
    }

    private async Task InitializeInternal()
    {
        await ReloadFilters();
    }

    private async Task ReloadFilters()
    {
        _filterModel = await ComicFilterModel.Instance.GetModel() ?? new();
        UpdateFiltersInternal();
    }

    private void UpdateFiltersInternal()
    {
        // Validate and save

        List<ComicFilterModel.ExternalFilterModel> filters = _filterModel.Filters;
        if (filters == null || filters.Count == 0)
        {
            filters = [CreateDefaultFilter()];
            _filterModel.Filters = filters;
        }

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

        List<MenuFlyoutItemModel<SortByUIModel>> sortByItems = GetProperties().ConvertAll(x => CreateToggleMenuFlyoutItem(PropertyToDisplayName(x), x.Type == sortBy.Type, new SortByUIModel
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
            Items = GetProperties().ConvertAll(x => CreateToggleMenuFlyoutItem(PropertyToDisplayName(x), groupBy == null ? false : x.Type == groupBy.Type, x)),
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
    }

    private ComicFilterModel.ExternalFilterModel EnsureLastFilter()
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
