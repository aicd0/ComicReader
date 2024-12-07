// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.Data.SqlHelpers;

internal class LikeCondition : ICondition
{
    private readonly string _columnName;
    private readonly string _pattern;

    public LikeCondition(Column column, string pattern)
    {
        _columnName = column.Name;
        _pattern = pattern;
    }

    public string GetExpression(ICommandContext command)
    {
        string parameterName = command.AppendParameter(_pattern);
        return $"{_columnName} LIKE {parameterName}";
    }
}
