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

    public abstract class XmlData
    {
        public abstract string FileName { get; }
        [XmlIgnore]
        public abstract XmlData Target { get; set; }

        public abstract void Pack();
        public abstract void Unpack();
    }

    public class XmlDatabase
    {
        public static SettingData Settings = new SettingData();
        public static FavoriteData Favorites = new FavoriteData();
        public static HistoryData History = new HistoryData();
    };

    public enum XmlDatabaseItem
    {
        Favorites,
        History,
        Settings
    }

    public class XmlDatabaseManager
    {
        private static StorageFolder DatabaseFolder => ApplicationData.Current.LocalFolder;

        private static bool m_database_ready = false;
        private static SemaphoreSlim m_database_lock = new SemaphoreSlim(1);

        public static async Task WaitLock()
        {
            await Utils.C0.WaitFor(() => m_database_ready);
            await m_database_lock.WaitAsync();
        }

        public static void ReleaseLock()
        {
            m_database_lock.Release();
        }

        public static async RawTask Load()
        {
            await _Load(XmlDatabase.Settings);
            await _Load(XmlDatabase.Favorites);
            await _Load(XmlDatabase.History);

            m_database_ready = true;
            return new TaskResult();
        }

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptions]
        private static async RawTask _Load(XmlData obj)
        {
            object file = await Utils.C0.TryGetFile(DatabaseFolder, obj.FileName);

            if (file == null)
            {
                return new TaskResult(TaskException.FileNotExists);
            }

            IRandomAccessStream stream = await (file as StorageFile).OpenAsync(FileAccessMode.Read);
            XmlSerializer serializer = new XmlSerializer(obj.GetType());
            serializer.UnknownAttribute += (object x, XmlAttributeEventArgs y) => Log("UnknownAttribute: " + y.ToString());
            serializer.UnknownElement += (object x, XmlElementEventArgs y) => Log("UnknownElement: " + y.ToString());
            serializer.UnknownNode += (object x, XmlNodeEventArgs y) => Log("UnknownNode: " + y.ToString());
            serializer.UnreferencedObject += (object x, UnreferencedObjectEventArgs y) => Log("UnreferencedObject: " + y.ToString());

            try
            {
                obj.Target = serializer.Deserialize(stream.AsStream()) as XmlData;
            }
            catch (Exception)
            {
                return new TaskResult(TaskException.Failure);
            }

            obj.Target.Unpack();
            stream.Dispose();
            return new TaskResult();
        }

        public static SealedTask SaveSealed(XmlDatabaseItem item) =>
            (RawTask _) => SaveUnsealed(item).Result;

        public static async RawTask SaveUnsealed(XmlDatabaseItem item)
        {
            Utils.Debug.Log("Saving: " + item.ToString());

            switch (item)
            {
                case XmlDatabaseItem.Favorites:
                    await _Save(XmlDatabase.Favorites);
                    break;
                case XmlDatabaseItem.History:
                    await _Save(XmlDatabase.History);
                    break;
                case XmlDatabaseItem.Settings:
                    await _Save(XmlDatabase.Settings);
                    break;
                default:
                    return new TaskResult(TaskException.InvalidParameters, fatal: true);
            }

            return new TaskResult();
        }

        private static async Task _Save(XmlData obj)
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

        private static void Log(string content)
        {
            Utils.Debug.Log("XmlDatabase: " + content);
        }
    }
}
