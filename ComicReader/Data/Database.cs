using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Windows.Storage;
using Windows.Storage.AccessCache;

namespace ComicReader.Data
{
    public class TagData
    {
        [XmlAttribute]
        public string Name;
        public HashSet<string> Tags = new HashSet<string>();
    };

    public class ComicData
    {
        public List<ComicItemData> Items = new List<ComicItemData>();

        public void Pack()
        {
            foreach (ComicItemData i in Items)
            {
                i.Pack();
            }
        }

        public void Unpack()
        {
            foreach (ComicItemData i in Items)
            {
                i.Unpack();
            }
        }
    }

    public class ComicItemData
    {
        private const string default_title = "Untitled Collection";
        [XmlAttribute]
        public string m_title = default_title;
        [XmlAttribute]
        public string m_title2 = "";

        [XmlAttribute]
        public string Id;
        [XmlAttribute]
        public string ComicCollectionId;
        public List<TagData> Tags = new List<TagData>();
        [XmlAttribute]
        public string Directory;
        [XmlIgnore]
        public DateTimeOffset LastVisit = DateTimeOffset.MinValue;
        [XmlAttribute]
        public string LastVisitStr;
        [XmlAttribute]
        public bool Hidden = false;

        [XmlIgnore]
        public string Title
        {
            get => m_title;
            set { m_title = value.Length == 0 ? default_title : value; }
        }
        [XmlIgnore]
        public string Title2
        {
            get => m_title2;
            set { m_title2 = value; }
        }
        [XmlIgnore]
        public bool IsExternal = false;
        [XmlIgnore]
        public StorageFolder Folder;
        [XmlIgnore]
        public List<StorageFile> ImageFiles = new List<StorageFile>();
        [XmlIgnore]
        public StorageFile InfoFile;

        public void Pack()
        {
            LastVisitStr = LastVisit.ToString();
        }

        public void Unpack()
        {
            LastVisit = DateTimeOffset.Parse(LastVisitStr);
        }
    };

    public class ReadRecordsData
    {
        public List<ReadRecordData> Items = new List<ReadRecordData>();

        public void Pack() { }

        public void Unpack() { }
    }

    public class ReadRecordData
    {
        public string Id = "";
        public int Rating = -1;
        public int Progress = 0;
    }

    public class FavoritesData
    {
        public List<FavoritesNodeData> RootNodes = new List<FavoritesNodeData>();

        public void Pack() { }

        public void Unpack() { }
    }

    public class FavoritesNodeData
    {
        [XmlAttribute]
        public string Type;
        [XmlAttribute]
        public string Name;
        [XmlAttribute]
        public string Id;
        public List<FavoritesNodeData> Children = new List<FavoritesNodeData>();
    };

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

    public class SettingsData
    {
        public List<string> ComicFolders = new List<string>();
        public bool LeftToRight = false;
        public bool SaveHistory = true;

        public void Pack() { }

        public void Unpack() { }
    };

    public class Database
    {
        public static ComicData Comics = new ComicData();
        public static ReadRecordsData ComicRecords = new ReadRecordsData();
        public static FavoritesData Favorites = new FavoritesData();
        public static HistoryData History = new HistoryData();
        public static SettingsData AppSettings = new SettingsData();
    };
}
