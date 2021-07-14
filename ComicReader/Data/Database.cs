using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;

namespace ComicReader.Data
{
    public class TagData
    {
        public string Name;
        public HashSet<string> Tags = new HashSet<string>();
    };

    public class ComicData
    {
        private const string default_title = "Untitled Collection";
        public string m_title = default_title;
        public string m_title2 = "";

        public string Id;
        public string ComicCollectionId;
        public string Title
        {
            get => m_title;
            set { m_title = value.Length == 0 ? default_title : value; }
        }
        public string Title2
        {
            get => m_title2;
            set { m_title2 = value; }
        }
        public List<TagData> Tags = new List<TagData>();
        public string Directory;
        public DateTimeOffset LastVisit = DateTimeOffset.MinValue;
        public bool Hidden = false;

        public bool IsExternal = false;
        public StorageFolder Folder;
        public List<StorageFile> ImageFiles = new List<StorageFile>();
        public StorageFile InfoFile;
    };

    class ComicRecordData
    {
        public ComicRecordData()
        {
            Id = "";
            Rating = -1;
            Progress = 0;
        }

        public string Id;
        public int Rating;
        public int Progress;
    }

    class ComicCollectionData
    {
        public ComicCollectionData()
        {
            ComicIds = new List<string>();
        }

        public string Id;
        public List<string> ComicIds;
    };

    class FavoritesNode
    {
        public string Type;
        public string Name;
        public string Id;
        public List<FavoritesNode> Children;

        public FavoritesNode()
        {
            Children = new List<FavoritesNode>();
        }
    };

    class HistoryData
    {
        public string Id;
        public DateTimeOffset DateTime;
    }

    class AppSettingsData
    {
        public List<string> ComicFolders = new List<string>();
        public bool SaveHistory = true;
    };

    class Database
    {
        public static List<ComicData> Comics = new List<ComicData>();
        public static List<ComicRecordData> ComicRecords = new List<ComicRecordData>();
        public static List<ComicCollectionData> ComicCollections = new List<ComicCollectionData>();
        public static List<FavoritesNode> Favorites = new List<FavoritesNode>();
        public static List<HistoryData> History = new List<HistoryData>();
        public static AppSettingsData AppSettings = new AppSettingsData();
    };
}
