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
        public static async RawTask Init()
        {
            await SqliteDatabaseManager.Init();
            await XmlDatabaseManager.Load();

            Utils.TaskQueueManager.AppendTask(
                ComicDataManager.UpdateSealed(lazy_load: true));

            return new TaskResult();
        }
    }
}
