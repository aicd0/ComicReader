// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

namespace ComicReader.SDK.Data.SqlHelpers;

public class EqualityCondition<T> : ICondition
{
    private readonly string _columnName;
    private readonly T _value;

    public EqualityCondition(IColumn<T> column, T value)
    {
        _columnName = column.Name;
        _value = value;
    }

    string ICondition.GetExpression(ICommandContext command)
    {
        string parameterName = command.AppendParameter(_value);
        return $"{_columnName}={parameterName}";
    }
}
