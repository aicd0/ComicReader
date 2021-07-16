using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Graphics.Display;
using Windows.Security.Cryptography;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace ComicReader.Data
{
    public enum DatabaseItem
    {
        Comics,
        ComicRecords,
        ComicCollections,
        Favorites,
        History,
        Settings
    }

    class DataManager
    {
        private const string COMIC_DATA_FILE_NAME = "comics";
        private const string COMIC_RECORD_DATA_FILE_NAME = "records";
        private const string COMIC_COLLECTION_DATA_FILE_NAME = "comic_collections";
        private const string FAVORITES_DATA_FILE_NAME = "favorites";
        private const string HISTORY_DATA_FILE_NAME = "history";
        private const string APP_SETTINGS_DATA_FILE_NAME = "settings";
        private const string COMIC_INFO_FILE_NAME = "Info.txt";

        private static bool m_is_database_ready = false;
        public static async Task WaitForDatabaseReady()
        {
            await Task.Run(delegate
            {
                SpinWait sw = new SpinWait();
                while (!m_is_database_ready)
                {
                    sw.SpinOnce();
                }
            });
        }

        // helper functions
        private static IBuffer GetBufferFromString(string _string)
        {
            if (_string.Length == 0)
            {
                return new Windows.Storage.Streams.Buffer(0);
            }
            else
            {
                return CryptographicBuffer.ConvertStringToBinary(
                    _string, BinaryStringEncoding.Utf8);
            }
        }

        private static string GetStringFromBuffer(IBuffer buffer)
        {
            // Throws HRESULT_FROM_WIN32(ERROR_NO_UNICODE_TRANSLATION)
            // if the buffer is not properly encoded.
            return CryptographicBuffer.ConvertBinaryToString(
                BinaryStringEncoding.Utf8, buffer);
        }

        private static async Task<object> GetStringFromFile(StorageFolder userFolder, string file_name)
        {
            IStorageItem item = await userFolder.TryGetItemAsync(file_name);

            if (item == null)
            {
                return null;
            }

            if (!item.IsOfType(StorageItemTypes.File))
            {
                return null;
            }

            var file = (StorageFile)item;

            IBuffer buffer = await FileIO.ReadBufferAsync(file);
            return GetStringFromBuffer(buffer);
        }

        private static void WriteString(DataWriter data_writer, string data)
        {
            var buf = GetBufferFromString(data);
            data_writer.WriteInt32((Int32)buf.Length);
            data_writer.WriteBuffer(buf);
        }

        private static string ReadString(DataReader data_reader)
        {
            var bufLen = (uint)data_reader.ReadInt32();
            var buf = data_reader.ReadBuffer(bufLen);
            return GetStringFromBuffer(buf);
        }

        // lock
        private static SemaphoreSlim database_semaphore = new SemaphoreSlim(1);

        public static async Task WaitLock()
        {
            await database_semaphore.WaitAsync();
        }

        public static void ReleaseLock()
        {
            database_semaphore.Release();
        }

        // database saving
        public static Func<Task<int>, int> SaveDatabaseSealed(DatabaseItem item)
        {
            return delegate (Task<int> _t) {
                Task<int> task = SaveDatabase(item);
                task.Wait();
                return task.Result;
            };
        }

        private static async Task<int> SaveDatabase(DatabaseItem item)
        {
            StorageFolder folder = ApplicationData.Current.LocalFolder;

            switch (item)
            {
                case DatabaseItem.Comics:
                    await SaveComicData(folder);
                    break;
                case DatabaseItem.ComicRecords:
                    await SaveComicRecordData(folder);
                    break;
                case DatabaseItem.ComicCollections:
                    await SaveComicCollectionData(folder);
                    break;
                case DatabaseItem.Favorites:
                    await SaveFavoritesData(folder);
                    break;
                case DatabaseItem.History:
                    await SaveHistoryData(folder);
                    break;
                case DatabaseItem.Settings:
                    await SaveAppSettingsData(folder);
                    break;
                default:
                    return 1;
            }

            return 0;
        }

        private static async Task SaveComicData(StorageFolder userFolder)
        {
            await WaitLock();

            StorageFile file = await userFolder.CreateFileAsync(COMIC_DATA_FILE_NAME, CreationCollisionOption.ReplaceExisting);
            string data = "";

            for (int i = 0; i < Database.Comics.Count; ++i)
            {
                var comic = Database.Comics[i];

                if (i != 0)
                {
                    data += "\n0";
                }

                data += "1" + comic.Id;

                if (comic.ComicCollectionId != null)
                {
                    data += "\n12" + comic.ComicCollectionId;
                }

                if (comic.Title != null)
                {
                    data += "\n13" + comic.Title;
                }

                if (comic.Title2 != null)
                {
                    data += "\n14" + comic.Title2;
                }

                if (comic.Tags.Count != 0)
                {
                    data += "\n15";

                    for (int j = 0; j < comic.Tags.Count; ++j)
                    {
                        TagData tagdata = comic.Tags[j];

                        if (j != 0)
                        {
                            data += "\n2";
                        }

                        data += "1" + tagdata.Name;

                        if (tagdata.Tags.Count != 0)
                        {
                            data += "\n32" + Utils.StringUtils.Join("/", tagdata.Tags);
                        }
                    }
                }

                data += "\n16" + comic.Directory;
                data += "\n17" + comic.LastVisit.ToString();
                data += "\n18" + comic.Hidden.ToString();
            }

            IBuffer buffer = GetBufferFromString(data);
            await FileIO.WriteBufferAsync(file, buffer);

            ReleaseLock();
        }

        private static async Task SaveComicRecordData(StorageFolder userFolder)
        {
            await WaitLock();

            StorageFile file = await userFolder.CreateFileAsync(COMIC_RECORD_DATA_FILE_NAME, CreationCollisionOption.ReplaceExisting);
            string data = "";

            foreach (var record in Database.ComicRecords)
            {
                data += "\n";
                data += "\n1" + record.Id;
                data += "\n2" + record.Rating.ToString();
                data += "\n3" + record.Progress.ToString();
            }

            if (data.Length >= 2)
            {
                data = data.Substring(2);
            }

            IBuffer buffer = GetBufferFromString(data);
            await FileIO.WriteBufferAsync(file, buffer);

            ReleaseLock();
        }

        private static async Task SaveComicCollectionData(StorageFolder userFolder)
        {
            await WaitLock();

            StorageFile file = await userFolder.CreateFileAsync(COMIC_COLLECTION_DATA_FILE_NAME, CreationCollisionOption.ReplaceExisting);
            string data = "";

            foreach (var versions in Database.ComicCollections)
            {
                data += "\n";
                data += "\n1" + versions.Id;
                data += "\n2" + Utils.StringUtils.Join("/", versions.ComicIds);
            }

            if (data.Length >= 2)
            {
                data = data.Substring(2);
            }

            IBuffer buffer = GetBufferFromString(data);
            await FileIO.WriteBufferAsync(file, buffer);

            ReleaseLock();
        }

        private static async Task SaveFavoritesData(StorageFolder userFolder)
        {
            await WaitLock();

            StorageFile file = await userFolder.CreateFileAsync(FAVORITES_DATA_FILE_NAME, CreationCollisionOption.ReplaceExisting);

            string data = "";
            SaveFavoritesDataHelper(Database.Favorites, ref data);

            IBuffer buffer = GetBufferFromString(data);
            await FileIO.WriteBufferAsync(file, buffer);

            ReleaseLock();
        }

        private static void SaveFavoritesDataHelper(List<FavoritesNode> folder, ref string str)
        {
            foreach (FavoritesNode node in folder)
            {
                str += "\n0";
                str += "\n1" + node.Type;
                str += "\n2" + node.Name;
                str += "\n3" + node.Id;
                if (node.Children.Count > 0)
                {
                    str += "\n8";
                    SaveFavoritesDataHelper(node.Children, ref str);
                }
            }
            str += "\n9";
        }

        private static async Task SaveHistoryData(StorageFolder userFolder)
        {
            await WaitLock();

            StorageFile file = await userFolder.CreateFileAsync(HISTORY_DATA_FILE_NAME, CreationCollisionOption.ReplaceExisting);
            string data = "";

            foreach (var record in Database.History)
            {
                data += "\n";
                data += "\n1" + record.Id;
                data += "\n2" + record.DateTime.ToString();
                data += "\n3" + record.Title;
            }

            if (data.Length >= 2)
            {
                data = data.Substring(2);
            }

            IBuffer buffer = GetBufferFromString(data);
            await FileIO.WriteBufferAsync(file, buffer);

            ReleaseLock();
        }

        private static async Task SaveAppSettingsData(StorageFolder userFolder)
        {
            await WaitLock();

            StorageFile file = await userFolder.CreateFileAsync(APP_SETTINGS_DATA_FILE_NAME, CreationCollisionOption.ReplaceExisting);

            var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
            var outputStream = stream.GetOutputStreamAt(0);
            var dataWriter = new DataWriter(outputStream);

            WriteString(dataWriter, "1.0.0");
            dataWriter.WriteBoolean(Database.AppSettings.SaveHistory);

            dataWriter.WriteInt32((Int32)Database.AppSettings.ComicFolders.Count);
            foreach (string folder in Database.AppSettings.ComicFolders)
            {
                WriteString(dataWriter, folder);
            }

            await dataWriter.StoreAsync();
            await outputStream.FlushAsync();
            stream.Dispose();
            outputStream.Dispose();
            dataWriter.Dispose();

            ReleaseLock();
        }

        // database loading
        public static Func<Task<int>, int> LoadDatabaseSealed()
        {
            return delegate (Task<int> _t) {
                var task = LoadDatabase();
                task.Wait();
                return task.Result;
            };
        }

        private static async Task<int> LoadDatabase()
        {
            StorageFolder folder = ApplicationData.Current.LocalFolder;

            _ = await LoadAppSettingsData(folder);
            _ = await LoadComicData(folder);
            _ = await LoadComicRecordData(folder);
            _ = await LoadComicCollectionData(folder);
            _ = await LoadFavoritesData(folder);
            _ = await LoadHistoryData(folder);
            m_is_database_ready = true;

            // this should only be called asynchronously after app settings were loaded
            Utils.BackgroundTasks.AppendTask(UpdateComicDataSealed(), "", Utils.BackgroundTasks.EmptyQueue());
            return 0;
        }

        private static async Task<int> LoadComicData(StorageFolder userFolder)
        {
            await WaitLock();
            try
            {
                object data = await GetStringFromFile(userFolder, COMIC_DATA_FILE_NAME);

                if (data == null)
                {
                    return 1; // File not exist
                }

                string data_s = (string)data;
                Database.Comics.Clear();

                if (data_s == "")
                {
                    return 0;
                }

                string[] comics_str = data_s.Split("\n0");

                foreach (string comic_str in comics_str)
                {
                    string[] comic_properties = comic_str.Split("\n1");
                    var comic = new ComicData();

                    foreach (string comic_property in comic_properties)
                    {
                        char mark = comic_property[0];
                        string content = comic_property.Substring(1);
                        switch (mark)
                        {
                        case '1':
                            comic.Id = content;
                            break;
                        case '2':
                            comic.ComicCollectionId = content;
                            break;
                        case '3':
                            comic.Title = content;
                            break;
                        case '4':
                            comic.Title2 = content;
                            break;
                        case '5':
                            string[] tags_str = content.Split("\n2");
                            var tags = new List<TagData>();

                            foreach (string tag_str in tags_str)
                            {
                                string[] tag_properties = tag_str.Split("\n3");
                                var tag = new TagData();

                                foreach (string tag_property in tag_properties)
                                {
                                    mark = tag_property[0];
                                    content = tag_property.Substring(1);
                                    switch (mark)
                                    {
                                    case '1':
                                        tag.Name = content;
                                        break;
                                    case '2':
                                        tag.Tags = content.Split("/").ToHashSet();
                                        break;
                                    default:
                                        throw new Exception();
                                    }
                                }

                                tags.Add(tag);
                            }

                            comic.Tags = tags;
                            break;
                        case '6':
                            comic.Directory = content;
                            break;
                        case '7':
                            comic.LastVisit = DateTimeOffset.Parse(content);
                            break;
                        case '8':
                            comic.Hidden = bool.Parse(content);
                            break;
                        default:
                            throw new Exception();
                        }
                    }

                    Database.Comics.Add(comic);
                }

                return 0;
            }
            finally
            {
                ReleaseLock();
            }
        }

        private static async Task<int> LoadComicRecordData(StorageFolder userFolder)
        {
            await WaitLock();
            try
            {
                object data = await GetStringFromFile(userFolder, COMIC_RECORD_DATA_FILE_NAME);

                if (data == null)
                {
                    return 1; // File not exist
                }

                string data_s = (string)data;
                Database.ComicRecords.Clear();

                if (data_s == "")
                {
                    return 0;
                }

                string[] comic_records = data_s.Split("\n\n");

                foreach (string str in comic_records)
                {
                    string[] properties = str.Split("\n");

                    var comic_record = new ComicRecordData();

                    foreach (string property in properties)
                    {
                        char mark = property[0];
                        string content = property.Substring(1);
                        switch (mark)
                        {
                            case '1':
                                comic_record.Id = content;
                                break;
                            case '2':
                                comic_record.Rating = int.Parse(content);
                                break;
                            case '3':
                                comic_record.Progress = int.Parse(content);
                                break;
                            default:
                                throw new Exception();
                        }
                    }

                    Database.ComicRecords.Add(comic_record);
                }

                return 0;
            }
            finally
            {
                ReleaseLock();
            }
        }

        private static async Task<int> LoadComicCollectionData(StorageFolder userFolder)
        {
            await WaitLock();
            try
            {
                object data = await GetStringFromFile(userFolder, COMIC_COLLECTION_DATA_FILE_NAME);

                if (data == null)
                {
                    return 1; // File not exist
                }

                string data_s = (string)data;
                Database.ComicCollections.Clear();

                if (data_s == "")
                {
                    return 0;
                }

                string[] comic_collections = data_s.Split("\n\n");

                foreach (string str in comic_collections)
                {
                    string[] properties = str.Split("\n");

                    var comic_collection = new ComicCollectionData();

                    foreach (string property in properties)
                    {
                        char mark = property[0];
                        string content = property.Substring(1);
                        switch (mark)
                        {
                            case '1':
                                comic_collection.Id = content;
                                break;
                            case '2':
                                comic_collection.ComicIds = new List<string>(content.Split("/"));
                                break;
                            default:
                                throw new Exception();
                        }
                    }

                    Database.ComicCollections.Add(comic_collection);
                }

                return 0;
            }
            finally
            {
                ReleaseLock();
            }
        }

        private static async Task<int> LoadFavoritesData(StorageFolder userFolder)
        {
            await WaitLock();
            try
            {
                object data = await GetStringFromFile(userFolder, FAVORITES_DATA_FILE_NAME);
                if (data == null)
                {
                    return 1; // File not exist
                }
                string data_s = (string)data;

                Database.Favorites.Clear();

                int ps = 0;
                LoadFavoritesDataHelper(Database.Favorites, ref data_s, ref ps);

                return 0;
            }
            finally
            {
                ReleaseLock();
            }
        }

        private static void LoadFavoritesDataHelper(List<FavoritesNode> folder, ref string str, ref int ps)
        {
            FavoritesNode node = null;

            for (; ; )
            {
                if (++ps >= str.Length)
                {
                    break;
                }
                char mark = str[ps++];

                int p_end = str.IndexOf("\n", ps);
                if (p_end == -1)
                {
                    p_end = str.Length;
                }
                string content = str.Substring(ps, p_end - ps);
                ps = p_end;

                switch (mark)
                {
                case '0':
                    if (node != null)
                    {
                        folder.Add(node);
                    }
                    node = new FavoritesNode();
                    break;
                case '1':
                    if (node == null)
                    {
                        throw new Exception();
                    }
                    node.Type = content;
                    break;
                case '2':
                    if (node == null)
                    {
                        throw new Exception();
                    }
                    node.Name = content;
                    break;
                case '3':
                    if (node == null)
                    {
                        throw new Exception();
                    }
                    node.Id = content;
                    break;
                case '8':
                    if (node == null)
                    {
                        throw new Exception();
                    }
                    LoadFavoritesDataHelper(node.Children, ref str, ref ps);
                    break;
                case '9':
                    if (node != null)
                    {
                        folder.Add(node);
                    }
                    return;
                default:
                    throw new Exception();
                }
            }
            if (node != null)
            {
                folder.Add(node);
            }
        }

        private static async Task<int> LoadHistoryData(StorageFolder userFolder)
        {
            await WaitLock();
            try
            {
                object data = await GetStringFromFile(userFolder, HISTORY_DATA_FILE_NAME);

                if (data == null)
                {
                    return 1; // File not exist
                }

                string data_s = (string)data;
                Database.History.Clear();

                if (data_s == "")
                {
                    return 0;
                }

                string[] all_records = data_s.Split("\n\n");

                foreach (string str in all_records)
                {
                    string[] properties = str.Split("\n");

                    var record = new HistoryData();

                    foreach (string property in properties)
                    {
                        char mark = property[0];
                        string content = property.Substring(1);
                        switch (mark)
                        {
                            case '1':
                                record.Id = content;
                                break;
                            case '2':
                                record.DateTime = DateTimeOffset.Parse(content);
                                break;
                            case '3':
                                record.Title = content;
                                break;
                            default:
                                throw new Exception();
                        }
                    }

                    Database.History.Add(record);
                }

                return 0;
            }
            finally
            {
                ReleaseLock();
            }
        }

        private static async Task<int> LoadAppSettingsData(StorageFolder userFolder)
        {
            await WaitLock();
            try
            {
                IStorageItem item = await userFolder.TryGetItemAsync(APP_SETTINGS_DATA_FILE_NAME);

                if (item == null)
                {
                    return 1; // File not exist
                }

                if (!item.IsOfType(StorageItemTypes.File))
                {
                    return 1; // File not exist
                }

                var file = (StorageFile)item;

                var stream = await file.OpenAsync(FileAccessMode.Read);
                var size = stream.Size;
                var inputStream = stream.GetInputStreamAt(0);
                var dataReader = new DataReader(inputStream);
                await dataReader.LoadAsync((uint)size);

                _ = ReadString(dataReader);
                Database.AppSettings.SaveHistory = dataReader.ReadBoolean();

                Database.AppSettings.ComicFolders.Clear();
                var folder_count = dataReader.ReadInt32();
                for (int i = 0; i < folder_count; ++i)
                {
                    Database.AppSettings.ComicFolders.Add(ReadString(dataReader));
                }

                return 0;
            }
            finally
            {
                ReleaseLock();
            }
        }

        // helper functions (database associated)
        public static async Task<string> GenerateComicInfoString(ComicData comic)
        {
            await WaitLock();
            try
            {
                return GenerateComicInfoStringNoLock(comic);
            }
            finally
            {
                ReleaseLock();
            }
        }

        private static string GenerateComicInfoStringNoLock(ComicData comic)
        {
            string text = "";
            text += "Title: " + comic.Title;
            text += "\nTitle2: " + comic.Title2;

            foreach (TagData tag in comic.Tags)
            {
                text += "\n" + tag.Name + ": " + Utils.StringUtils.Join("/", tag.Tags);
            }

            return text;
        }

        private static TagData GetTagData(string p)
        {
            string[] pieces = p.Split(":", 2, StringSplitOptions.RemoveEmptyEntries);

            if (pieces.Length != 2)
            {
                return null;
            }

            TagData tag_data = new TagData
            {
                Name = pieces[0]
            };

            List<string> tags = new List<string>(pieces[1].Split("/", StringSplitOptions.RemoveEmptyEntries));

            foreach (string tag in tags)
            {
                string tag_trimed = tag.Trim();

                if (tag_trimed.Length != 0)
                {
                    tag_data.Tags.Add(tag_trimed);
                }
            }

            return tag_data.Tags.Count == 0 ? null : tag_data;
        }

        public static async Task IntepretComicInfoString(string text, ComicData comic)
        {
            await WaitLock();
            IntepretComicInfoStringNoLock(text, comic);
            ReleaseLock();
        }

        private static void IntepretComicInfoStringNoLock(string text, ComicData comic)
        {
            comic.Title = "";
            comic.Title2 = "";
            comic.Tags.Clear();
            text = text.Replace('\r', '\n');
            string[] properties = text.Split("\n", StringSplitOptions.RemoveEmptyEntries);

            foreach (string property in properties)
            {
                TagData tag_data = GetTagData(property);

                if (tag_data == null)
                {
                    continue;
                }

                string name = tag_data.Name.ToLower();

                if (name == "title" || name == "t1" || name == "to")
                {
                    comic.Title = Utils.StringUtils.Join("/", tag_data.Tags);
                }
                else if (name == "title2" || name == "t2")
                {
                    comic.Title2 = Utils.StringUtils.Join("/", tag_data.Tags);
                }
                else
                {
                    // combine duplicated tag
                    bool duplicated = false;

                    foreach (TagData t in comic.Tags)
                    {
                        if (t.Name.ToLower().Equals(name))
                        {
                            duplicated = true;
                            t.Tags.UnionWith(tag_data.Tags);
                            break;
                        }
                    }

                    if (!duplicated)
                    {
                        comic.Tags.Add(tag_data);
                    }
                }
            }
        }

        private static async Task<int> CompleteComicInfoFileNoLock(ComicData comic, bool create_if_not_exists)
        {
            if (comic.InfoFile != null)
            {
                return 0;
            }

            if (comic.IsExternal)
            {
                return create_if_not_exists ? 2 : 1; // No permissions : Not found
            }

            int res = await CompleteComicFolderNoLock(comic);

            if (res != 0)
            {
                throw new Exception();
            }

            IStorageItem item = await comic.Folder.TryGetItemAsync(COMIC_INFO_FILE_NAME);

            if (item == null)
            {
                if (!create_if_not_exists)
                {
                    return 1;
                }

                comic.InfoFile = await comic.Folder.CreateFileAsync(COMIC_INFO_FILE_NAME, CreationCollisionOption.FailIfExists);
                return 0;
            }

            if (!(item is StorageFile))
            {
                return 3; // name collision
            }

            comic.InfoFile = (StorageFile)item;
            return 0;
        }

        public static Func<Task<int>, int> SaveComicInfoFileSealed(ComicData comic)
        {
            return delegate (Task<int> _t) {
                var task = SaveComicInfoFile(comic);
                task.Wait();
                return task.Result;
            };
        }

        private static async Task<int> SaveComicInfoFile(ComicData comic)
        {
            await WaitLock();
            try
            {
                return await SaveComicInfoFileNoLock(comic);
            }
            finally
            {
                ReleaseLock();
            }
        }

        private static async Task<int> SaveComicInfoFileNoLock(ComicData comic)
        {
            int res = await CompleteComicInfoFileNoLock(comic, true);

            if (res != 0)
            {
                return 1;
            }

            string text = GenerateComicInfoStringNoLock(comic);
            IBuffer buffer = GetBufferFromString(text);
            await FileIO.WriteBufferAsync(comic.InfoFile, buffer);
            return 0;
        }

        public static async Task<int> UpdateComicInfo(ComicData comic)
        {
            await WaitLock();
            try
            {
                return await UpdateComicInfoNoLock(comic);
            }
            finally
            {
                ReleaseLock();
            }
        }

        public static async Task<int> UpdateComicInfoNoLock(ComicData comic)
        {
            int res = await CompleteComicInfoFileNoLock(comic, false);

            if (res != 0)
            {
                return 0;
            }

            string content = await FileIO.ReadTextAsync(comic.InfoFile);
            IntepretComicInfoStringNoLock(content, comic);
            return 0;
        }

        private static int update_comic_data_cancel_requests = 0;
        private static SemaphoreSlim update_comic_data_semaphore = new SemaphoreSlim(1);

        public static Func<Task<int>, int> UpdateComicDataSealed()
        {
            return delegate (Task<int> _t) {
                return UpdateComicData().Result;
            };
        }

        private static async Task<int> UpdateComicData()
        {
            update_comic_data_cancel_requests++;
            await update_comic_data_semaphore.WaitAsync();
            update_comic_data_cancel_requests--;

            try
            {
                await WaitLock();
                // get root folders
                List<string> root_folders = new List<string>(Database.AppSettings.ComicFolders.Count);
                foreach (string folder_path in Database.AppSettings.ComicFolders)
                {
                    root_folders.Add(folder_path);
                }
                ReleaseLock();

                // get all folders and subfolders
                List<StorageFolder> all_folders = new List<StorageFolder>();
                foreach (string folder_path in root_folders)
                {
                    StorageFolder folder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(
                        Utils.StringUtils.TokenFromPath(folder_path)).AsTask();
                    QueryOptions queryOptions = new QueryOptions
                    {
                        FolderDepth = FolderDepth.Deep,
                        IndexerOption = IndexerOption.UseIndexerWhenAvailable
                    };
                    var query = folder.CreateFolderQueryWithOptions(queryOptions);
                    var folders = await query.GetFoldersAsync(); // 20 secs for 1000 folders
                    all_folders = all_folders.Concat(folders).ToList();

                    // cancel this task if more requests came in
                    if (update_comic_data_cancel_requests > 0)
                    {
                        return 1;
                    }
                }

                // extracts StorageFolder.Path into a new string list
                List<string> all_dir = new List<string>(all_folders.Count);
                for (int i = 0; i < all_folders.Count; ++i)
                {
                    all_dir.Add(all_folders[i].Path);
                }

                // cancel this task if more requests came in
                if (update_comic_data_cancel_requests > 0)
                {
                    return 1;
                }

                await WaitLock();
                // get all folder paths in database
                List<string> all_dir_in_lib = new List<string>(Database.Comics.Count);
                foreach (ComicData comic in Database.Comics)
                {
                    all_dir_in_lib.Add(comic.Directory);
                }

                // get folders added and removed
                List<string> dir_added = all_dir.Except(all_dir_in_lib).ToList();
                List<string> dir_removed = all_dir_in_lib.Except(all_dir).ToList();

                // remove from database
                foreach (var dir in dir_removed)
                {
                    RemoveComicsWithDirectoryNoLock(dir);
                }
                ReleaseLock();

                // add to database
                DateTimeOffset last_date_time = DateTimeOffset.Now;
                for (int i = 0; i < dir_added.Count; ++i)
                {
                    string dir = dir_added[i];

                    // exclude folders which do not directly contain images // 120 secs for 1000 folders
                    QueryOptions queryOptions = new QueryOptions
                    {
                        FolderDepth = FolderDepth.Shallow,
                        IndexerOption = IndexerOption.UseIndexerWhenAvailable,
                        FileTypeFilter = { ".jpg", ".jpeg", ".png", ".bmp" }
                    };

                    StorageFolder folder = await Utils.Methods.TryGetFolder(dir);
                    if (folder == null)
                    {
                        continue;
                    }

                    var query = folder.CreateFileQueryWithOptions(queryOptions);
                    uint file_count = await query.GetItemCountAsync();
                    if (file_count == 0)
                    {
                        continue;
                    }

                    // cancel this task if more requests came in
                    if (update_comic_data_cancel_requests > 0)
                    {
                        return 1;
                    }

                    await WaitLock ();
                    ComicData comic = AddNewComicNoLock();
                    comic.Directory = dir;
                    comic.Folder = folder;

                    TagData default_tag = new TagData
                    {
                        Name = "Default",
                        Tags = dir.Split("\\").ToHashSet()
                    };
                    comic.Tags.Add(default_tag);

                    await UpdateComicInfoNoLock(comic);

                    ReleaseLock();

                    // save for each 5 secs
                    if ((DateTimeOffset.Now - last_date_time).Seconds > 5.0)
                    {
                        Utils.BackgroundTasks.AppendTask(SaveDatabaseSealed(DatabaseItem.Comics));
                        last_date_time = DateTimeOffset.Now;
                    }
                }

                // save
                Utils.BackgroundTasks.AppendTask(SaveDatabaseSealed(DatabaseItem.Comics));
                return 0;
            }
            finally
            {
                update_comic_data_semaphore.Release();
            }
        }

        /*//public static async Task<int> FixComicCollectionDataAsync()
        //{
        //    await Task.Run(()=>{}); // Make compiler happy.

        //    bool completed = false;
        //    for (int last_pos = 0; !completed;)
        //    {
        //        int i = last_pos;

        //        completed = true;
        //        for (; i < Database.ComicCollections.Count; ++i, ++last_pos)
        //        {
        //            var collection = Database.ComicCollections[i];

        //            for (int k = 0; k < collection.ComicIds.Count; k++)
        //            {
        //                string comic_id = collection.ComicIds[k];
        //                var comic = GetComicWithId(comic_id);
        //                if (comic != null)
        //                {
        //                    DeleteVersionFromCollection(collection.Id, comic_id);
        //                    completed = false;
        //                    break;
        //                }
        //                comic.ComicCollectionId = collection.Id;
        //            }

        //            if (!completed)
        //            {
        //                break;
        //            }
        //        }
        //    }
        //    return 0;
        //}*/
        
        // comic associated operations
        public static async Task<ComicData> GetComicWithId(string _id)
        {
            await WaitLock();
            try
            {
                return GetComicWithIdNoLock(_id);
            }
            finally
            {
                ReleaseLock();
            }
        }

        private static ComicData GetComicWithIdNoLock(string _id)
        {
            if (_id.Length == 0)
            {
                return null;
            }

            foreach (ComicData comic in Database.Comics)
            {
                if (comic.Id == _id)
                {
                    return comic;
                }
            }

            return null;
        }

        public static async Task<ComicData> GetComicWithDirectory(string dir)
        {
            await WaitLock();
            try
            {
                string dir_lower = dir.ToLower();

                foreach (ComicData comic in Database.Comics)
                {
                    if (comic.Directory.ToLower().Equals(dir_lower))
                    {
                        return comic;
                    }
                }

                return null;
            }
            finally
            {
                ReleaseLock();
            }
        }

        private static ComicData AddNewComicNoLock()
        {
            ComicData comic = new ComicData();
            string id;

            if (Database.Comics.Count != 0)
            {
                id = (int.Parse(Database.Comics[Database.Comics.Count - 1].Id) + 1).ToString();
            }
            else
            {
                id = "0";
            }

            comic.Id = id;
            comic.LastVisit = DateTimeOffset.Now;
            Database.Comics.Add(comic);
            return comic;
        }

        private static void RemoveComicsWithDirectoryNoLock(string dir)
        {
            dir = dir.ToLower();

            for (int i = 0; i < Database.Comics.Count; ++i)
            {
                var comic = Database.Comics[i];
                var current_dir = comic.Directory.ToLower();

                if (current_dir.Length < dir.Length)
                {
                    continue;
                }

                if (current_dir.Substring(0, dir.Length).Equals(dir))
                {
                    Database.Comics.RemoveAt(i);
                    --i;
                }
            }
        }

        private static async Task<int> CompleteComicFolderNoLock(ComicData comic)
        {
            if (comic.Folder != null)
            {
                return 0;
            }

            if (comic.Directory == null)
            {
                return 1; // invalid parameters
            }

            StorageFolder folder = await Utils.Methods.TryGetFolder(comic.Directory);

            if (folder == null)
            {
                return 2; // we don't have that access permissions
            }

            comic.Folder = folder;
            return 0;
        }

        public static Func<Task<int>, int> CompleteComicImagesSealed(ComicData comic)
        {
            return delegate (Task<int> _t) {
                var task = CompleteComicImages(comic);
                task.Wait();
                return task.Result;
            };
        }

        public static async Task<int> CompleteComicImages(ComicData comic)
        {
            await WaitLock();
            try
            {
                if (comic == null)
                {
                    throw new Exception();
                }

                if (comic.ImageFiles.Count != 0)
                {
                    return 0;
                }

                int res = await CompleteComicFolderNoLock(comic);

                if (res != 0)
                {
                    throw new Exception();
                }

                QueryOptions queryOptions = new QueryOptions
                {
                    FolderDepth = FolderDepth.Shallow,
                    IndexerOption = IndexerOption.UseIndexerWhenAvailable,
                    FileTypeFilter = { ".jpg", ".jpeg", ".png", ".bmp" }
                };

                var query = comic.Folder.CreateFileQueryWithOptions(queryOptions);
                var img_files = await query.GetFilesAsync();

                // sort by display name
                comic.ImageFiles = img_files.OrderBy(x => x.DisplayName, new Utils.StringUtils.FileNameComparer()).ToList();
                return 0;
            }
            finally
            {
                ReleaseLock();
            }
        }

        public static async Task HideComic(ComicData comic)
        {
            await WaitLock();
            comic.Hidden = true;
            ReleaseLock();
            Utils.BackgroundTasks.AppendTask(SaveDatabaseSealed(DatabaseItem.Comics), "Saving...");
        }

        public static async Task UnhideComic(ComicData comic)
        {
            await WaitLock();
            comic.Hidden = false;
            ReleaseLock();
            Utils.BackgroundTasks.AppendTask(SaveDatabaseSealed(DatabaseItem.Comics), "Saving...");
        }

        // comic record associated operations
        public static async Task<ComicRecordData> GetComicRecordWithId(string _id)
        {
            await WaitLock();
            try
            {
                return GetComicRecordWithIdNoLock(_id);
            }
            finally
            {
                ReleaseLock();
            }
        }

        private static ComicRecordData GetComicRecordWithIdNoLock(string _id)
        {
            if (_id.Length == 0)
            {
                return null;
            }

            foreach (ComicRecordData record in Database.ComicRecords)
            {
                if (record.Id == _id)
                {
                    return record;
                }
            }

            return null;
        }

        // comic collection associated operations
        public static async Task<ComicCollectionData> GetComicCollectionWithId(string _id)
        {
            await WaitLock();
            try
            {
                return GetComicCollectionWithIdNoLock(_id);
            }
            finally
            {
                ReleaseLock();
            }
        }

        private static ComicCollectionData GetComicCollectionWithIdNoLock(string _id)
        {
            if (_id.Length == 0)
            {
                return null;
            }

            foreach (ComicCollectionData collection in Database.ComicCollections)
            {
                if (collection.Id == _id)
                {
                    return collection;
                }
            }

            return null;
        }

        public static async Task<bool> DeleteComicCollection(string _id)
        {
            await WaitLock();
            try
            {
                return DeleteComicCollectionNoLock(_id);
            }
            finally
            {
                ReleaseLock();
            }
        }

        private static bool DeleteComicCollectionNoLock(string _id)
        {
            for (int i = 0; i < Database.ComicCollections.Count; ++i)
            {
                var collection = Database.ComicCollections[i];

                if (collection.Id == _id)
                {
                    foreach (string id in collection.ComicIds)
                    {
                        var comic = GetComicWithIdNoLock(id);

                        if (comic != null)
                        {
                            comic.ComicCollectionId = "";
                        }
                    }

                    Database.ComicCollections.RemoveAt(i);

                    return true;
                }
            }
            return false;
        }

        public static async Task<bool> DeleteVersionFromCollection(string collection_id, string version_id)
        {
            await WaitLock();
            try
            {
                var collection = GetComicCollectionWithIdNoLock(collection_id);

                if (collection == null)
                {
                    return false;
                }

                for (int i = 0; i < collection.ComicIds.Count; ++i)
                {
                    if (collection.ComicIds[i] == version_id)
                    {
                        collection.ComicIds.RemoveAt(i);

                        var comic = GetComicWithIdNoLock(version_id);
                        if (comic != null)
                        {
                            comic.ComicCollectionId = "";
                        }

                        if (collection.ComicIds.Count <= 1)
                        {
                            DeleteComicCollectionNoLock(collection_id);
                        }

                        return true;
                    }
                }

                return false;
            }
            finally
            {
                ReleaseLock();
            }
        }

        // favorites associated operations
        public static async Task<FavoritesNode> GetFavoriteWithId(string id)
        {
            await WaitLock();
            try
            {
                return GetFavoriteWithIdNoLock(id);
            }
            finally
            {
                ReleaseLock();
            }
        }

        private static FavoritesNode GetFavoriteWithIdNoLock(string id)
        {
            return GetFavoriteWithIdHelper(id, Database.Favorites);
        }

        private static FavoritesNode GetFavoriteWithIdHelper(string id, List<FavoritesNode> e)
        {
            foreach (var node in e)
            {
                if (node.Type == "i")
                {
                    if (node.Id == id)
                    {
                        return node;
                    }
                }
                else
                {
                    if (!(node.Children.Count == 0))
                    {
                        var result = GetFavoriteWithIdHelper(id, node.Children);

                        if (result != null)
                        {
                            return result;
                        }
                    }
                }
            }

            return null;
        }

        public static async Task<bool> RemoveFromFavoritesWithId(string id, bool is_final)
        {
            await WaitLock();
            bool res = RemoveFromFavoritesWithIdHelper(id, Database.Favorites);
            ReleaseLock();

            if (is_final)
            {
                await UpdateFavorites();
            }

            return res;
        }

        private static bool RemoveFromFavoritesWithIdHelper(string id, List<FavoritesNode> e)
        {
            for (int i = 0; i < e.Count; ++i)
            {
                var node = e[i];

                if (node.Type == "i")
                {
                    if (node.Id == id)
                    {
                        e.RemoveAt(i);
                        return true;
                    }
                }
                else
                {
                    if (!(node.Children.Count == 0))
                    {
                        if (RemoveFromFavoritesWithIdHelper(id, node.Children))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public static async Task AddToFavorites(string id, string title, bool is_final)
        {
            await WaitLock();
            try
            {
                if (GetFavoriteWithIdNoLock(id) != null)
                {
                    return;
                }

                FavoritesNode node = new FavoritesNode
                {
                    Type = "i",
                    Name = title,
                    Id = id
                };
                Database.Favorites.Add(node);
            }
            finally
            {
                ReleaseLock();
            }

            if (is_final)
            {
                await UpdateFavorites();
            }
        }

        private static async Task UpdateFavorites()
        {
            Utils.BackgroundTasks.AppendTask(SaveDatabaseSealed(DatabaseItem.Favorites));

            if (Views.FavoritesPage.Current != null)
            {
                await Views.FavoritesPage.Current.UpdateTreeExplorer();
            }
        }

        // history associated operations
        public static async Task AddToHistory(string id, string title, bool is_final)
        {
            await WaitLock();
            try
            {
                if (!Database.AppSettings.SaveHistory)
                {
                    return;
                }

                Calendar calendar = new Calendar();
                var datetime = calendar.GetDateTime();

                HistoryData record = new HistoryData();
                record.Id = id;
                record.DateTime = datetime;
                record.Title = title;

                RemoveFromHistoryNoLock(id);
                Database.History.Insert(0, record);
            }
            finally
            {
                ReleaseLock();
            }

            if (is_final)
            {
                Utils.BackgroundTasks.AppendTask(SaveDatabaseSealed(DatabaseItem.History));

                if (Views.HistoryPage.Current != null)
                {
                    await Views.HistoryPage.Current.UpdateHistory();
                }
            }
        }

        public static async Task RemoveFromHistory(string id, bool is_final)
        {
            await WaitLock();
            RemoveFromHistoryNoLock(id);
            ReleaseLock();

            if (is_final)
            {
                Utils.BackgroundTasks.AppendTask(SaveDatabaseSealed(DatabaseItem.History));
            }
        }

        private static void RemoveFromHistoryNoLock(string id)
        {
            for (int i = 0; i < Database.History.Count; ++i)
            {
                HistoryData record = Database.History[i];

                if (record.Id.Equals(id))
                {
                    Database.History.RemoveAt(i);
                    --i;
                }
            }
        }

        // settings associated operations
        public static async Task<int> AddToComicFolders(string new_folder, bool is_final)
        {
            await WaitLock();
            try
            {
                string new_folder_lower = new_folder.ToLower();
                bool new_folder_inserted = false;

                foreach (string old_folder in Database.AppSettings.ComicFolders)
                {
                    string old_folder_lower = old_folder.ToLower();

                    if (new_folder_lower.Length > old_folder_lower.Length)
                    {
                        if (new_folder_lower.Substring(0, old_folder_lower.Length).Equals(old_folder_lower))
                        {
                            return 1;
                        }
                    }
                    else if (new_folder_lower.Length < old_folder_lower.Length)
                    {
                        if (old_folder_lower.Substring(0, new_folder_lower.Length).Equals(new_folder_lower))
                        {
                            Database.AppSettings.ComicFolders[Database.AppSettings.ComicFolders.IndexOf(old_folder)] = new_folder;
                            new_folder_inserted = true;
                            break;
                        }
                    }
                    else if (new_folder_lower.Equals(old_folder_lower))
                    {
                        return 1; // already existed
                    }
                }

                if (!new_folder_inserted)
                {
                    Database.AppSettings.ComicFolders.Add(new_folder);
                }
            }
            finally
            {
                ReleaseLock();
            }

            if (is_final)
            {
                Utils.BackgroundTasks.AppendTask(SaveDatabaseSealed(DatabaseItem.Settings));
            }

            return 0;
        }

        // utilities
        public static async Task<bool> UtilsAddToComicFoldersUsingPicker()
        {
            FolderPicker picker = new FolderPicker();
            _ = picker.FileTypeFilter.Append("*");
            StorageFolder folder = await picker.PickSingleFolderAsync();

            if (folder == null)
            {
                return false;
            }

            StorageApplicationPermissions.FutureAccessList.AddOrReplace(
                Utils.StringUtils.TokenFromPath(folder.Path), folder);
            int res = await AddToComicFolders(folder.Path, true);
            if (res != 0)
            {
                return false;
            }
            Utils.BackgroundTasks.AppendTask(UpdateComicDataSealed(), "", Utils.BackgroundTasks.EmptyQueue());
            return true;
        }

        public static async Task UtilsRemoveFromFolders(string folder)
        {
            await WaitLock();
            _ = Database.AppSettings.ComicFolders.Remove(folder);
            ReleaseLock();
            Utils.BackgroundTasks.AppendTask(SaveDatabaseSealed(DatabaseItem.Settings));
            Utils.BackgroundTasks.AppendTask(UpdateComicDataSealed(), "", Utils.BackgroundTasks.EmptyQueue());

            string token = Utils.StringUtils.TokenFromPath(Utils.StringUtils.TokenFromPath(folder));
            if (StorageApplicationPermissions.FutureAccessList.ContainsItem(token))
            {
                StorageApplicationPermissions.FutureAccessList.Remove(token);
            }
        }

        public static async Task UtilsLoadImages(IEnumerable<SearchResultData> items,
            double max_width, double max_height, Utils.CancellationLock cancellation_lock)
        {
            // check parameters
            if (double.IsNaN(max_width) || double.IsNaN(max_height))
            {
                throw new Exception("Invalid parameters");
            }
            if (double.IsInfinity(max_width) && double.IsInfinity(max_height))
            {
                throw new Exception("Invalid parameters");
            }

            double raw_pixels_per_view_pixel = DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel;
            double frame_ratio = max_width / max_height;

            foreach (SearchResultData item in items)
            {
                if (cancellation_lock.CancellationRequested())
                {
                    return;
                }

                ComicData comic = item.Comic;
                BitmapImage image = item.Image;

                if (comic.ImageFiles.Count == 0)
                {
                    int res = await CompleteComicImages(comic);

                    if (res != 0 || comic.ImageFiles.Count == 0)
                    {
                        continue;
                    }
                }

                StorageFile cover_image_file = comic.ImageFiles[0];
                IRandomAccessStream stream = await cover_image_file.OpenAsync(FileAccessMode.Read);
                await image.SetSourceAsync(stream);

                double image_ratio = (double)image.PixelWidth / image.PixelHeight;
                double image_height;
                double image_width;
                if (image_ratio > frame_ratio)
                {
                    image_width = max_width * raw_pixels_per_view_pixel;
                    image_height = image_width / image_ratio;
                }
                else
                {
                    image_height = max_height * raw_pixels_per_view_pixel;
                    image_width = image_height * image_ratio;
                }
                image.DecodePixelHeight = (int)image_height;
                image.DecodePixelWidth = (int)image_width;
                item.IsImageLoaded = true;
            }
        }
    }
}