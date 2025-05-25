// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using ComicReader.SDK.Common.DebugTools;

using Microsoft.Data.Sqlite;

using Windows.Storage;

namespace ComicReader.SDK.Data;

public class SqliteDatabase
{
    private static StorageFolder DatabaseFolder => ApplicationData.Current.LocalFolder;
    private static string DatabaseFileName => "database.db";
    private static string DatabasePath => Path.Combine(DatabaseFolder.Path, DatabaseFileName);

    private static SqliteConnection m_connection = null;

    public static async Task Initialize()
    {
        // Create database.
        await DatabaseFolder.CreateFileAsync(DatabaseFileName, CreationCollisionOption.OpenIfExists);

        // Build connection.
        var connection = new SqliteConnection($"Filename={DatabasePath}");
        connection.Open();
        m_connection = connection;
    }

    public static SqliteCommand NewCommand()
    {
        Logger.Assert(m_connection != null, "B16C84A49CD057D2");
        return m_connection.CreateCommand();
    }

    public static SqliteTransaction NewTransaction()
    {
        Logger.Assert(m_connection != null, "9D588AB3BDE60961");
        return m_connection.BeginTransaction();
    }
}
