﻿// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ComicReader.Data.SqlHelpers;

internal class DeleteCommand<T> where T : ITable
{
    private readonly T _table;
    private readonly List<ICondition> _conditions = [];

    private bool _executed = false;

    public DeleteCommand(T table)
    {
        _table = table;
    }

    public DeleteCommand<T> AppendCondition<U>(Column column, U value)
    {
        return AppendCondition(new EqualityCondition<U>(column, value));
    }

    public DeleteCommand<T> AppendCondition(ICondition condition)
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

        using CommandWrapper command = GenerateCommand();
        command.ExecuteNonQuery();
    }

    public async Task ExecuteAsync()
    {
        if (_executed)
        {
            throw new InvalidOperationException("Cannot execute the same command twice.");
        }
        _executed = true;

        using CommandWrapper command = GenerateCommand();
        await command.ExecuteNonQueryAsync();
    }

    private CommandWrapper GenerateCommand()
    {
        CommandWrapper command = new();

        StringBuilder sb = new("DELETE FROM ");
        sb.Append(_table.GetTableName());
        sb.Append(" WHERE TRUE");

        foreach (ICondition condition in _conditions)
        {
            sb.Append(" AND ");
            sb.Append(condition.GetExpression(command));
        }

        command.SetCommandText(sb.ToString());
        return command;
    }
}
