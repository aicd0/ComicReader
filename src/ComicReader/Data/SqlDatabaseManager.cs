// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Data.Legacy;
using ComicReader.Data.Tables;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Data.SqlHelpers;

using Windows.Storage;

namespace ComicReader.Data;

public class SqlDatabaseManager
{
    private const string TAG = nameof(SqlDatabaseManager);

    private static StorageFolder DatabaseFolder => ApplicationData.Current.LocalFolder;
    private static string DatabaseFileName => "database.db";
    private static string DatabasePath => Path.Combine(DatabaseFolder.Path, DatabaseFileName);

    private static volatile SqlDatabase? _mainDatabase = null;

    public static SqlDatabase MainDatabase
    {
        get
        {
            return _mainDatabase ?? throw new InvalidOperationException("Main database is not initialized.");
        }
    }

    public static async Task Initialize(int databaseVersion)
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

        await UpdateDatabase(databaseVersion);
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
                {
                    string comicTable = ComicTable.Instance.GetTableName();
                    ExecuteCommand($"ALTER TABLE {comicTable} DROP COLUMN image_aspect_ratios");
                    ExecuteCommand($"ALTER TABLE {comicTable} DROP COLUMN cover_file_name");
                    ExecuteCommand($"ALTER TABLE {comicTable} ADD COLUMN {ComicTable.ColumnCoverCacheKey.Name} TEXT DEFAULT ''");
                    ExecuteCommand($"ALTER TABLE {comicTable} ADD COLUMN {ComicTable.ColumnDescription.Name} TEXT DEFAULT ''");
                }
                goto case 3;
            case 3:
                XmlDatabase.Settings.DatabaseVersion = 3;
                await XmlDatabaseManager.SaveUnsealed(XmlDatabaseItem.Settings);
                return TaskException.Success;
            default:
                Logger.AssertNotReachHere("A39EA189ED8BB40B");
                return TaskException.UnknownEnum;
        }
    }

    private static void ExecuteCommand(string commandText)
    {
        UnsafeCommand command = new(commandText);
        command.Execute(MainDatabase);
    }
}
