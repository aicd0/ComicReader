// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.SqlHelpers;

public class UnsafeCommand
{
    private readonly string _command;

    private bool _executed = false;

    public UnsafeCommand(string command)
    {
        _command = command;
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
        command.SetCommandText(_command);
        return command;
    }
}
