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

    private ComicFilterModel.ExternalModel _filterModel;

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
        return _filterModel?.LastFilter;
    }

    public void SelectViewType(ComicFilterModel.ViewTypeEnum viewType)
    {
        bool modified = false;

        if (_filterModel.LastFilter.ViewType != viewType)
        {
            modified = true;
            _filterModel.LastFilter.ViewType = viewType;
        }

        if (modified)
        {
            _filterModel.LastFilterModified = true;
        }
        UpdateFiltersInternal();
    }

    public void SelectSortBy(SortByUIModel model)
    {
        bool modified = false;

        if (model.IsProperty)
        {
            if (_filterModel.LastFilter.SortBy.Type != model.Property)
            {
                modified = true;
                _filterModel.LastFilter.SortBy.Type = model.Property;
            }
        }
        else
        {
            if (_filterModel.LastFilter.SortByAscending != model.IsAscending)
            {
                modified = true;
                _filterModel.LastFilter.SortByAscending = model.IsAscending;
            }
        }

        if (modified)
        {
            _filterModel.LastFilterModified = true;
        }
        UpdateFiltersInternal();
    }

    public void SelectGroupBy(ComicFilterModel.PropertyTypeEnum propertyType)
    {
        if (_filterModel.LastFilter.GroupBy == null)
        {
            _filterModel.LastFilter.GroupBy = new ComicFilterModel.ExternalPropertyModel
            {
                Type = propertyType,
            };
        }
        else
        {
            if (_filterModel.LastFilter.GroupBy.Type != propertyType)
            {
                _filterModel.LastFilter.GroupBy.Type = propertyType;
            }
            else
            {
                _filterModel.LastFilter.GroupBy = null;
            }
        }

        _filterModel.LastFilterModified = true;
        UpdateFiltersInternal();
    }

    public void SelectFilterPreset(string name)
    {
        ComicFilterModel.ExternalFilterModel filter = _filterModel.Filters.Find(x => x.Name == name);
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
        _filterModel = await ComicFilterModel.Instance.GetModel();
        UpdateFiltersInternal();
    }

    private void UpdateFiltersInternal()
    {
        ComicFilterModel.ExternalModel dataModel = _filterModel;
        dataModel ??= new();
        _filterModel = dataModel;

        List<ComicFilterModel.ExternalFilterModel> filters = dataModel.Filters;
        if (filters == null || filters.Count == 0)
        {
            filters = [CreateDefaultFilter()];
            dataModel.Filters = filters;
        }

        ComicFilterModel.ExternalFilterModel lastFilter = dataModel.LastFilter;
        lastFilter ??= filters[0].Clone();
        dataModel.LastFilter = lastFilter;

        ComicFilterModel.ExternalPropertyModel sortBy = lastFilter.SortBy;
        sortBy ??= new()
        {
            Type = ComicFilterModel.PropertyTypeEnum.Title,
        };
        lastFilter.SortBy = sortBy;

        ComicFilterModel.ExternalPropertyModel groupBy = lastFilter.GroupBy;
        _ = ComicFilterModel.Instance.UpdateModel(_filterModel);

        var viewTypeDropDown = new DropDownButtonModel<ComicFilterModel.ViewTypeEnum>
        {
            Name = ViewTypeToDisplayName(lastFilter.ViewType),
            Items = _viewTypes.ConvertAll(x => CreateMenuFlyoutItem(ViewTypeToDisplayName(x), x)),
        };

        List<MenuFlyoutItemModel<SortByUIModel>> sortByItems = _properties.ConvertAll(x => CreateToggleMenuFlyoutItem(PropertyTypeToDisplayName(x), x == sortBy.Type, new SortByUIModel
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
            Property = sortBy.Type,
        }));
        var sortByDropDown = new DropDownButtonModel<SortByUIModel>
        {
            Name = "Sort by",
            Items = sortByItems,
        };

        var groupByDropDown = new DropDownButtonModel<ComicFilterModel.PropertyTypeEnum>
        {
            Name = "Group by",
            Items = _properties.ConvertAll(x => CreateToggleMenuFlyoutItem(PropertyTypeToDisplayName(x), groupBy == null ? false : x == groupBy.Type, x)),
        };

        string lastFilterName = lastFilter.Name ?? "";
        if (dataModel.LastFilterModified)
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

    private string PropertyTypeToDisplayName(ComicFilterModel.PropertyTypeEnum propertyType)
    {
        return propertyType switch
        {
            ComicFilterModel.PropertyTypeEnum.Title => "Title",
            ComicFilterModel.PropertyTypeEnum.Progress => "Progress",
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
        public DropDownButtonModel<ComicFilterModel.ViewTypeEnum> ViewTypeDropDown { get; set; }
        public DropDownButtonModel<SortByUIModel> SortByDropDown { get; set; }
        public DropDownButtonModel<ComicFilterModel.PropertyTypeEnum> GroupByDropDown { get; set; }
        public DropDownButtonModel<string> FilterPresetDropDown { get; set; }
    }

    public class DropDownButtonModel<T>
    {
        public string Name { get; set; }
        public List<MenuFlyoutItemModel<T>> Items { get; set; }
    }

    public class MenuFlyoutItemModel<T>
    {
        public bool IsSeperator { get; set; }
        public string Name { get; set; }
        public bool CanToggle { get; set; }
        public bool Toggled { get; set; }
        public T DataContext { get; set; }
    }

    public class SortByUIModel
    {
        public bool IsProperty { get; set; }
        public bool IsAscending { get; set; }
        public ComicFilterModel.PropertyTypeEnum Property { get; set; }
    }
}
