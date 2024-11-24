// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Data.Sqlite;

using Windows.Storage;

namespace ComicReader.Database;

public class SqlKey
{
    public SqlKey(string name, object value = null)
    {
        Name = name;
        Value = value;
    }

    public string Name;
    public object Value;
}

public class SqliteDatabaseManager
{
    public const string ComicTable = "comics";
    public const string TagCategoryTable = "tag_categories";
    public const string TagTable = "tags";

    private static StorageFolder DatabaseFolder => ApplicationData.Current.LocalFolder;
    private static string DatabaseFileName => "database.db";
    private static string DatabasePath => Path.Combine(DatabaseFolder.Path, DatabaseFileName);

    private static SqliteConnection m_connection = null;

    public static async Task Init()
    {
        // Create database.
        await DatabaseFolder.CreateFileAsync(DatabaseFileName, CreationCollisionOption.OpenIfExists);

        // Build connection.
        var connection = new SqliteConnection($"Filename={DatabasePath}");
        connection.Open();
        m_connection = connection;

        // Create tables.
        using (SqliteCommand command = NewCommand())
        {
            // Create comic table.
            command.CommandText = "CREATE TABLE IF NOT EXISTS " + ComicTable + " (" +
                ComicData.Field.Id + " INTEGER PRIMARY KEY AUTOINCREMENT," + // 0
                ComicData.Field.Type + " INTEGER NOT NULL," + // 1
                ComicData.Field.Location + " TEXT NOT NULL," + // 2
                ComicData.Field.Title1 + " TEXT," + // 3
                ComicData.Field.Title2 + " TEXT," + // 4
                ComicData.Field.Hidden + " BOOLEAN NOT NULL," + // 5
                ComicData.Field.Rating + " INTEGER NOT NULL," + // 6
                ComicData.Field.Progress + " INTEGER NOT NULL," + // 7
                ComicData.Field.LastVisit + " TIMESTAMP NOT NULL," + // 8
                ComicData.Field.LastPosition + " REAL NOT NULL," + // 9
                ComicData.Field.CoverFileCache + " TEXT)"; // 10
            await command.ExecuteNonQueryAsync();

            // Create tag category table.
            command.CommandText = "CREATE TABLE IF NOT EXISTS " + TagCategoryTable + " (" +
                ComicData.Field.TagCategory.Id + " INTEGER PRIMARY KEY AUTOINCREMENT," + // 0
                ComicData.Field.TagCategory.Name + " TEXT," + // 1
                ComicData.Field.TagCategory.ComicId + " INTEGER REFERENCES " + ComicTable + "(" + ComicData.Field.Id + ") ON DELETE CASCADE)"; // 2
            await command.ExecuteNonQueryAsync();

            // Create tag table.
            command.CommandText = "CREATE TABLE IF NOT EXISTS " + TagTable + " (" +
                ComicData.Field.Tag.Content + " TEXT," + // 0
                ComicData.Field.Tag.ComicId + " INTEGER NOT NULL," + // 1
                ComicData.Field.Tag.TagCategoryId + " INTEGER REFERENCES " + TagCategoryTable + "(" + ComicData.Field.TagCategory.Id + ") ON DELETE CASCADE)"; // 2
            await command.ExecuteNonQueryAsync();
        }
    }

    public static SqliteCommand NewCommand()
    {
        System.Diagnostics.Debug.Assert(m_connection != null);
        return m_connection.CreateCommand();
    }

    public static SqliteTransaction NewTransaction()
    {
        System.Diagnostics.Debug.Assert(m_connection != null);
        return m_connection.BeginTransaction();
    }

    public static async Task<bool> IsTableExist(string table)
    {
        SqliteCommand command = NewCommand();
        command.CommandText = "select count(*) from sqlite_master where type='table' and name='$table'";
        command.Parameters.AddWithValue("$table", table);
        long count = (long)await command.ExecuteScalarAsync();
        return count > 0;
    }
}
