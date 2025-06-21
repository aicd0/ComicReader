// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;

using ComicReader.Data.Tables;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Storage;
using ComicReader.SDK.Data.SqlHelpers;

namespace ComicReader.Data;

public class SqlDatabaseManager
{
    private const string TAG = nameof(SqlDatabaseManager);

    private static string DatabaseFolderPath => StorageLocation.GetLocalFolderPath();
    private static string DatabaseFileName => "database.db";
    private static string DatabasePath => Path.Combine(DatabaseFolderPath, DatabaseFileName);

    private static volatile SqlDatabase? _mainDatabase = null;

    public static SqlDatabase MainDatabase
    {
        get
        {
            return _mainDatabase ?? throw new InvalidOperationException("Main database is not initialized.");
        }
    }

    public static void Initialize()
    {
        if (_mainDatabase != null)
        {
            Logger.F(TAG, "Database is already initialized.");
            return;
        }

        _mainDatabase = new SqlDatabase(DatabasePath);

        // Create tables.
        string comicTable = ComicTable.Instance.GetTableName();
        string tagCategoryTable = TagCategoryTable.Instance.GetTableName();
        string tagTable = TagTable.Instance.GetTableName();

        ExecuteCommand("CREATE TABLE IF NOT EXISTS " + comicTable + " (" +
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
            "," + ComicTable.ColumnCompletionState.Name + " INTEGER NOT NULL" +
            ")");

        ExecuteCommand("CREATE TABLE IF NOT EXISTS " + tagCategoryTable + " (" +
            TagCategoryTable.ColumnId.Name + " INTEGER PRIMARY KEY AUTOINCREMENT" +
            "," + TagCategoryTable.ColumnName.Name + " TEXT" +
            "," + TagCategoryTable.ColumnComicId.Name + " INTEGER REFERENCES " + comicTable + "(" + ComicTable.ColumnId.Name + ") ON DELETE CASCADE" +
            ")");

        ExecuteCommand("CREATE TABLE IF NOT EXISTS " + tagTable + " (" +
            TagTable.ColumnContent.Name + " TEXT" +
            "," + TagTable.ColumnComicId.Name + " INTEGER NOT NULL" +
            "," + TagTable.ColumnTagCategoryId.Name + " INTEGER REFERENCES " + tagCategoryTable + "(" + TagCategoryTable.ColumnId.Name + ") ON DELETE CASCADE" +
            ")");
    }

    public static void UpdateDatabase(int databaseVersion)
    {
        string tableName = ComicTable.Instance.GetTableName();
        switch (databaseVersion)
        {
            case -1:
            case 0:
            case 1:
                goto case 4;
            case 2:
                {
                    ExecuteCommand($"ALTER TABLE {tableName} DROP COLUMN image_aspect_ratios");
                    ExecuteCommand($"ALTER TABLE {tableName} DROP COLUMN cover_file_name");
                    ExecuteCommand($"ALTER TABLE {tableName} ADD COLUMN {ComicTable.ColumnCoverCacheKey.Name} TEXT DEFAULT ''");
                    ExecuteCommand($"ALTER TABLE {tableName} ADD COLUMN {ComicTable.ColumnDescription.Name} TEXT DEFAULT ''");
                }
                goto case 3;
            case 3:
                {
                    ExecuteCommand($"ALTER TABLE {tableName} ADD COLUMN {ComicTable.ColumnCompletionState.Name} INTEGER NOT NULL DEFAULT 0");
                }
                goto case 4;
            case 4:
                break;
            default:
                Logger.AssertNotReachHere("A39EA189ED8BB40B");
                break;
        }
    }

    private static void ExecuteCommand(string commandText)
    {
        UnsafeCommand command = new(commandText);
        command.Execute(MainDatabase);
    }
}
