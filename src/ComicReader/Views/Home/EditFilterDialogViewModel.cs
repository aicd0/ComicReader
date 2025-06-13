// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;

using ComicReader.Common.Expression;
using ComicReader.Common.Expression.Sql;
using ComicReader.Common.Lifecycle;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.Data.Tables;
using ComicReader.SDK.Data.SqlHelpers;
using ComicReader.ViewModels;

namespace ComicReader.Views.Home;

internal partial class EditFilterDialogViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public MutableLiveData<string> NameLiveData = new();
    public MutableLiveData<string> ExpressionLiveData = new();
    public MutableLiveData<string> ParseResultLiveData = new();
    public MutableLiveData<bool> SaveEnableLiveData = new();
    public MutableLiveData<bool> SaveAsNewEnableLiveData = new();

    private ComicFilterModel.ExternalModel? _filterModel;
    private ComicFilterModel.ExternalFilterModel? _filter;
    private string _name = "";
    private string _expression = "";
    private bool _isNameValid = false;
    private bool _isNameExists = false;
    private bool _isExpressionValid = false;

    private ObservableCollection<TagViewModel> _expressionButtons = [];
    public ObservableCollection<TagViewModel> ExpressionButtons
    {
        get => _expressionButtons;
        set
        {
            _expressionButtons = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExpressionButtons)));
        }
    }

    public void Initialize(ComicFilterModel.ExternalFilterModel filter)
    {
        _ = InitializeAsync(filter);
    }

    public void UpdateName(string name)
    {
        name = name.Trim();
        if (name == _name)
        {
            return;
        }
        _name = name;

        _isNameValid = name.Length > 0;
        _isNameExists = FindFilter(name) != null;
        UpdateButtonStates();
    }

    public void UpdateExpression(string expression)
    {
        if (expression == _expression)
        {
            return;
        }
        _expression = expression;

        ComicFilterModel.ExternalFilterModel? filter = _filter;
        if (filter == null)
        {
            return;
        }

        void onExpressionInvalid(string message)
        {
            _isExpressionValid = false;
            ParseResultLiveData.Emit(message);
            UpdateButtonStates();
        }

        ExpressionToken token;
        try
        {
            token = ExpressionParser.Parse(expression);
        }
        catch (ExpressionException e)
        {
            onExpressionInvalid(e.Message);
            return;
        }

        SelectCommand command;
        try
        {
            command = SQLGenerator<long>.CreateSQLQuery(token, new ComicSQLCommandProvider());
        }
        catch (ExpressionException e)
        {
            onExpressionInvalid(e.Message);
            return;
        }
        command.PutQueryInt64(ComicTable.ColumnId);

        _isExpressionValid = true;
        ParseResultLiveData.Emit(command.ToString());
        filter.Expression = expression;
        UpdateButtonStates();
    }

    public void Save()
    {
        ComicFilterModel.ExternalFilterModel? filter = _filter;
        if (filter == null)
        {
            return;
        }
        RemoveFilter(filter.Name);
        filter.Name = _name;
        OverwriteFilter(filter);

        ComicFilterModel.ExternalModel? model = _filterModel;
        if (model == null)
        {
            return;
        }
        model.LastFilterModified = false;
        model.LastFilter = filter.Clone();
        _ = ComicFilterModel.Instance.UpdateModel(model);
    }

    public void SaveAsNew()
    {
        ComicFilterModel.ExternalFilterModel? filter = _filter;
        if (filter == null)
        {
            return;
        }
        filter = filter.Clone();
        filter.Name = _name;
        OverwriteFilter(filter);

        ComicFilterModel.ExternalModel? model = _filterModel;
        if (model == null)
        {
            return;
        }
        model.LastFilterModified = false;
        model.LastFilter = filter.Clone();
        _ = ComicFilterModel.Instance.UpdateModel(model);
    }

    public void Delete()
    {
        ComicFilterModel.ExternalFilterModel? filter = _filter;
        if (filter == null)
        {
            return;
        }
        RemoveFilter(filter.Name);

        ComicFilterModel.ExternalModel? model = _filterModel;
        if (model == null)
        {
            return;
        }
        model.LastFilterModified = false;
        model.LastFilter = null;
        _ = ComicFilterModel.Instance.UpdateModel(model);
    }

    private async Task InitializeAsync(ComicFilterModel.ExternalFilterModel filter)
    {
        {
            ObservableCollection<TagViewModel> buttons = [];
            buttons.Add(new() { Tag = "AND" });
            buttons.Add(new() { Tag = "OR" });
            buttons.Add(new() { Tag = "NOT" });
            buttons.Add(new() { Tag = "=" });
            buttons.Add(new() { Tag = ">" });
            buttons.Add(new() { Tag = "<" });
            buttons.Add(new() { Tag = ">=" });
            buttons.Add(new() { Tag = "<=" });
            buttons.Add(new() { Tag = "()" });
            buttons.Add(new() { Tag = "IN" });
            buttons.Add(new() { Tag = "Title" });
            buttons.Add(new() { Tag = "Rating" });
            buttons.Add(new() { Tag = "Tag" });
            buttons.Add(new() { Tag = "Last read time" });
            buttons.Add(new() { Tag = "Completion state" });
            ExpressionButtons = buttons;
        }

        ComicFilterModel.ExternalModel? filterModel = await ComicFilterModel.Instance.GetModel();
        _filterModel = filterModel;
        _filter = filter;
        if (filter != null)
        {
            UpdateName(filter.Name);
            NameLiveData.Emit(filter.Name);
            UpdateExpression(filter.Expression);
            ExpressionLiveData.Emit(filter.Expression);
        }
    }

    private void UpdateButtonStates()
    {
        bool isInputValid = _isNameValid && _isExpressionValid;
        SaveEnableLiveData.Emit(isInputValid);
        SaveAsNewEnableLiveData.Emit(isInputValid && !_isNameExists);
    }

    private ComicFilterModel.ExternalFilterModel? FindFilter(string name)
    {
        ComicFilterModel.ExternalModel? filterModel = _filterModel;
        if (filterModel == null)
        {
            return null;
        }
        return filterModel.Filters.Find(x => x.Name == name);
    }

    private void OverwriteFilter(ComicFilterModel.ExternalFilterModel filter)
    {
        ComicFilterModel.ExternalModel? filterModel = _filterModel;
        if (filterModel == null)
        {
            return;
        }
        RemoveFilter(filter.Name);
        filterModel.Filters.Add(filter);
    }

    private void RemoveFilter(string name)
    {
        ComicFilterModel.ExternalModel? filterModel = _filterModel;
        if (filterModel == null)
        {
            return;
        }
        ComicFilterModel.ExternalFilterModel? oldFilter = filterModel.Filters.Find(x => x.Name == name);
        if (oldFilter != null)
        {
            filterModel.Filters.Remove(oldFilter);
        }
    }
}
