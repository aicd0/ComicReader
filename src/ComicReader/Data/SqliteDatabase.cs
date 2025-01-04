// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.DebugTools;
using ComicReader.Data.Comic;

using Microsoft.Data.Sqlite;

using Windows.Storage;

namespace ComicReader.Data;

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
    private static StorageFolder DatabaseFolder => ApplicationData.Current.LocalFolder;
    private static string DatabaseFileName => "database.db";
    private static string DatabasePath => Path.Combine(DatabaseFolder.Path, DatabaseFileName);

    private static SqliteConnection m_connection = null;

    public static async Task Initialize(int databaseVersion)
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
            string comicTable = ComicTable.Instance.GetTableName();
            string tagCategoryTable = TagCategoryTable.Instance.GetTableName();
            string tagTable = TagTable.Instance.GetTableName();

#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
            command.CommandText = "CREATE TABLE IF NOT EXISTS " + comicTable + " (" +
                ComicTable.ColumnId.Name + " INTEGER PRIMARY KEY AUTOINCREMENT" +
                "," + ComicTable.ColumnType.Name + " INTEGER NOT NULL" +
                "," + ComicTable.ColumnLocation.Name + " TEXT NOT NULL" +
                "," + ComicTable.ColumnTitle1.Name + " TEXT" +
                "," + ComicTable.ColumnTitle2.Name + " TEXT" +
                "," + ComicTable.ColumnHidden.Name + " BOOLEAN NOT NULL" +
                "," + ComicTable.ColumnRating.Name + " INTEGER NOT NULL" +
                "," + ComicTable.ColumnProgress.Name + " INTEGER NOT NULL" +
                "," + ComicTable.ColumnLastVisit.Name + " TIMESTAMP NOT NULL" +
                "," + ComicTable.ColumnLastPosition.Name + " REAL NOT NULL" +
                "," + ComicTable.ColumnCoverCacheKey.Name + " TEXT" +
                "," + ComicTable.ColumnDescription.Name + " TEXT" +
                ")";
            await command.ExecuteNonQueryAsync();

            command.CommandText = "CREATE TABLE IF NOT EXISTS " + tagCategoryTable + " (" +
                TagCategoryTable.ColumnId.Name + " INTEGER PRIMARY KEY AUTOINCREMENT" +
                "," + TagCategoryTable.ColumnName.Name + " TEXT" +
                "," + TagCategoryTable.ColumnComicId.Name + " INTEGER REFERENCES " + comicTable + "(" + ComicTable.ColumnId.Name + ") ON DELETE CASCADE" +
                ")";
            await command.ExecuteNonQueryAsync();

            command.CommandText = "CREATE TABLE IF NOT EXISTS " + tagTable + " (" +
                TagTable.ColumnContent.Name + " TEXT" +
                "," + TagTable.ColumnComicId.Name + " INTEGER NOT NULL" +
                "," + TagTable.ColumnTagCategoryId.Name + " INTEGER REFERENCES " + tagCategoryTable + "(" + TagCategoryTable.ColumnId.Name + ") ON DELETE CASCADE" +
                ")";
            await command.ExecuteNonQueryAsync();
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
        }

        await UpdateDatabase(databaseVersion);
    }

    public static SqliteCommand NewCommand()
    {
        DebugUtils.Assert(m_connection != null);
        return m_connection.CreateCommand();
    }

    public static SqliteTransaction NewTransaction()
    {
        DebugUtils.Assert(m_connection != null);
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

    private static async Task<TaskException> UpdateDatabase(int databaseVersion)
    {
        switch (databaseVersion)
        {
            case -1:
            case 0:
            case 1:
                goto case 3;
            case 2:
                using (SqliteCommand command = NewCommand())
                {
                    string comicTable = ComicTable.Instance.GetTableName();
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
                    command.CommandText = $"ALTER TABLE {comicTable} DROP COLUMN image_aspect_ratios";
                    command.ExecuteNonQuery();
                    command.CommandText = $"ALTER TABLE {comicTable} DROP COLUMN cover_file_name";
                    command.ExecuteNonQuery();
                    command.CommandText = $"ALTER TABLE {comicTable} ADD COLUMN {ComicTable.ColumnCoverCacheKey.Name} TEXT DEFAULT ''";
                    command.ExecuteNonQuery();
                    command.CommandText = $"ALTER TABLE {comicTable} ADD COLUMN {ComicTable.ColumnDescription.Name} TEXT DEFAULT ''";
                    command.ExecuteNonQuery();
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
                }
                goto case 3;
            case 3:
                XmlDatabase.Settings.DatabaseVersion = 3;
                await XmlDatabaseManager.SaveUnsealed(XmlDatabaseItem.Settings);
                return TaskException.Success;
            default:
                DebugUtils.Assert(false);
                return TaskException.UnknownEnum;
        }
    }
}
