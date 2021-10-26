using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Windows.Globalization;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ComicReader.Data
{
    using RawTask = Task<Utils.TaskQueue.TaskResult>;
    using SealedTask = Func<Task<Utils.TaskQueue.TaskResult>, Utils.TaskQueue.TaskResult>;
    using TaskResult = Utils.TaskQueue.TaskResult;
    using TaskException = Utils.TaskQueue.TaskException;

    public class HistoryData
    {
        public List<HistoryItemData> Items = new List<HistoryItemData>();

        public void Pack()
        {
            foreach (HistoryItemData i in Items)
            {
                i.Pack();
            }
        }

        public void Unpack()
        {
            foreach (HistoryItemData i in Items)
            {
                i.Unpack();
            }
        }
    }

    public class HistoryItemData
    {
        [XmlAttribute]
        public string Id;
        [XmlAttribute]
        public string Title;
        [XmlIgnore]
        public DateTimeOffset DateTime;
        [XmlAttribute]
        public string DateTimePack;

        public void Pack()
        {
            DateTimePack = DateTime.ToString();
        }

        public void Unpack()
        {
            DateTime = DateTimeOffset.Parse(DateTimePack);
        }
    }

    class HistoryDataManager
    {
        private const string HISTORY_DATA_FILE_NAME = "his";

        public static async Task Save(StorageFolder user_folder)
        {
            await DatabaseManager.WaitLock();
            StorageFile file = await user_folder.CreateFileAsync(
                HISTORY_DATA_FILE_NAME, CreationCollisionOption.ReplaceExisting);
            IRandomAccessStream stream = await file.OpenAsync(
                FileAccessMode.ReadWrite);

            Database.History.Pack();
            XmlSerializer serializer = new XmlSerializer(typeof(HistoryData));
            serializer.Serialize(stream.AsStream(), Database.History);

            stream.Dispose();
            DatabaseManager.ReleaseLock();
        }

        public static async RawTask Load(StorageFolder user_folder)
        {
            object file = await DatabaseManager.TryGetFile(user_folder, HISTORY_DATA_FILE_NAME);

            if (file == null)
            {
                return new TaskResult(TaskException.FileNotExists);
            }

            IRandomAccessStream stream =
                await ((StorageFile)file).OpenAsync(FileAccessMode.Read);

            XmlSerializer serializer = new XmlSerializer(typeof(HistoryData));
            Database.History =
                (HistoryData)serializer.Deserialize(stream.AsStream());
            Database.History.Unpack();

            stream.Dispose();
            return new TaskResult();
        }

        public static async Task Add(string id, string title, bool final)
        {
            await DatabaseManager.WaitLock();
            try
            {
                if (!Database.AppSettings.SaveHistory)
                {
                    return;
                }

                Calendar calendar = new Calendar();
                var datetime = calendar.GetDateTime();

                HistoryItemData record = new HistoryItemData();
                record.Id = id;
                record.DateTime = datetime;
                record.Title = title;

                RemoveNoLock(id);
                Database.History.Items.Insert(0, record);
            }
            finally
            {
                DatabaseManager.ReleaseLock();
            }

            if (final)
            {
                Utils.TaskQueue.TaskQueueManager.AppendTask(
                    DatabaseManager.SaveSealed(DatabaseItem.History));

                if (Views.HistoryPage.Current != null)
                {
                    await Views.HistoryPage.Current.UpdateHistory();
                }
            }
        }

        public static async Task Remove(string id, bool final)
        {
            await DatabaseManager.WaitLock();
            RemoveNoLock(id);
            DatabaseManager.ReleaseLock();

            if (final)
            {
                Utils.TaskQueue.TaskQueueManager.AppendTask(DatabaseManager.SaveSealed(DatabaseItem.History));
            }
        }

        private static void RemoveNoLock(string id)
        {
            List<HistoryItemData> items = Database.History.Items;

            for (int i = 0; i < items.Count; ++i)
            {
                HistoryItemData record = items[i];

                if (record.Id.Equals(id))
                {
                    items.RemoveAt(i);
                    --i;
                }
            }
        }
    }
}
