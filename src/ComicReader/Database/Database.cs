using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ComicReader.Database
{
    using RawTask = Task<Utils.TaskResult>;
    using SealedTask = Func<Task<Utils.TaskResult>, Utils.TaskResult>;
    using TaskResult = Utils.TaskResult;
    using TaskException = Utils.TaskException;

    public class LockContext
    {
        public int ComicTableLockDepth = 0;
    }

    public class DatabaseManager
    {
        public static bool DatabaseFirstInit { get; private set; }

        private static bool Initialized { get; set; } = false;

        public static async RawTask Init()
        {
            if (Initialized)
            {
                return new TaskResult();
            }
            Initialized = true;

            Log("Initializing database");

            // For backward compability.
            DatabaseFirstInit = !await SqliteDatabaseManager.IsDatabaseExist();

            await SqliteDatabaseManager.Init();
            await XmlDatabaseManager.Load();
            await Update();
            Utils.TaskQueueManager.AppendTask(ComicData.Manager.UpdateSealed(lazy_load: true));
            return new TaskResult();
        }

        public static async RawTask Update()
        {
            int old_version = XmlDatabase.Settings.DatabaseVersion;

            switch (old_version)
            {
                case -1:
                    if (DatabaseFirstInit)
                    {
                        goto case 0;
                    }

                    Log("Updating from version -1");

                    await ExecuteCommands(new List<string>
                    {
                        "PRAGMA foreign_keys=OFF",
                        "BEGIN TRANSACTION",

                        "CREATE TABLE IF NOT EXISTS comics_tmp (" +
                        "id INTEGER PRIMARY KEY AUTOINCREMENT," +
                        "type INTEGER NOT NULL DEFAULT 1," +
                        "location TEXT NOT NULL," +
                        "title1 TEXT," +
                        "title2 TEXT," +
                        "hidden BOOLEAN NOT NULL," +
                        "rating INTEGER NOT NULL," +
                        "progress INTEGER NOT NULL," +
                        "last_visit TIMESTAMP NOT NULL," +
                        "last_pos REAL NOT NULL," +
                        "image_aspect_ratios BLOB," +
                        "cover_file_name TEXT)",

                        "INSERT INTO comics_tmp (" +
                        "id, location, title1, title2, hidden," +
                        "rating, progress, last_visit, last_pos," +
                        "image_aspect_ratios, cover_file_name" +
                        ") SELECT " +
                        "id, dir, title1, title2, hidden," +
                        "rating, progress, last_visit, last_pos," +
                        "image_aspect_ratios, cover_file_name" +
                        " FROM comics",

                        "DROP TABLE IF EXISTS comics",
                        "ALTER TABLE comics_tmp RENAME TO comics",

                        "COMMIT",
                        "PRAGMA foreign_keys=ON",
                    });

                    goto case 1;
                case 1:
                case 0:
                    XmlDatabase.Settings.DatabaseVersion = 1;
                    await XmlDatabaseManager.SaveUnsealed(XmlDatabaseItem.Settings);
                    return new TaskResult();
                default:
                    System.Diagnostics.Debug.Assert(false);
                    return new TaskResult(TaskException.UnknownEnum);
            }
        }

        private static async Task ExecuteCommands(List<string> commands)
        {
            using (SqliteCommand command = SqliteDatabaseManager.NewCommand())
            {
                command.CommandText = string.Join(';', commands) + ";";
                await command.ExecuteNonQueryAsync();
            }
        }

        private static void Log(string content)
        {
            Utils.Debug.Log("Database: " + content);
        }
    }
}
