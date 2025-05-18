// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Threading.Tasks;

using ComicReader.Common.Lifecycle;
using ComicReader.Data.Models;

namespace ComicReader.Views.Home;

internal class EditFilterDialogViewModel
{
    public MutableLiveData<string> NameLiveData = new();
    public MutableLiveData<bool> SaveEnableLiveData = new();
    public MutableLiveData<bool> SaveAsNewEnableLiveData = new();

    private ComicFilterModel.ExternalModel _filterModel;
    private ComicFilterModel.ExternalFilterModel _filter;
    private string _inputName;

    public void Initialize(ComicFilterModel.ExternalFilterModel filter)
    {
        _ = InitializeAsync(filter);
    }

    public void UpdateName(string name)
    {
        name ??= string.Empty;
        name = name.Trim();
        if (name == _inputName)
        {
            return;
        }
        _inputName = name;

        ComicFilterModel.ExternalFilterModel filter = FindFilter(name);
        bool isValid = name.Length > 0;
        SaveEnableLiveData.Emit(isValid);
        SaveAsNewEnableLiveData.Emit(isValid && filter == null);
    }

    public void Save()
    {
        ComicFilterModel.ExternalFilterModel filter = _filter;
        if (filter == null)
        {
            return;
        }
        RemoveFilter(filter.Name);
        filter.Name = _inputName;
        OverwriteFilter(filter);
        _filterModel.LastFilterModified = false;
        _filterModel.LastFilter = filter.Clone();
        _ = ComicFilterModel.Instance.UpdateModel(_filterModel);
    }

    public void SaveAsNew()
    {
        ComicFilterModel.ExternalFilterModel filter = _filter;
        if (filter == null)
        {
            return;
        }
        filter = filter.Clone();
        filter.Name = _inputName;
        OverwriteFilter(filter);
        _filterModel.LastFilterModified = false;
        _filterModel.LastFilter = filter.Clone();
        _ = ComicFilterModel.Instance.UpdateModel(_filterModel);
    }

    public void Delete()
    {
        ComicFilterModel.ExternalFilterModel filter = _filter;
        if (filter == null)
        {
            return;
        }
        RemoveFilter(filter.Name);
        _filterModel.LastFilterModified = false;
        _filterModel.LastFilter = null;
        _ = ComicFilterModel.Instance.UpdateModel(_filterModel);
    }

    private async Task InitializeAsync(ComicFilterModel.ExternalFilterModel filter)
    {
        ComicFilterModel.ExternalModel filterModel = await ComicFilterModel.Instance.GetModel();
        _filterModel = filterModel;
        _filter = filter;
        if (filter != null)
        {
            UpdateName(filter.Name);
            NameLiveData.Emit(filter.Name);
        }
    }

    private ComicFilterModel.ExternalFilterModel FindFilter(string name)
    {
        ComicFilterModel.ExternalModel filterModel = _filterModel;
        if (filterModel == null)
        {
            return null;
        }
        return filterModel.Filters.Find(x => x.Name == name);
    }

    private void OverwriteFilter(ComicFilterModel.ExternalFilterModel filter)
    {
        ComicFilterModel.ExternalModel filterModel = _filterModel;
        if (filterModel == null)
        {
            return;
        }
        RemoveFilter(filter.Name);
        filterModel.Filters.Add(filter);
    }

    private void RemoveFilter(string name)
    {
        ComicFilterModel.ExternalModel filterModel = _filterModel;
        if (filterModel == null)
        {
            return;
        }
        ComicFilterModel.ExternalFilterModel oldFilter = filterModel.Filters.Find(x => x.Name == name);
        if (oldFilter != null)
        {
            filterModel.Filters.Remove(oldFilter);
        }
    }
}
