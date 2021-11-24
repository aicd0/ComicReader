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

    public class HistoryData : AppData
    {
        public List<HistoryItemData> Items = new List<HistoryItemData>();

        // serialization
        public override string FileName => "History";

        [XmlIgnore]
        public override AppData Target
        {
            get => Database.History;
            set
            {
                Database.History = value as HistoryData;
            }
        }

        public override void Pack()
        {
            foreach (HistoryItemData i in Items)
            {
                i.Pack();
            }
        }

        public override void Unpack()
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
                    await Views.HistoryPage.Current.Update();
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
