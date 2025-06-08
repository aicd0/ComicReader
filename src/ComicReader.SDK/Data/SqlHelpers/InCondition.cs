// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace ComicReader.SDK.Data.SqlHelpers;

public class InCondition<T>(IColumn<T> column, IEnumerable<T> values) : ICondition where T : notnull
{
    private readonly string _columnName = column.Name;
    private readonly List<T> _values = [.. values];

    string ICondition.GetExpression(ICommandContext command)
    {
        StringBuilder sb = new();
        sb.Append(_columnName).Append(" IN (");
        for (int i = 0; i < _values.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }
            string parameterName = command.AppendParameter(_values[i]);
            sb.Append(parameterName);
        }
        sb.Append(')');
        return sb.ToString();
    }
}
