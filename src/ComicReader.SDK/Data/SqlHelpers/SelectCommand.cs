// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Text;

using Microsoft.Data.Sqlite;

namespace ComicReader.SDK.Data.SqlHelpers;

public class SelectCommand<T> where T : ITable
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

    public IToken<long> PutQueryCountAll()
    {
        return PutToken(new GeneralToken<long>("COUNT(*)", delegate (SqliteDataReader reader, int ordinal) { return reader.GetInt64(ordinal); }));
    }

    public IToken<int> PutQueryInt32(Column column)
    {
        return PutToken(new ColumnToken<int>(column, delegate (SqliteDataReader reader, int ordinal) { return reader.GetInt32(ordinal); }));
    }

    public IToken<long> PutQueryInt64(Column column)
    {
        return PutToken(new ColumnToken<long>(column, delegate (SqliteDataReader reader, int ordinal) { return reader.GetInt64(ordinal); }));
    }

    public IToken<string> PutQueryString(Column column)
    {
        return PutToken(new ColumnToken<string>(column, delegate (SqliteDataReader reader, int ordinal) { return reader.GetString(ordinal); }));
    }

    public IToken<bool> PutQueryBoolean(Column column)
    {
        return PutToken(new ColumnToken<bool>(column, delegate (SqliteDataReader reader, int ordinal) { return reader.GetBoolean(ordinal); }));
    }

    public IToken<double> PutQueryDouble(Column column)
    {
        return PutToken(new ColumnToken<double>(column, delegate (SqliteDataReader reader, int ordinal) { return reader.GetDouble(ordinal); }));
    }

    public IToken<DateTimeOffset> PutQueryDateTimeOffset(Column column)
    {
        return PutToken(new ColumnToken<DateTimeOffset>(column, delegate (SqliteDataReader reader, int ordinal) { return reader.GetDateTimeOffset(ordinal); }));
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

    public IReader Execute(SqlDatabase database)
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
        CommandWrapper command = GenerateCommand(database, tokens);
        return new Reader(command, command.ExecuteReader(), tokens);
    }

    public async Task<IReader> ExecuteAsync(SqlDatabase database)
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
        CommandWrapper command = GenerateCommand(database, tokens);
        return new Reader(command, await command.ExecuteReaderAsync(), tokens);
    }

    private CommandWrapper GenerateCommand(SqlDatabase database, List<ITokenInternal> tokens)
    {
        CommandWrapper command = new(database);

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
            sb.Append(token.GetQueryExpression());
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

    private Token<V> PutToken<V>(Token<V> token)
    {
        _tokens[token.GetQueryExpression()] = token;
        return token;
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

        public async Task<bool> ReadAsync()
        {
            bool hasMore = await reader.ReadAsync();
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

        public Task<bool> ReadAsync()
        {
            return Task.FromResult(false);
        }
    }

    private abstract class Token<V> : IToken<V>, ITokenInternal
    {
        private readonly Func<SqliteDataReader, int, V> _getter;
        private bool _valueSet = false;
        private V _value = default;

        public Token(Func<SqliteDataReader, int, V> getter)
        {
            _getter = getter;
        }

        public abstract string GetQueryExpression();

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

    private class GeneralToken<V> : Token<V>
    {
        private readonly string _expression;

        public GeneralToken(string expression, Func<SqliteDataReader, int, V> getter) : base(getter)
        {
            _expression = expression;
        }

        public override string GetQueryExpression()
        {
            return _expression;
        }
    }

    private class ColumnToken<V> : Token<V>
    {
        private readonly Column _column;

        public ColumnToken(Column column, Func<SqliteDataReader, int, V> getter) : base(getter)
        {
            _column = column;
        }

        public override string GetQueryExpression()
        {
            return _column.Name;
        }
    }

    public interface IReader : IDisposable
    {
        bool Read();

        Task<bool> ReadAsync();
    }

    private interface ITokenInternal
    {
        string GetQueryExpression();

        void UpdateValue(SqliteDataReader reader, int ordinal);
    }

    public interface IToken<U>
    {
        U GetValue();
    }
}
