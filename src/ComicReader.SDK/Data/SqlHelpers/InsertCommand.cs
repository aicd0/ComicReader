// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Text;

namespace ComicReader.SDK.Data.SqlHelpers;

public class InsertCommand<T> where T : ITable
{
    private readonly T _table;
    private readonly Dictionary<string, IToken> _tokens = [];

    private bool _executed = false;

    public InsertCommand(T table)
    {
        _table = table;
    }

    public InsertCommand<T> AppendColumn<U>(Column column, U value)
    {
        var token = new Token<U>(column, value);
        _tokens[column.Name] = token;
        return this;
    }

    public long Execute()
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

        using CommandWrapper command = new();

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
        return (long)command.ExecuteScalar();
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
