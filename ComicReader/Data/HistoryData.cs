using System;
using System.Collections.Generic;
using System.Globalization;
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
        public long Id;
        [XmlAttribute]
        public string Title;
        [XmlIgnore]
        public DateTimeOffset DateTime = DateTimeOffset.MinValue;
        [XmlAttribute]
        public string DateTimePack;

        public void Pack()
        {
            DateTimePack = DateTime.ToString(CultureInfo.InvariantCulture);
        }

        public void Unpack()
        {
            DateTime = DateTimeOffset.Parse(DateTimePack, CultureInfo.InvariantCulture);
        }
    }

    class HistoryDataManager
    {
        public static async Task Add(long id, string title, bool final)
        {
            await DatabaseManager.WaitLock();
            try
            {
                if (!Database.AppSettings.SaveHistory)
                {
                    return;
                }

                HistoryItemData record = new HistoryItemData
                {
                    Id = id,
                    DateTime = DateTimeOffset.Now,
                    Title = title
                };

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

        public static async Task Remove(long id, bool final)
        {
            await DatabaseManager.WaitLock();
            RemoveNoLock(id);
            DatabaseManager.ReleaseLock();

            if (final)
            {
                Utils.TaskQueue.TaskQueueManager.AppendTask(DatabaseManager.SaveSealed(DatabaseItem.History));
            }
        }

        private static void RemoveNoLock(long id)
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
