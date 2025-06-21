﻿// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace ComicReader.SDK.Data.SqlHelpers;

public class InCondition : ICondition
{
    private readonly SelectCommand? _subquery;
    private readonly ColumnOrValue _source;
    private readonly List<ColumnOrValue> _values;

    public InCondition(ColumnOrValue source, IEnumerable<ColumnOrValue> values)
    {
        _source = source;
        _values = [.. values];
    }

    public InCondition(ColumnOrValue source, SelectCommand subquery)
    {
        _source = source;
        _subquery = subquery;
        _values = [];
    }

    string ICondition.GetExpression(ICommandContext command)
    {
        StringBuilder sb = new();
        _source.AppendToCommand(sb, command);
        sb.Append(" IN (");
        if (_subquery != null)
        {
            sb.Append(_subquery.ToSubquery(command));
        }
        else
        {
            for (int i = 0; i < _values.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }
                _values[i].AppendToCommand(sb, command);
            }
        }
        sb.Append(')');
        return sb.ToString();
    }
}
