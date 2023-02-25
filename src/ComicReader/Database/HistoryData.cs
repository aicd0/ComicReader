using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace ComicReader.Database
{
    public class HistoryData : XmlData
    {
        public List<HistoryItemData> Items = new List<HistoryItemData>();

        // serialization
        public override string FileName => "History";

        [XmlIgnore]
        public override XmlData Target
        {
            get => XmlDatabase.History;
            set => XmlDatabase.History = value as HistoryData;
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
            await XmlDatabaseManager.WaitLock();
            try
            {
                if (!XmlDatabase.Settings.SaveHistory)
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
                XmlDatabase.History.Items.Insert(0, record);
            }
            finally
            {
                XmlDatabaseManager.ReleaseLock();
            }

            if (final)
            {
                Utils.TaskQueueManager.AppendTask(
                    XmlDatabaseManager.SaveSealed(XmlDatabaseItem.History));

                if (Views.HistoryPage.Current != null)
                {
                    await Views.HistoryPage.Current.Update();
                }
            }
        }

        public static async Task Remove(long id, bool final)
        {
            await XmlDatabaseManager.WaitLock();
            RemoveNoLock(id);
            XmlDatabaseManager.ReleaseLock();

            if (final)
            {
                Utils.TaskQueueManager.AppendTask(XmlDatabaseManager.SaveSealed(XmlDatabaseItem.History));
            }
        }

        public static async Task Clear()
        {
            await XmlDatabaseManager.WaitLock();
            XmlDatabase.History.Items.Clear();
            XmlDatabaseManager.ReleaseLock();
            Utils.TaskQueueManager.AppendTask(XmlDatabaseManager.SaveSealed(XmlDatabaseItem.History));
        }

        private static void RemoveNoLock(long id)
        {
            List<HistoryItemData> items = XmlDatabase.History.Items;

            for (int i = 0; i < items.Count; ++i)
            {
                HistoryItemData record = items[i];

                if (record.Id == id)
                {
                    items.RemoveAt(i);
                    --i;
                }
            }
        }
    }
}
