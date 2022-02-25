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

        public static async RawTask Init()
        {
            Utils.Debug.Log("Initializing database");

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
            await Task.Run(() => { });
            int old_version = XmlDatabase.Settings.DatabaseVersion;

            switch (old_version)
            {
                case -1:
                    if (DatabaseFirstInit)
                    {
                        break;
                    }

                    goto case 1;
                case 1:
                    break;
                default:
                    System.Diagnostics.Debug.Assert(false);
                    break;
            }

            XmlDatabase.Settings.DatabaseVersion = 1;
            Utils.TaskQueueManager.AppendTask(XmlDatabaseManager.SaveSealed(XmlDatabaseItem.Settings));
            return new TaskResult();
        }
    }
}
