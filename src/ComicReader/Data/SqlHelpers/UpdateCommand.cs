﻿// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;

namespace ComicReader.Data.SqlHelpers;

internal class UpdateCommand<T> where T : ITable
{
    private readonly T _table;
    private readonly Dictionary<string, IToken> _tokens = [];
    private readonly List<ICondition> _conditions = [];

    private bool _executed = false;

    public UpdateCommand(T table)
    {
        _table = table;
    }

    public UpdateCommand<T> AppendColumn<U>(Column column, U value)
    {
        var token = new Token<U>(column, value);
        _tokens[column.Name] = token;
        return this;
    }

    public UpdateCommand<T> AppendCondition<U>(Column column, U value)
    {
        return AppendCondition(new EqualityCondition<U>(column, value));
    }

    public UpdateCommand<T> AppendCondition(ICondition condition)
    {
        _conditions.Add(condition);
        return this;
    }

    public void Execute()
    {
        if (_executed)
        {
            throw new InvalidOperationException("Cannot execute the same command twice.");
        }
        _executed = true;

        if (_tokens.Count == 0)
        {
            return;
        }

        using CommandWrapper command = new();

        StringBuilder sb = new($"UPDATE {_table.GetTableName()} SET ");

        bool divider = false;
        foreach (IToken token in _tokens.Values)
        {
            string parameterKey = token.AppendParameter(command);

            if (divider)
            {
                sb.Append(',');
            }
            divider = true;
            sb.Append(token.GetColumnName());
            sb.Append('=');
            sb.Append(parameterKey);
        }

        sb.Append(" WHERE TRUE");

        foreach (ICondition condition in _conditions)
        {
            sb.Append(" AND ");
            sb.Append(condition.GetExpression(command));
        }

        command.SetCommandText(sb.ToString());
        command.ExecuteNonQuery();
    }

    private class Token<U> : IToken
    {
        private readonly Column _column;
        private readonly U _value;

        public Token(Column column, U value)
        {
            _column = column;
            _value = value;
        }

        public string AppendParameter(ICommandContext command)
        {
            return command.AppendParameter(_value);
        }

        public string GetColumnName()
        {
            return _column.Name;
        }
    }

    private interface IToken
    {
        string GetColumnName();

        string AppendParameter(ICommandContext command);
    }
}
