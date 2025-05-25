// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Data.Legacy;
using ComicReader.Data.Tables;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Data;

using Microsoft.Data.Sqlite;

namespace ComicReader.Data;

public class SqliteDatabaseManager
{
    public static async Task Initialize(int databaseVersion)
    {
        // Create tables.
        using (SqliteCommand command = SqliteDatabase.NewCommand())
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

    private static async Task<TaskException> UpdateDatabase(int databaseVersion)
    {
        switch (databaseVersion)
        {
            case -1:
            case 0:
            case 1:
                goto case 3;
            case 2:
                using (SqliteCommand command = SqliteDatabase.NewCommand())
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
                Logger.AssertNotReachHere("A39EA189ED8BB40B");
                return TaskException.UnknownEnum;
        }
    }
}
