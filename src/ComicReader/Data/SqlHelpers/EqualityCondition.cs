// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

namespace ComicReader.Data.SqlHelpers;

internal class EqualityCondition<T> : ICondition
{
    private readonly string _columnName;
    private readonly T _value;

    public EqualityCondition(Column column, T value)
    {
        _columnName = column.Name;
        _value = value;
    }

    public string GetExpression(ICommandContext command)
    {
        string parameterName = command.AppendParameter(_value);
        return $"{_columnName}={parameterName}";
    }
}
