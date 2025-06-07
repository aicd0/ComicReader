// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace ComicReader.SDK.Data.SqlHelpers;

public class DeleteCommand<T> where T : ITable
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

    public void Execute(SqlDatabase database)
    {
        if (_executed)
        {
            throw new InvalidOperationException("Cannot execute the same command twice.");
        }
        _executed = true;

        using CommandWrapper command = GenerateCommand(database);
        command.ExecuteNonQuery();
    }

    public async Task ExecuteAsync(SqlDatabase database)
    {
        if (_executed)
        {
            throw new InvalidOperationException("Cannot execute the same command twice.");
        }
        _executed = true;

        using CommandWrapper command = GenerateCommand(database);
        await command.ExecuteNonQueryAsync();
    }

    private CommandWrapper GenerateCommand(SqlDatabase database)
    {
        CommandWrapper command = new(database);

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
