using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Streams;

namespace ComicReader.Data
{
    using RawTask = Task<Utils.TaskQueue.TaskResult>;
    using SealedTask = Func<Task<Utils.TaskQueue.TaskResult>, Utils.TaskQueue.TaskResult>;
    using TaskResult = Utils.TaskQueue.TaskResult;
    using TaskException = Utils.TaskQueue.TaskException;

    public enum DatabaseItem
    {
        Comic,
        ComicExtra,
        Favorites,
        History,
        AppSettings
    }

    public class Database
    {
        public static AppSettingData AppSettings = new AppSettingData();
        public static ComicData Comic = new ComicData();
        public static ComicExtraData ComicExtra = new ComicExtraData();
        public static FavoriteData Favorites = new FavoriteData();
        public static HistoryData History = new HistoryData();
    };

    public abstract class AppData
    {
        public abstract string FileName { get; }
        public abstract void Pack();
        public abstract void Unpack();
        public abstract void Set(object obj);
    }

    public class DatabaseManager
    {
        private static bool m_database_ready = false;
        private static SemaphoreSlim m_database_semaphore = new SemaphoreSlim(1);
        private static StorageFolder DatabaseFolder => ApplicationData.Current.LocalFolder;

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
            switch (item)
            {
                case DatabaseItem.Comic:
                    await Save(Database.Comic);
                    break;
                case DatabaseItem.ComicExtra:
                    await Save(Database.ComicExtra);
                    break;
                case DatabaseItem.Favorites:
                    await Save(Database.Favorites);
                    break;
                case DatabaseItem.History:
                    await Save(Database.History);
                    break;
                case DatabaseItem.AppSettings:
                    await Save(Database.AppSettings);
                    break;
                default:
                    return new TaskResult(TaskException.InvalidParameters, fatal: true);
            }

            return new TaskResult();
        }

        private static async Task Save(AppData obj)
        {
            await WaitLock();
            StorageFile file = await DatabaseFolder.CreateFileAsync(
                obj.FileName, CreationCollisionOption.ReplaceExisting);
            IRandomAccessStream stream = await file.OpenAsync(
                FileAccessMode.ReadWrite);

            obj.Pack();
            XmlSerializer serializer = new XmlSerializer(obj.GetType());
            serializer.Serialize(stream.AsStream(), obj);

            stream.Dispose();
            ReleaseLock();
        }

        public static SealedTask LoadSealed() => (RawTask _) => Load().Result;

        private static async RawTask Load()
        {
            await Load(Database.AppSettings);
            await Load(Database.Comic);
            await Load(Database.ComicExtra);
            await Load(Database.Favorites);
            await Load(Database.History);
            m_database_ready = true;

            // this should only be called after AppSettings was loaded
            Utils.TaskQueue.TaskQueueManager.AppendTask(UpdateSealed(lazy_load: false), "",
                Utils.TaskQueue.TaskQueueManager.EmptyQueue());
            return new TaskResult();
        }

        private static async RawTask Load(AppData obj)
        {
            object file = await TryGetFile(DatabaseFolder, obj.FileName);

            if (file == null)
            {
                return new TaskResult(TaskException.FileNotExists);
            }

            IRandomAccessStream stream = await (file as StorageFile).OpenAsync(FileAccessMode.Read);
            XmlSerializer serializer = new XmlSerializer(obj.GetType());

            try
            {
                obj.Set(serializer.Deserialize(stream.AsStream()));
            }
            catch (Exception)
            {
                return new TaskResult(TaskException.Failure);
            }

            obj.Unpack();
            stream.Dispose();
            return new TaskResult();
        }

        public static SealedTask UpdateSealed(bool lazy_load = true) => (RawTask _) => Update(lazy_load).Result;

        private static async RawTask Update(bool lazy_load)
        {
            await ComicDataManager.Update(lazy_load);
            await ComicExtraDataManager.Update();
            return new TaskResult();
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
