using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Windows.Storage;
using Windows.Storage.AccessCache;

namespace ComicReader.Data
{
    using RawTask = Task<Utils.TaskQueue.TaskResult>;
    using SealedTask = Func<Task<Utils.TaskQueue.TaskResult>, Utils.TaskQueue.TaskResult>;
    using TaskResult = Utils.TaskQueue.TaskResult;
    using TaskException = Utils.TaskQueue.TaskException;

    public enum DatabaseItem
    {
        Comics,
        ReadRecords,
        Favorites,
        History,
        Settings
    }

    public class Database
    {
        public static ComicData Comics = new ComicData();
        public static RecentReadData RecentRead = new RecentReadData();
        public static FavoritesData Favorites = new FavoritesData();
        public static HistoryData History = new HistoryData();
        public static AppSettingsData AppSettings = new AppSettingsData();
    };

    public class DatabaseManager
    {
        private static bool m_database_ready = false;
        private static SemaphoreSlim m_database_semaphore = new SemaphoreSlim(1);

        public static async Task WaitLock()
        {
            await Utils.Methods.WaitFor(() => m_database_ready);
            await m_database_semaphore.WaitAsync();
        }

        public static void ReleaseLock()
        {
            m_database_semaphore.Release();
        }

        public static SealedTask SaveSealed(DatabaseItem item) => (RawTask _) => Save(item).Result;

        private static async RawTask Save(DatabaseItem item)
        {
            StorageFolder folder = ApplicationData.Current.LocalFolder;

            switch (item)
            {
                case DatabaseItem.Comics:
                    await ComicDataManager.Save(folder);
                    break;
                case DatabaseItem.ReadRecords:
                    await RecentReadDataManager.Save(folder);
                    break;
                case DatabaseItem.Favorites:
                    await FavoritesDataManager.Save(folder);
                    break;
                case DatabaseItem.History:
                    await HistoryDataManager.Save(folder);
                    break;
                case DatabaseItem.Settings:
                    await AppSettingsDataManager.Save(folder);
                    break;
                default:
                    return new TaskResult(TaskException.InvalidParameters, fatal: true);
            }

            return new TaskResult();
        }

        public static SealedTask LoadSealed() => (RawTask _) => Load().Result;

        private static async RawTask Load()
        {
            StorageFolder folder = ApplicationData.Current.LocalFolder;

            _ = await AppSettingsDataManager.Load(folder);
            _ = await ComicDataManager.Load(folder);
            _ = await RecentReadDataManager.Load(folder);
            _ = await FavoritesDataManager.Load(folder);
            _ = await HistoryDataManager.Load(folder);
            m_database_ready = true;

            // this should only be called after AppSettings loaded
            Utils.TaskQueue.TaskQueueManager.AppendTask(DatabaseManager.UpdateSealed(lazy_load: false), "",
                Utils.TaskQueue.TaskQueueManager.EmptyQueue());
            return new TaskResult();
        }

        public static SealedTask UpdateSealed(bool lazy_load = true) => (RawTask _) => Update(lazy_load).Result;

        private static async RawTask Update(bool lazy_load)
        {
            TaskResult res = await ComicDataManager.Update(lazy_load);
            await RecentReadDataManager.Update();
            return res;
        }

        public static async Task<object> TryGetFile(StorageFolder folder, string name)
        {
            IStorageItem item = await folder.TryGetItemAsync(name);

            if (item == null)
            {
                return null;
            }

            if (!item.IsOfType(StorageItemTypes.File))
            {
                return null;
            }

            return (StorageFile)item;
        }

    }
}
