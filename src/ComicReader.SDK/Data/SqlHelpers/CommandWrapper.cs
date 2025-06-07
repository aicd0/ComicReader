// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Data.Sqlite;

namespace ComicReader.SDK.Data.SqlHelpers;

public sealed class CommandWrapper : ICommandContext, IDisposable
{
    private readonly SqliteCommand _command;
    private int _parameterIndex = 0;

    public CommandWrapper(SqlDatabase database)
    {
        _command = database.NewCommand();
        _command.CommandType = System.Data.CommandType.Text;
    }

    public string AppendParameter(object value)
    {
        string parameterName = $"@param{_parameterIndex++}";
        _command.Parameters.AddWithValue(parameterName, value);
        return parameterName;
    }

    public void Dispose()
    {
        _command.Dispose();
    }

    public void SetCommandText(string commandText)
    {
#pragma warning disable CA2100
        _command.CommandText = commandText;
#pragma warning restore CA2100
    }

    public int ExecuteNonQuery()
    {
        return _command.ExecuteNonQuery();
    }

    public async Task<int> ExecuteNonQueryAsync()
    {
        return await _command.ExecuteNonQueryAsync();
    }

    public object? ExecuteScalar()
    {
        return _command.ExecuteScalar();
    }

    public SqliteDataReader ExecuteReader()
    {
        return _command.ExecuteReader();
    }

    public async Task<SqliteDataReader> ExecuteReaderAsync()
    {
        return await _command.ExecuteReaderAsync();
    }
}
