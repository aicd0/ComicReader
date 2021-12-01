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

    public class ComicExtraData : AppData
    {
        public List<ComicExtraItemData> Items = new List<ComicExtraItemData>();

        // serialization
        public override string FileName => "ComicExtra";

        [XmlIgnore]
        public override AppData Target
        {
            get => Database.ComicExtra;
            set
            {
                Database.ComicExtra = value as ComicExtraData;
            }
        }

        public override void Pack()
        {
            foreach (ComicExtraItemData i in Items)
            {
                i.Pack();
            }
        }

        public override void Unpack()
        {
            foreach (ComicExtraItemData i in Items)
            {
                i.Unpack();
            }
        }
    }

    public class ComicExtraItemData
    {
        [XmlAttribute]
        public string Id = "";
        [XmlAttribute]
        public int Rating = -1;
        [XmlAttribute]
        public int Progress = 0;
        [XmlIgnore]
        public DateTimeOffset LastVisit = DateTimeOffset.MinValue;
        [XmlAttribute]
        public string LastVisitPack;
        [XmlAttribute]
        public double LastPosition = 0.0;
        public List<double> ImageAspectRatios = new List<double>();

        public void Pack()
        {
            LastVisitPack = LastVisit.ToString(CultureInfo.InvariantCulture);
        }

        public void Unpack()
        {
            LastVisit = DateTimeOffset.Parse(LastVisitPack, CultureInfo.InvariantCulture);
        }
    }

    class ComicExtraDataManager
    {
        public static async Task Update()
        {
            await DatabaseManager.WaitLock();

            for (int i = Database.ComicExtra.Items.Count - 1; i >= 0; i--)
            {
                ComicExtraItemData item = Database.ComicExtra.Items[i];
                ComicItemData comic = ComicDataManager.FromIdNoLock(item.Id);

                if (comic == null)
                {
                    Database.ComicExtra.Items.RemoveAt(i);
                }
            }

            DatabaseManager.ReleaseLock();
        }

        public static async Task<ComicExtraItemData> FromId(string id, bool create_if_not_exists = false)
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

        public static ComicExtraItemData FromIdNoLock(string id, bool create_if_not_exists = false)
        {
            if (id.Length == 0)
            {
                return null;
            }

            foreach (ComicExtraItemData record in Database.ComicExtra.Items)
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

            ComicExtraItemData item = new ComicExtraItemData
            {
                Id = id,
            };

            Database.ComicExtra.Items.Add(item);
            return item;
        }
    }
}
