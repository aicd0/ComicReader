// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Text;

namespace ComicReader.SDK.Data.SqlHelpers;

public class InsertCommand
{
    private readonly ITable _table;
    private readonly Dictionary<string, IToken> _tokens = [];

    private bool _executed = false;

    public InsertCommand(ITable table)
    {
        _table = table;
    }

    public InsertCommand AppendColumn(IColumnTypeless column, object value)
    {
        var token = new Token(column, value);
        _tokens[column.Name] = token;
        return this;
    }

    public long Execute(SqlDatabase database)
    {
        if (_executed)
        {
            throw new InvalidOperationException("Cannot execute the same command twice.");
        }
        _executed = true;

        if (_tokens.Count == 0)
        {
            throw new ArgumentException("Empty tokens.");
        }

        CommandWrapper command = new();

        StringBuilder sb = new("INSERT INTO ");
        sb.Append(_table.GetTableName());
        sb.Append(" (");

        bool divider = false;
        foreach (IToken token in _tokens.Values)
        {
            if (divider)
            {
                sb.Append(',');
            }
            divider = true;
            sb.Append(token.GetColumnName());
        }

        sb.Append(") VALUES (");

        divider = false;
        foreach (IToken token in _tokens.Values)
        {
            string parameterName = token.AppendParameter(command);

            if (divider)
            {
                sb.Append(',');
            }
            divider = true;
            sb.Append(parameterName);
        }

        sb.Append(");SELECT LAST_INSERT_ROWID();");

        command.SetCommandText(sb.ToString());
        return (long)command.ExecuteScalar(database);
    }

    private class Token : IToken
    {
        private readonly IColumnTypeless _column;
        private readonly object _value;

        public Token(IColumnTypeless column, object value)
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
