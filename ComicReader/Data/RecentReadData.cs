using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ComicReader.Data
{
    using RawTask = Task<Utils.TaskQueue.TaskResult>;
    using SealedTask = Func<Task<Utils.TaskQueue.TaskResult>, Utils.TaskQueue.TaskResult>;
    using TaskResult = Utils.TaskQueue.TaskResult;
    using TaskException = Utils.TaskQueue.TaskException;

    public class RecentReadData
    {
        public List<RecentReadItemData> Items = new List<RecentReadItemData>();

        public void Pack() { }

        public void Unpack() { }
    }

    public class RecentReadItemData
    {
        public string Id = "";
        public int Rating = -1;
        public int Progress = 0;
    }

    class RecentReadDataManager
    {
        private const string RECENT_READ_DATA_FILE_NAME = "rec";

        public static async Task Save(StorageFolder user_folder)
        {
            await DatabaseManager.WaitLock();
            StorageFile file = await user_folder.CreateFileAsync(
                RECENT_READ_DATA_FILE_NAME, CreationCollisionOption.ReplaceExisting);
            IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite);

            Database.RecentRead.Pack();
            XmlSerializer serializer = new XmlSerializer(typeof(RecentReadData));
            serializer.Serialize(stream.AsStream(), Database.RecentRead);

            stream.Dispose();
            DatabaseManager.ReleaseLock();
        }

        public static async RawTask Load(StorageFolder user_folder)
        {
            object file = await DatabaseManager.TryGetFile(user_folder, RECENT_READ_DATA_FILE_NAME);

            if (file == null)
            {
                return new TaskResult(TaskException.FileNotExists);
            }

            IRandomAccessStream stream =
                await ((StorageFile)file).OpenAsync(FileAccessMode.Read);

            XmlSerializer serializer = new XmlSerializer(typeof(RecentReadData));
            Database.RecentRead = (RecentReadData)serializer.Deserialize(stream.AsStream());
            Database.RecentRead.Unpack();

            stream.Dispose();
            return new TaskResult();
        }

        public static async Task<RecentReadItemData> FromId(string id, bool create_if_not_exists = false)
        {
            await DatabaseManager.WaitLock();

            try
            {
                return FromIdNoLock(id, create_if_not_exists);
            }
            finally
            {
                DatabaseManager.ReleaseLock();
            }
        }

        private static RecentReadItemData FromIdNoLock(string id, bool create_if_not_exists)
        {
            if (id.Length == 0)
            {
                return null;
            }

            foreach (RecentReadItemData record in Database.RecentRead.Items)
            {
                if (record.Id == id)
                {
                    return record;
                }
            }

            if (!create_if_not_exists)
            {
                return null;
            }

            RecentReadItemData item = new RecentReadItemData
            {
                Id = id,
            };

            Database.RecentRead.Items.Add(item);
            return item;
        }
    }
}
