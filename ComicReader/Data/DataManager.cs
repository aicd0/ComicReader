﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
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
        ReadRecords,
        Favorites,
        History,
        Settings
    }

    class DataManager
    {
        private const string COMIC_DATA_FILE_NAME = "com";
        private const string READ_RECORD_DATA_FILE_NAME = "rec";
        private const string FAVORITES_DATA_FILE_NAME = "fav";
        private const string HISTORY_DATA_FILE_NAME = "his";
        private const string SETTINGS_DATA_FILE_NAME = "set";
        private const string COMIC_INFO_FILE_NAME = "Info.txt";

        private static bool m_is_database_ready = false;
        public static async Task WaitForDatabaseReady()
        {
            await Utils.Methods.WaitFor(() => m_is_database_ready);
        }

        // util functions
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

        private static async Task<object> TryGetFile(StorageFolder folder, string name)
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
        public static Func<Task<Utils.BackgroundTaskResult>, Utils.BackgroundTaskResult> SaveDatabaseSealed(DatabaseItem item)
        {
            return delegate (Task<Utils.BackgroundTaskResult> _t) {
                Task<Utils.BackgroundTaskResult> task = SaveDatabase(item);
                task.Wait();
                return task.Result;
            };
        }

        private static async Task<Utils.BackgroundTaskResult> SaveDatabase(DatabaseItem item)
        {
            StorageFolder folder = ApplicationData.Current.LocalFolder;

            switch (item)
            {
                case DatabaseItem.Comics:
                    await SaveComicData(folder);
                    break;
                case DatabaseItem.ReadRecords:
                    await SaveReadRecordData(folder);
                    break;
                case DatabaseItem.Favorites:
                    await SaveFavoritesData(folder);
                    break;
                case DatabaseItem.History:
                    await SaveHistoryItemData(folder);
                    break;
                case DatabaseItem.Settings:
                    await SaveSettingsData(folder);
                    break;
                default:
                    return new Utils.BackgroundTaskResult(Utils.BackgroundTaskExceptionType.InvalidParameters, true);
            }
            return new Utils.BackgroundTaskResult();
        }

        private static async Task SaveComicData(StorageFolder userFolder)
        {
            await WaitLock();
            StorageFile file = await userFolder.CreateFileAsync(COMIC_DATA_FILE_NAME, CreationCollisionOption.ReplaceExisting);
            IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite);

            Database.Comics.Pack();
            XmlSerializer serializer = new XmlSerializer(typeof(ComicData));
            serializer.Serialize(stream.AsStream(), Database.Comics);

            stream.Dispose();
            ReleaseLock();
        }

        private static async Task SaveReadRecordData(StorageFolder userFolder)
        {
            await WaitLock();
            StorageFile file = await userFolder.CreateFileAsync(READ_RECORD_DATA_FILE_NAME, CreationCollisionOption.ReplaceExisting);
            IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite);

            Database.ComicRecords.Pack();
            XmlSerializer serializer = new XmlSerializer(typeof(ReadRecordsData));
            serializer.Serialize(stream.AsStream(), Database.ComicRecords);

            stream.Dispose();
            ReleaseLock();
        }

        private static async Task SaveFavoritesData(StorageFolder userFolder)
        {
            await WaitLock();
            StorageFile file = await userFolder.CreateFileAsync(FAVORITES_DATA_FILE_NAME, CreationCollisionOption.ReplaceExisting);
            IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite);

            Database.Favorites.Pack();
            XmlSerializer serializer = new XmlSerializer(typeof(FavoritesData));
            serializer.Serialize(stream.AsStream(), Database.Favorites);

            stream.Dispose();
            ReleaseLock();
        }

        private static async Task SaveHistoryItemData(StorageFolder userFolder)
        {
            await WaitLock();
            StorageFile file = await userFolder.CreateFileAsync(HISTORY_DATA_FILE_NAME, CreationCollisionOption.ReplaceExisting);
            IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite);

            Database.History.Pack();
            XmlSerializer serializer = new XmlSerializer(typeof(HistoryData));
            serializer.Serialize(stream.AsStream(), Database.History);

            stream.Dispose();
            ReleaseLock();
        }

        private static async Task SaveSettingsData(StorageFolder userFolder)
        {
            await WaitLock();
            StorageFile file = await userFolder.CreateFileAsync(SETTINGS_DATA_FILE_NAME, CreationCollisionOption.ReplaceExisting);
            IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite);

            Database.AppSettings.Pack();
            XmlSerializer serializer = new XmlSerializer(typeof(SettingsData));
            serializer.Serialize(stream.AsStream(), Database.AppSettings);

            stream.Dispose();
            ReleaseLock();
        }

        // database loading
        public static Func<Task<Utils.BackgroundTaskResult>, Utils.BackgroundTaskResult> LoadDatabaseSealed()
        {
            return delegate (Task<Utils.BackgroundTaskResult> _t) {
                var task = LoadDatabase();
                task.Wait();
                return task.Result;
            };
        }

        private static async Task<Utils.BackgroundTaskResult> LoadDatabase()
        {
            StorageFolder folder = ApplicationData.Current.LocalFolder;

            _ = await LoadSettingsData(folder);
            _ = await LoadComicData(folder);
            _ = await LoadReadRecordData(folder);
            _ = await LoadFavoritesData(folder);
            _ = await LoadHistoryItemData(folder);
            m_is_database_ready = true;

            // this should only be called asynchronously after app settings were loaded
            Utils.BackgroundTasks.AppendTask(UpdateComicDataSealed(), "", Utils.BackgroundTasks.EmptyQueue());
            return new Utils.BackgroundTaskResult();
        }

        private static async Task<Utils.BackgroundTaskResult> LoadComicData(StorageFolder userFolder)
        {
            await WaitLock();
            try
            {
                object file = await TryGetFile(userFolder, COMIC_DATA_FILE_NAME);

                if (file == null)
                {
                    return new Utils.BackgroundTaskResult(
                        Utils.BackgroundTaskExceptionType.FileNotExists);
                }

                IRandomAccessStream stream =
                    await ((StorageFile)file).OpenAsync(FileAccessMode.Read);

                XmlSerializer serializer = new XmlSerializer(typeof(ComicData));
                Database.Comics =
                    (ComicData)serializer.Deserialize(stream.AsStream());
                Database.Comics.Unpack();

                stream.Dispose();
                return new Utils.BackgroundTaskResult();
            }
            finally
            {
                ReleaseLock();
            }
        }

        private static async Task<Utils.BackgroundTaskResult> LoadReadRecordData(StorageFolder userFolder)
        {
            await WaitLock();
            try
            {
                object file = await TryGetFile(userFolder, READ_RECORD_DATA_FILE_NAME);

                if (file == null)
                {
                    return new Utils.BackgroundTaskResult(
                        Utils.BackgroundTaskExceptionType.FileNotExists);
                }

                IRandomAccessStream stream =
                    await ((StorageFile)file).OpenAsync(FileAccessMode.Read);

                XmlSerializer serializer = new XmlSerializer(typeof(ReadRecordsData));
                Database.ComicRecords =
                    (ReadRecordsData)serializer.Deserialize(stream.AsStream());
                Database.ComicRecords.Unpack();

                stream.Dispose();
                return new Utils.BackgroundTaskResult();
            }
            finally
            {
                ReleaseLock();
            }
        }

        private static async Task<Utils.BackgroundTaskResult> LoadFavoritesData(StorageFolder userFolder)
        {
            await WaitLock();
            try
            {
                object file = await TryGetFile(userFolder, FAVORITES_DATA_FILE_NAME);

                if (file == null)
                {
                    return new Utils.BackgroundTaskResult(
                        Utils.BackgroundTaskExceptionType.FileNotExists);
                }

                IRandomAccessStream stream =
                    await ((StorageFile)file).OpenAsync(FileAccessMode.Read);

                XmlSerializer serializer = new XmlSerializer(typeof(FavoritesData));
                Database.Favorites =
                    (FavoritesData)serializer.Deserialize(stream.AsStream());
                Database.Favorites.Unpack();

                stream.Dispose();
                return new Utils.BackgroundTaskResult();
            }
            finally
            {
                ReleaseLock();
            }
        }

        private static void LoadFavoritesDataHelper(List<FavoritesNodeData> folder, ref string str, ref int ps)
        {
            FavoritesNodeData node = null;

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
                    node = new FavoritesNodeData();
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

        private static async Task<Utils.BackgroundTaskResult> LoadHistoryItemData(
            StorageFolder userFolder)
        {
            await WaitLock();
            try
            {
                object file = await TryGetFile(userFolder, HISTORY_DATA_FILE_NAME);

                if (file == null)
                {
                    return new Utils.BackgroundTaskResult(
                        Utils.BackgroundTaskExceptionType.FileNotExists);
                }

                IRandomAccessStream stream =
                    await ((StorageFile)file).OpenAsync(FileAccessMode.Read);

                XmlSerializer serializer = new XmlSerializer(typeof(HistoryData));
                Database.History =
                    (HistoryData)serializer.Deserialize(stream.AsStream());
                Database.History.Unpack();

                stream.Dispose();
                return new Utils.BackgroundTaskResult();
            }
            finally
            {
                ReleaseLock();
            }
        }

        private static async Task<Utils.BackgroundTaskResult> LoadSettingsData(
            StorageFolder userFolder)
        {
            await WaitLock();
            try
            {
                object file = await TryGetFile(userFolder, SETTINGS_DATA_FILE_NAME);

                if (file == null)
                {
                    return new Utils.BackgroundTaskResult(
                        Utils.BackgroundTaskExceptionType.FileNotExists);
                }

                IRandomAccessStream stream =
                    await ((StorageFile)file).OpenAsync(FileAccessMode.Read);

                XmlSerializer serializer = new XmlSerializer(typeof(SettingsData));
                //serializer.UnknownNode += new
                //XmlNodeEventHandler(serializer_UnknownNode);
                //serializer.UnknownAttribute += new
                //XmlAttributeEventHandler(serializer_UnknownAttribute);
                Database.AppSettings =
                    (SettingsData)serializer.Deserialize(stream.AsStream());
                Database.AppSettings.Unpack();

                stream.Dispose();
                return new Utils.BackgroundTaskResult();
            }
            finally
            {
                ReleaseLock();
            }
        }

        // util functions (database related)
        public static async Task<string> GenerateComicInfoString(ComicItemData comic)
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

        private static string GenerateComicInfoStringNoLock(ComicItemData comic)
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

        public static async Task IntepretComicInfoString(string text, ComicItemData comic)
        {
            await WaitLock();
            IntepretComicInfoStringNoLock(text, comic);
            ReleaseLock();
        }

        private static void IntepretComicInfoStringNoLock(string text, ComicItemData comic)
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

        private static async Task<Utils.BackgroundTaskResult> CompleteComicInfoFileNoLock(ComicItemData comic, bool create_if_not_exists)
        {
            if (comic.InfoFile != null)
            {
                return new Utils.BackgroundTaskResult();
            }

            if (comic.IsExternal)
            {
                return create_if_not_exists ?
                    new Utils.BackgroundTaskResult(Utils.BackgroundTaskExceptionType.NoPermission) :
                    new Utils.BackgroundTaskResult(Utils.BackgroundTaskExceptionType.FileNotExists);
            }

            Utils.BackgroundTaskResult res = await CompleteComicFolderNoLock(comic);

            if (res.ExceptionType != Utils.BackgroundTaskExceptionType.Success)
            {
                throw new Exception();
            }

            IStorageItem item = await comic.Folder.TryGetItemAsync(COMIC_INFO_FILE_NAME);

            if (item == null)
            {
                if (!create_if_not_exists)
                {
                    return new Utils.BackgroundTaskResult(Utils.BackgroundTaskExceptionType.FileNotExists);
                }

                comic.InfoFile = await comic.Folder.CreateFileAsync(COMIC_INFO_FILE_NAME, CreationCollisionOption.FailIfExists);
                return new Utils.BackgroundTaskResult();
            }

            if (!(item is StorageFile))
            {
                return new Utils.BackgroundTaskResult(Utils.BackgroundTaskExceptionType.NameCollision);
            }

            comic.InfoFile = (StorageFile)item;
            return new Utils.BackgroundTaskResult();
        }

        public static Func<Task<Utils.BackgroundTaskResult>, Utils.BackgroundTaskResult> SaveComicInfoFileSealed(ComicItemData comic)
        {
            return delegate (Task<Utils.BackgroundTaskResult> _t) {
                var task = SaveComicInfoFile(comic);
                task.Wait();
                return task.Result;
            };
        }

        private static async Task<Utils.BackgroundTaskResult> SaveComicInfoFile(ComicItemData comic)
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

        private static async Task<Utils.BackgroundTaskResult> SaveComicInfoFileNoLock(ComicItemData comic)
        {
            Utils.BackgroundTaskResult res = await CompleteComicInfoFileNoLock(comic, true);

            if (res.ExceptionType != Utils.BackgroundTaskExceptionType.Success)
            {
                return new Utils.BackgroundTaskResult(Utils.BackgroundTaskExceptionType.Failure);
            }

            string text = GenerateComicInfoStringNoLock(comic);
            IBuffer buffer = GetBufferFromString(text);
            await FileIO.WriteBufferAsync(comic.InfoFile, buffer);
            return new Utils.BackgroundTaskResult();
        }

        public static async Task<Utils.BackgroundTaskResult> UpdateComicInfo(ComicItemData comic)
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

        public static async Task<Utils.BackgroundTaskResult> UpdateComicInfoNoLock(ComicItemData comic)
        {
            Utils.BackgroundTaskResult res = await CompleteComicInfoFileNoLock(comic, false);

            if (res.ExceptionType != Utils.BackgroundTaskExceptionType.Success)
            {
                return new Utils.BackgroundTaskResult();
            }

            string content = await FileIO.ReadTextAsync(comic.InfoFile);
            IntepretComicInfoStringNoLock(content, comic);
            return new Utils.BackgroundTaskResult();
        }

        private static Utils.CancellationLock update_comic_data_lock = new Utils.CancellationLock();

        public static Func<Task<Utils.BackgroundTaskResult>, Utils.BackgroundTaskResult> UpdateComicDataSealed()
        {
            return delegate (Task<Utils.BackgroundTaskResult> _t) {
                return UpdateComicItemData().Result;
            };
        }

        private static async Task<Utils.BackgroundTaskResult> UpdateComicItemData()
        {
            await update_comic_data_lock.WaitAsync();
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
                    StorageFolder folder = await Utils.Methods.TryGetFolder(folder_path);

                    // remove unreachable folders from database
                    if (folder == null)
                    {
                        await WaitLock();
                        Database.AppSettings.ComicFolders.Remove(folder_path);
                        ReleaseLock();
                        continue;
                    }

                    all_folders.Add(folder);

                    var query = folder.CreateFolderQueryWithOptions(new QueryOptions
                    {
                        FolderDepth = FolderDepth.Deep,
                        IndexerOption = IndexerOption.UseIndexerWhenAvailable
                    });
                    var folders = await query.GetFoldersAsync(); // 20 secs for 1000 folders
                    all_folders = all_folders.Concat(folders).ToList();

                    // cancel this task if more requests came in
                    if (update_comic_data_lock.CancellationRequested)
                    {
                        return new Utils.BackgroundTaskResult(Utils.BackgroundTaskExceptionType.Cancellation);
                    }
                }

                // extracts StorageFolder.Path into a new string list
                List<string> all_dir = new List<string>(all_folders.Count);
                for (int i = 0; i < all_folders.Count; ++i)
                {
                    all_dir.Add(all_folders[i].Path);
                }

                // cancel this task if more requests came in
                if (update_comic_data_lock.CancellationRequested)
                {
                    return new Utils.BackgroundTaskResult(Utils.BackgroundTaskExceptionType.Cancellation);
                }

                await WaitLock();
                // get all folder paths in database
                List<string> all_dir_in_lib = new List<string>(Database.Comics.Items.Count);
                foreach (ComicItemData comic in Database.Comics.Items)
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
                    if (update_comic_data_lock.CancellationRequested)
                    {
                        return new Utils.BackgroundTaskResult(Utils.BackgroundTaskExceptionType.Cancellation);
                    }

                    await WaitLock ();
                    ComicItemData comic = AddNewComicNoLock();
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
                return new Utils.BackgroundTaskResult();
            }
            finally
            {
                update_comic_data_lock.Release();
            }
        }

        // comic associated operations
        public static async Task<ComicItemData> GetComicWithId(string _id)
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

        private static ComicItemData GetComicWithIdNoLock(string _id)
        {
            if (_id.Length == 0)
            {
                return null;
            }

            foreach (ComicItemData comic in Database.Comics.Items)
            {
                if (comic.Id == _id)
                {
                    return comic;
                }
            }

            return null;
        }

        public static async Task<ComicItemData> GetComicWithDirectory(string dir)
        {
            await WaitLock();
            try
            {
                string dir_lower = dir.ToLower();

                foreach (ComicItemData comic in Database.Comics.Items)
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

        private static ComicItemData AddNewComicNoLock()
        {
            ComicItemData comic = new ComicItemData();
            string id;

            if (Database.Comics.Items.Count != 0)
            {
                id = (int.Parse(Database.Comics.Items[Database.Comics.Items.Count - 1].Id) + 1).ToString();
            }
            else
            {
                id = "0";
            }

            comic.Id = id;
            comic.LastVisit = DateTimeOffset.Now;
            Database.Comics.Items.Add(comic);
            return comic;
        }

        private static void RemoveComicsWithDirectoryNoLock(string dir)
        {
            dir = dir.ToLower();

            for (int i = 0; i < Database.Comics.Items.Count; ++i)
            {
                var comic = Database.Comics.Items[i];
                var current_dir = comic.Directory.ToLower();

                if (current_dir.Length < dir.Length)
                {
                    continue;
                }

                if (current_dir.Substring(0, dir.Length).Equals(dir))
                {
                    Database.Comics.Items.RemoveAt(i);
                    --i;
                }
            }
        }

        private static async Task<Utils.BackgroundTaskResult> CompleteComicFolder(ComicItemData comic)
        {
            await WaitLock();
            try
            {
                return await CompleteComicFolderNoLock(comic);
            }
            finally
            {
                ReleaseLock();
            }
        }

        private static async Task<Utils.BackgroundTaskResult> CompleteComicFolderNoLock(ComicItemData comic)
        {
            if (comic.Folder != null)
            {
                return new Utils.BackgroundTaskResult();
            }

            if (comic.Directory == null)
            {
                return new Utils.BackgroundTaskResult(Utils.BackgroundTaskExceptionType.InvalidParameters);
            }

            StorageFolder folder = await Utils.Methods.TryGetFolder(comic.Directory);

            if (folder == null)
            {
                return new Utils.BackgroundTaskResult(Utils.BackgroundTaskExceptionType.NoPermission);
            }

            comic.Folder = folder;
            return new Utils.BackgroundTaskResult();
        }

        public static Func<Task<Utils.BackgroundTaskResult>, Utils.BackgroundTaskResult> CompleteComicImagesSealed(ComicItemData comic)
        {
            return delegate (Task<Utils.BackgroundTaskResult> _t) {
                var task = CompleteComicImages(comic);
                task.Wait();
                return task.Result;
            };
        }

        public static async Task<Utils.BackgroundTaskResult> CompleteComicImages(ComicItemData comic)
        {
            await WaitLock();
            try
            {
                if (comic == null)
                {
                    return new Utils.BackgroundTaskResult(Utils.BackgroundTaskExceptionType.InvalidParameters, fatal: true);
                }

                if (comic.ImageFiles.Count != 0)
                {
                    return new Utils.BackgroundTaskResult();
                }

                Utils.BackgroundTaskResult res = await CompleteComicFolderNoLock(comic);

                if (res.ExceptionType != Utils.BackgroundTaskExceptionType.Success)
                {
                    return new Utils.BackgroundTaskResult(Utils.BackgroundTaskExceptionType.NoPermission);
                }
            }
            finally
            {
                ReleaseLock();
            }

            QueryOptions queryOptions = new QueryOptions
            {
                FolderDepth = FolderDepth.Shallow,
                IndexerOption = IndexerOption.UseIndexerWhenAvailable,
                FileTypeFilter = { ".jpg", ".jpeg", ".png", ".bmp" }
            };

            var query = comic.Folder.CreateFileQueryWithOptions(queryOptions);
            var img_files = await query.GetFilesAsync();

            await WaitLock();
            // sort by display name
            comic.ImageFiles = img_files.OrderBy(x => x.DisplayName, new Utils.StringUtils.FileNameComparer()).ToList();
            ReleaseLock();

            return new Utils.BackgroundTaskResult();
        }

        public static async Task HideComic(ComicItemData comic)
        {
            await WaitLock();
            comic.Hidden = true;
            ReleaseLock();
            Utils.BackgroundTasks.AppendTask(SaveDatabaseSealed(DatabaseItem.Comics), "Saving...");
        }

        public static async Task UnhideComic(ComicItemData comic)
        {
            await WaitLock();
            comic.Hidden = false;
            ReleaseLock();
            Utils.BackgroundTasks.AppendTask(SaveDatabaseSealed(DatabaseItem.Comics), "Saving...");
        }

        // comic record associated operations
        public static async Task<ReadRecordData> GetReadRecordWithId(string _id)
        {
            await WaitLock();
            try
            {
                return GetReadRecordWithIdNoLock(_id);
            }
            finally
            {
                ReleaseLock();
            }
        }

        private static ReadRecordData GetReadRecordWithIdNoLock(string _id)
        {
            if (_id.Length == 0)
            {
                return null;
            }

            foreach (ReadRecordData record in Database.ComicRecords.Items)
            {
                if (record.Id == _id)
                {
                    return record;
                }
            }

            return null;
        }

        // favorites associated operations
        public static async Task<FavoritesNodeData> GetFavoriteWithId(string id)
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

        private static FavoritesNodeData GetFavoriteWithIdNoLock(string id)
        {
            return GetFavoriteWithIdHelper(id, Database.Favorites.RootNodes);
        }

        private static FavoritesNodeData GetFavoriteWithIdHelper(string id, List<FavoritesNodeData> e)
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

        public static async Task<bool> RemoveFromFavoritesWithId(string id, bool final)
        {
            await WaitLock();
            bool res = RemoveFromFavoritesWithIdHelper(id, Database.Favorites.RootNodes);
            ReleaseLock();

            if (final)
            {
                await UpdateFavorites();
            }

            return res;
        }

        private static bool RemoveFromFavoritesWithIdHelper(string id, List<FavoritesNodeData> e)
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

        public static async Task AddToFavorites(string id, string title, bool final)
        {
            await WaitLock();
            try
            {
                if (GetFavoriteWithIdNoLock(id) != null)
                {
                    return;
                }

                FavoritesNodeData node = new FavoritesNodeData
                {
                    Type = "i",
                    Name = title,
                    Id = id
                };

                Database.Favorites.RootNodes.Add(node);
            }
            finally
            {
                ReleaseLock();
            }

            if (final)
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
        public static async Task AddToHistory(string id, string title, bool final)
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

                HistoryItemData record = new HistoryItemData();
                record.Id = id;
                record.DateTime = datetime;
                record.Title = title;

                RemoveFromHistoryNoLock(id);
                Database.History.Items.Insert(0, record);
            }
            finally
            {
                ReleaseLock();
            }

            if (final)
            {
                Utils.BackgroundTasks.AppendTask(SaveDatabaseSealed(DatabaseItem.History));

                if (Views.HistoryPage.Current != null)
                {
                    await Views.HistoryPage.Current.UpdateHistory();
                }
            }
        }

        public static async Task RemoveFromHistory(string id, bool final)
        {
            await WaitLock();
            RemoveFromHistoryNoLock(id);
            ReleaseLock();

            if (final)
            {
                Utils.BackgroundTasks.AppendTask(SaveDatabaseSealed(DatabaseItem.History));
            }
        }

        private static void RemoveFromHistoryNoLock(string id)
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

        // settings associated operations
        public static async Task<Utils.BackgroundTaskResult> AddToComicFolders(
            string new_folder, bool final)
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
                            return new Utils.BackgroundTaskResult(Utils.BackgroundTaskExceptionType.ItemExists);
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
                        return new Utils.BackgroundTaskResult(Utils.BackgroundTaskExceptionType.ItemExists);
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

            if (final)
            {
                Utils.BackgroundTasks.AppendTask(SaveDatabaseSealed(
                    DatabaseItem.Settings));
            }

            return new Utils.BackgroundTaskResult();
        }

        public static async Task RemoveFromComicFolders(string folder,
            bool final = false)
        {
            await WaitLock();
            _ = Database.AppSettings.ComicFolders.Remove(folder);
            ReleaseLock();

            if (final)
            {
                Utils.BackgroundTasks.AppendTask(SaveDatabaseSealed(
                    DatabaseItem.Settings));
            }
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

            Utils.BackgroundTaskResult res =
                await AddToComicFolders(folder.Path, true);
            
            if (res.ExceptionType != Utils.BackgroundTaskExceptionType.Success)
            {
                return false;
            }

            StorageApplicationPermissions.FutureAccessList.AddOrReplace(
                Utils.StringUtils.TokenFromPath(folder.Path), folder);
            return true;
        }

        public class ImageLoaderToken
        {
            public ComicItemData Comic;
            public int Index;
            public Action<BitmapImage> Callback;
        }

        public enum ImageConstrainOption
        {
            None,
            SameAsFirstImage
        }

        public class ImageConstrain
        {
            public double Val;
            public ImageConstrainOption Option;

            public static implicit operator ImageConstrain(double val)
            {
                if (double.IsNaN(val))
                {
                    throw new Exception("Invalid parameters");
                }

                return new ImageConstrain
                {
                    Val = val,
                    Option = ImageConstrainOption.None
                };
            }

            public static implicit operator ImageConstrain(ImageConstrainOption opt)
            {
                return new ImageConstrain
                {
                    Val = 0.0,
                    Option = opt
                };
            }
        }

        public static async Task UtilsLoadImages(IEnumerable<ImageLoaderToken> tokens,
            ImageConstrain max_width, ImageConstrain max_height, Utils.CancellationLock cancellation_lock)
        {
            List<ImageLoaderToken> _tokens = new List<ImageLoaderToken>(tokens);
            bool use_origin_size =
                max_width.Option == ImageConstrainOption.None &&
                double.IsInfinity(max_width.Val) &&
                max_height.Option == ImageConstrainOption.None &&
                double.IsInfinity(max_height.Val);
            double raw_pixels_per_view_pixel = 0.0;
            double frame_ratio = 0.0;
            bool first = true;
            bool file_not_found_err = false;

            for (; _tokens.Count > 0; )
            {
                ImageLoaderToken t = _tokens.First();
                _tokens.RemoveAt(0);

                if (cancellation_lock.CancellationRequested)
                {
                    return;
                }

                ComicItemData comic = t.Comic;

                if (comic.ImageFiles.Count == 0)
                {
                    Utils.BackgroundTaskResult res = await CompleteComicImages(comic);

                    // skip the tokens whose comic folder cannot be reached
                    if (res.ExceptionType != Utils.BackgroundTaskExceptionType.Success)
                    {
                        _tokens.RemoveAll(x => x.Comic == comic);
                        file_not_found_err = true;
                        continue;
                    }
                }

                if (comic.ImageFiles.Count <= t.Index)
                {
                    file_not_found_err = true;
                    continue;
                }

                StorageFile image_file = comic.ImageFiles[t.Index];
                IRandomAccessStream stream;

                try
                {
                    stream = await image_file.OpenAsync(FileAccessMode.Read);
                }
                catch (FileNotFoundException)
                {
                    file_not_found_err = true;
                    continue;
                }

                BitmapImage image = null;
                Task task = null;

                await Utils.Methods.Sync(delegate
                {
                    image = new BitmapImage();
                    task = image.SetSourceAsync(stream).AsTask();
                });

                await task.AsAsyncAction();
                stream.Dispose();

                await Utils.Methods.Sync(delegate
                {
                    if (first)
                    {
                        first = false;

                        if (max_width.Option == ImageConstrainOption.SameAsFirstImage)
                        {
                            max_width.Val = image.PixelWidth;
                        }

                        if (max_height.Option == ImageConstrainOption.SameAsFirstImage)
                        {
                            max_height.Val = image.PixelHeight;
                        }

                        raw_pixels_per_view_pixel = DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel;
                        frame_ratio = max_width.Val / max_height.Val;
                    }

                    if (!use_origin_size)
                    {
                        double image_ratio = (double)image.PixelWidth / image.PixelHeight;
                        double image_height;
                        double image_width;
                        if (image_ratio > frame_ratio)
                        {
                            image_width = max_width.Val * raw_pixels_per_view_pixel;
                            image_height = image_width / image_ratio;
                        }
                        else
                        {
                            image_height = max_height.Val * raw_pixels_per_view_pixel;
                            image_width = image_height * image_ratio;
                        }
                        image.DecodePixelHeight = (int)image_height;
                        image.DecodePixelWidth = (int)image_width;
                    }

                    t.Callback?.Invoke(image);
                });
            }

            // Not all the images are successfully loaded, most likely that some of the
            // files or directories has been moved or deleted. This triggers a
            // DataManager.ComicDataUpdate() in the background.
            if (file_not_found_err)
            {
                Utils.BackgroundTasks.AppendTask(UpdateComicDataSealed(),
                    "", Utils.BackgroundTasks.EmptyQueue());
            }
        }
    }
}