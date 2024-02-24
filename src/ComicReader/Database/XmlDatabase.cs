using ComicReader.Utils;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Windows.Storage;
using Windows.Storage.Streams;
using SealedTask = System.Func<System.Threading.Tasks.Task<ComicReader.Utils.TaskException>, ComicReader.Utils.TaskException>;

namespace ComicReader.Database
{
    public abstract class XmlData
    {
        public abstract string FileName { get; }
        [XmlIgnore]
        public abstract XmlData Target { get; set; }

        public virtual void Pack() { }
        public virtual void Unpack() { }
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

        public static async Task<TaskException> Load()
        {
            await Load(XmlDatabase.Settings);
            await Load(XmlDatabase.Favorites);
            await Load(XmlDatabase.History);

            m_database_ready = true;
            return TaskException.Success;
        }

        private static async Task<TaskException> Load(XmlData obj)
        {
            object file = await Utils.Storage.TryGetFile(DatabaseFolder, obj.FileName);
            if (file == null)
            {
                return TaskException.FileNotFound;
            }

            IRandomAccessStream stream = await (file as StorageFile).OpenAsync(FileAccessMode.Read);
            var serializer = new XmlSerializer(obj.GetType());
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
                return TaskException.Failure;
            }

            obj.Target.Unpack();
            stream.Dispose();
            return TaskException.Success;
        }

        public static SealedTask SaveSealed(XmlDatabaseItem item) =>
            (Task<TaskException> _) => SaveUnsealed(item).Result;

        public static async Task<TaskException> SaveUnsealed(XmlDatabaseItem item)
        {
            Utils.Debug.Log("Saving: " + item.ToString());
            switch (item)
            {
                case XmlDatabaseItem.Favorites:
                    await Save(XmlDatabase.Favorites);
                    break;
                case XmlDatabaseItem.History:
                    await Save(XmlDatabase.History);
                    break;
                case XmlDatabaseItem.Settings:
                    await Save(XmlDatabase.Settings);
                    break;
                default:
                    Utils.Debug.LogException("SaveXmlDatabaseUnknownItem", null);
                    return TaskException.InvalidParameters;
            }

            return TaskException.Success;
        }

        private static async Task Save(XmlData obj)
        {
            await WaitLock();
            StorageFile file = await DatabaseFolder.CreateFileAsync(
                obj.FileName, CreationCollisionOption.ReplaceExisting);
            IRandomAccessStream stream = await file.OpenAsync(
                FileAccessMode.ReadWrite);

            obj.Pack();
            var serializer = new XmlSerializer(obj.GetType());
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
