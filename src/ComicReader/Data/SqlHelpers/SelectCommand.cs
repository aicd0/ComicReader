// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Data.Sqlite;

namespace ComicReader.Data.SqlHelpers;

internal class SelectCommand<T> where T : ITable
{
    private readonly T _table;
    private readonly Dictionary<string, ITokenInternal> _tokens = [];
    private readonly List<ICondition> _conditions = [];

    private bool _collateNocase = false;
    private bool _distinct = false;
    private int _limit = 0;
    private bool _executed = false;

    public SelectCommand(T table)
    {
        _table = table;
    }

    public SelectCommand<T> Distinct()
    {
        _distinct = true;
        return this;
    }

    public SelectCommand<T> CollateNocase()
    {
        _collateNocase = true;
        return this;
    }

    public SelectCommand<T> Limit(int limit)
    {
        _limit = limit;
        return this;
    }

    public IToken<int> PutQueryInt32(Column column)
    {
        var token = new Token<int>(column, delegate (SqliteDataReader reader, int ordinal) { return reader.GetInt32(ordinal); });
        _tokens[column.Name] = token;
        return token;
    }

    public IToken<long> PutQueryInt64(Column column)
    {
        var token = new Token<long>(column, delegate (SqliteDataReader reader, int ordinal) { return reader.GetInt64(ordinal); });
        _tokens[column.Name] = token;
        return token;
    }

    public IToken<string> PutQueryString(Column column)
    {
        var token = new Token<string>(column, delegate (SqliteDataReader reader, int ordinal) { return reader.GetString(ordinal); });
        _tokens[column.Name] = token;
        return token;
    }

    public IToken<bool> PutQueryBoolean(Column column)
    {
        var token = new Token<bool>(column, delegate (SqliteDataReader reader, int ordinal) { return reader.GetBoolean(ordinal); });
        _tokens[column.Name] = token;
        return token;
    }

    public IToken<double> PutQueryDouble(Column column)
    {
        var token = new Token<double>(column, delegate (SqliteDataReader reader, int ordinal) { return reader.GetDouble(ordinal); });
        _tokens[column.Name] = token;
        return token;
    }

    public IToken<DateTimeOffset> PutQueryDateTimeOffset(Column column)
    {
        var token = new Token<DateTimeOffset>(column, delegate (SqliteDataReader reader, int ordinal) { return reader.GetDateTimeOffset(ordinal); });
        _tokens[column.Name] = token;
        return token;
    }

    public SelectCommand<T> AppendCondition<U>(Column column, U value)
    {
        return AppendCondition(new EqualityCondition<U>(column, value));
    }

    public SelectCommand<T> AppendCondition(ICondition condition)
    {
        _conditions.Add(condition);
        return this;
    }

    public IReader Execute()
    {
        if (_executed)
        {
            throw new InvalidOperationException("Cannot execute the same command twice.");
        }
        _executed = true;

        if (_tokens.Count == 0)
        {
            return new EmptyReader();
        }

        var tokens = new List<ITokenInternal>(_tokens.Values);
        CommandWrapper command = GenerateCommand(tokens);
        return new Reader(command, command.ExecuteReader(), tokens);
    }

    public async Task<IReader> ExecuteAsync()
    {
        if (_executed)
        {
            throw new InvalidOperationException("Cannot execute the same command twice.");
        }
        _executed = true;

        if (_tokens.Count == 0)
        {
            return new EmptyReader();
        }

        var tokens = new List<ITokenInternal>(_tokens.Values);
        CommandWrapper command = GenerateCommand(tokens);
        return new Reader(command, await command.ExecuteReaderAsync(), tokens);
    }

    private CommandWrapper GenerateCommand(List<ITokenInternal> tokens)
    {
        CommandWrapper command = new();

        StringBuilder sb = new($"SELECT ");

        if (_distinct)
        {
            sb.Append("DISTINCT ");
        }

        bool divider = false;
        foreach (ITokenInternal token in tokens)
        {
            if (divider)
            {
                sb.Append(',');
            }
            divider = true;
            sb.Append(token.GetColumnName());
        }

        sb.Append(" FROM ");
        sb.Append(_table.GetTableName());
        sb.Append(" WHERE TRUE");

        foreach (ICondition condition in _conditions)
        {
            sb.Append(" AND ");
            sb.Append(condition.GetExpression(command));
        }

        if (_collateNocase)
        {
            sb.Append(" COLLATE NOCASE");
        }

        if (_limit > 0)
        {
            sb.Append(" LIMIT ");
            sb.Append(_limit);
        }

        command.SetCommandText(sb.ToString());
        return command;
    }

    private class Reader(CommandWrapper command, SqliteDataReader reader, List<ITokenInternal> tokens) : IReader
    {
        public void Dispose()
        {
            reader?.Dispose();
            command?.Dispose();
        }

        public bool Read()
        {
            bool hasMore = reader.Read();

            if (hasMore)
            {
                for (int i = 0; i < tokens.Count; ++i)
                {
                    ITokenInternal token = tokens[i];
                    token.UpdateValue(reader, i);
                }
            }

            return hasMore;
        }
    }

    private class EmptyReader : IReader
    {
        public void Dispose()
        {
        }

        public bool Read()
        {
            return false;
        }
    }

    private class Token<V> : IToken<V>, ITokenInternal
    {
        private readonly Column _column;
        private readonly Func<SqliteDataReader, int, V> _getter;
        private bool _valueSet = false;
        private V _value = default;

        public Token(Column column, Func<SqliteDataReader, int, V> getter)
        {
            _column = column;
            _getter = getter;
        }

        public string GetColumnName()
        {
            return _column.Name;
        }

        public V GetValue()
        {
            if (!_valueSet)
            {
                throw new InvalidOperationException("Value is not set.");
            }

            return _value;
        }

        public void UpdateValue(SqliteDataReader reader, int ordinal)
        {
            _value = _getter(reader, ordinal);
            _valueSet = true;
        }
    }

    public interface IReader : IDisposable
    {
        bool Read();
    }

    private interface ITokenInternal
    {
        string GetColumnName();

        void UpdateValue(SqliteDataReader reader, int ordinal);
    }

    public interface IToken<U>
    {
        U GetValue();
    }
}
