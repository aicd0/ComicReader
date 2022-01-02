//#define DEBUG_LOG_SAVE

using Microsoft.Data.Sqlite;
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
        Favorites,
        History,
        AppSettings
    }

    public class Database
    {
        public static AppSettingData AppSettings = new AppSettingData();
        public static FavoriteData Favorites = new FavoriteData();
        public static HistoryData History = new HistoryData();
    };

    public abstract class AppData
    {
        public abstract string FileName { get; }
        [XmlIgnore]
        public abstract AppData Target { get; set; }
        public abstract void Pack();
        public abstract void Unpack();
    }

    public class Key
    {
        public Key(string name, object value = null, bool blob = false)
        {
            Name = name;
            Value = value;
            IsBlob = blob;
        }

        public string Name;
        public object Value;
        public bool IsBlob;
    }

    public class DatabaseManager
    {
        private static bool m_database_ready = false;
        private static SemaphoreSlim m_database_semaphore = new SemaphoreSlim(1);
        private static SqliteConnection m_connection = null;
        public static SqliteConnection Connection => m_connection;
        private static StorageFolder DatabaseFolder => ApplicationData.Current.LocalFolder;
        private static string DatabaseFileName => "database.db";
        private static string DatabasePath => Path.Combine(DatabaseFolder.Path, DatabaseFileName);

        public static string ComicTable
        {
            get
            {
                return "comics";
            }
        }

        public static async Task Init()
        {
            // Create database.
            await DatabaseFolder.CreateFileAsync(DatabaseFileName, CreationCollisionOption.OpenIfExists);
            
            // Create tables.
            SqliteConnection connection =
                new SqliteConnection($"Filename={DatabasePath}");
            connection.Open();
            SqliteCommand command = connection.CreateCommand();

            command.CommandText = "CREATE TABLE IF NOT EXISTS " + ComicTable + " (" +
                ComicData.FieldId + " INTEGER PRIMARY KEY AUTOINCREMENT," + // 0
                ComicData.FieldTitle1 + " TEXT," + // 1
                ComicData.FieldTitle2 + " TEXT," + // 2
                ComicData.FieldDirectory + " TEXT NOT NULL," + // 3
                ComicData.FieldHidden + " BOOLEAN NOT NULL," + // 4
                ComicData.FieldRating + " INTEGER NOT NULL," + // 5
                ComicData.FieldProgress + " INTEGER NOT NULL," + // 6
                ComicData.FieldLastVisit + " TIMESTAMP NOT NULL," + // 7
                ComicData.FieldLastPosition + " REAL DEFAULT 0 NOT NULL," + // 8
                ComicData.FieldCoverFileName + " TEXT," + // 9
                ComicData.FieldTags + " BLOB," + // 10
                ComicData.FieldImageAspectRatios + " BLOB)"; // 11
            command.ExecuteNonQuery();

            command.Dispose();
            m_connection = connection;
        }

        public static async Task<long> Insert(string table, List<Key> keys)
        {
            List<string> field_names = new List<string>();
            List<string> field_vals = new List<string>();
            var blobs = new List<KeyValuePair<string, MemoryStream>>();
            SqliteCommand command = Connection.CreateCommand();

            foreach (Key key in keys)
            {
                field_names.Add(key.Name);

                if (key.IsBlob)
                {
                    MemoryStream stream = Utils.Methods.SerializeToMemoryStream(key.Value);
                    blobs.Add(new KeyValuePair<string, MemoryStream>(key.Name, stream));

                    string param = "$len_" + key.Name;
                    field_vals.Add("zeroblob(" + param + ")");
                    command.Parameters.AddWithValue(param, stream.Length);
                }
                else
                {
                    string param = "@" + key.Name;
                    field_vals.Add(param);
                    command.Parameters.AddWithValue(param, key.Value);
                }
            }

            command.CommandText = "INSERT INTO " + table + " (" +
                string.Join(',', field_names) + ") VALUES (" +
                string.Join(',', field_vals) + ");" +
                "SELECT LAST_INSERT_ROWID();";

            await ComicDataManager.WaitLock(); // Lock on.
            long rowid = (long)command.ExecuteScalar();

            // Copy to blobs.
            foreach (var pairs in blobs)
            {
                MemoryStream input_stream = pairs.Value;
                input_stream.Seek(0, SeekOrigin.Begin);

                using (SqliteBlob write_stream = new SqliteBlob(
                    Connection, table, pairs.Key, rowid))
                {
                    await input_stream.CopyToAsync(write_stream);
                }
            }
            ComicDataManager.ReleaseLock(); // Lock off.

            command.Dispose();
            return rowid;
        }

        public static async Task Update(string table, Key primary_key, List<Key> keys)
        {
            if (keys.Count == 0) return;

            List<string> fields = new List<string>();
            var blobs = new List<KeyValuePair<string, MemoryStream>>();
            SqliteCommand command = Connection.CreateCommand();
            command.Parameters.AddWithValue("@" + primary_key.Name, primary_key.Value);

            foreach (Key key in keys)
            {
                if (key.IsBlob)
                {
                    MemoryStream stream = Utils.Methods.SerializeToMemoryStream(key.Value);
                    blobs.Add(new KeyValuePair<string, MemoryStream>(key.Name, stream));

                    string param = "$len_" + key.Name;
                    fields.Add(key.Name + "=zeroblob(" + param + ")");
                    command.Parameters.AddWithValue(param, stream.Length);
                }
                else
                {
                    string param = "@" + key.Name;
                    fields.Add(key.Name + "=" + param);
                    command.Parameters.AddWithValue(param, key.Value);
                }
            }

            string condition = " WHERE " + primary_key.Name + "=@" + primary_key.Name;
            command.CommandText = "UPDATE " + table + " SET " +
                string.Join(',', fields) + condition;

            await ComicDataManager.WaitLock(); // Lock on.
            command.ExecuteNonQuery();

            // Copy to blobs.
            if (blobs.Count > 0)
            {
                command.CommandText = "SELECT rowid FROM " + table + condition + " LIMIT 1";
                long rowid = (long)command.ExecuteScalar();

                foreach (var pairs in blobs)
                {
                    MemoryStream input_stream = pairs.Value;
                    input_stream.Seek(0, SeekOrigin.Begin);

                    using (SqliteBlob write_stream = new SqliteBlob(
                        Connection, table, pairs.Key, rowid))
                    {
                        await input_stream.CopyToAsync(write_stream);
                    }
                }
            }
            ComicDataManager.ReleaseLock(); // Lock off.

            command.Dispose();
        }

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
#if DEBUG_LOG_SAVE
            System.Diagnostics.Debug.Print("Saving: " + item.ToString() + "\n");
#endif

            switch (item)
            {
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
            serializer.UnknownAttribute += (object x, XmlAttributeEventArgs y) => throw new Exception();
            serializer.UnknownElement += (object x, XmlElementEventArgs y) => throw new Exception();
            serializer.UnknownNode += (object x, XmlNodeEventArgs y) => throw new Exception();
            serializer.UnreferencedObject += (object x, UnreferencedObjectEventArgs y) => throw new Exception();

            try
            {
                obj.Target = serializer.Deserialize(stream.AsStream()) as AppData;
            }
            catch (Exception)
            {
                return new TaskResult(TaskException.Failure);
            }

            obj.Target.Unpack();
            stream.Dispose();
            return new TaskResult();
        }

        public static SealedTask UpdateSealed(bool lazy_load = true) =>
            (RawTask _) => Update(lazy_load).Result;

        private static async RawTask Update(bool lazy_load)
        {
            await ComicDataManager.Update(lazy_load);
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
