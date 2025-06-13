// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace ComicReader.SDK.Data.SqlHelpers;

public class ColumnOrValue
{
    public readonly IColumnTypeless? Column;
    public readonly object Value;

    public ColumnOrValue(IColumnTypeless column)
    {
        Column = column;
        Value = 0;
    }

    public ColumnOrValue(object value)
    {
        Column = null;
        Value = value;
    }

    internal void AppendToCommand(StringBuilder sb, ICommandContext command)
    {
        if (Column is null)
        {
            string parameterName = command.AppendParameter(Value);
            sb.Append(parameterName);
        }
        else
        {
            sb.Append(Column.Name);
        }
    }
}
