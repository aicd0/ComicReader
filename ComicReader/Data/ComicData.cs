﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Windows.Graphics.Display;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace ComicReader.Data
{
    using RawTask = Task<Utils.TaskQueue.TaskResult>;
    using SealedTask = Func<Task<Utils.TaskQueue.TaskResult>, Utils.TaskQueue.TaskResult>;
    using TaskResult = Utils.TaskQueue.TaskResult;
    using TaskException = Utils.TaskQueue.TaskException;

    public class TagData
    {
        [XmlAttribute]
        public string Name;
        public HashSet<string> Tags = new HashSet<string>();

        public static TagData Parse(string src)
        {
            string[] pieces = src.Split(":", 2, StringSplitOptions.RemoveEmptyEntries);

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

    class ComicDataManager
    {
        private const string COMIC_DATA_FILE_NAME = "com";
        private const string COMIC_INFO_FILE_NAME = "Info.txt";
        private static Utils.CancellationLock m_update_comic_data_lock = new Utils.CancellationLock();

        public static async Task Save(StorageFolder user_folder)
        {
            await DatabaseManager.WaitLock();
            StorageFile file = await user_folder.CreateFileAsync(
                COMIC_DATA_FILE_NAME, CreationCollisionOption.ReplaceExisting);
            IRandomAccessStream stream = await file.OpenAsync(
                FileAccessMode.ReadWrite);

            Database.Comics.Pack();
            XmlSerializer serializer = new XmlSerializer(typeof(ComicData));
            serializer.Serialize(stream.AsStream(), Database.Comics);

            stream.Dispose();
            DatabaseManager.ReleaseLock();
        }

        public static async RawTask Load(StorageFolder user_folder)
        {
            object file = await DatabaseManager.TryGetFile(user_folder, COMIC_DATA_FILE_NAME);

            if (file == null)
            {
                return new TaskResult(TaskException.FileNotExists);
            }

            IRandomAccessStream stream =
                await ((StorageFile)file).OpenAsync(FileAccessMode.Read);

            XmlSerializer serializer = new XmlSerializer(typeof(ComicData));
            Database.Comics =
                (ComicData)serializer.Deserialize(stream.AsStream());
            Database.Comics.Unpack();

            stream.Dispose();
            return new TaskResult();
        }

        public static async Task<ComicItemData> FromId(string _id)
        {
            await DatabaseManager.WaitLock();
            try
            {
                return FromIdNoLock(_id);
            }
            finally
            {
                DatabaseManager.ReleaseLock();
            }
        }

        private static ComicItemData FromIdNoLock(string _id)
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

        public static async Task<ComicItemData> FromDirectory(string dir)
        {
            await DatabaseManager.WaitLock();
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
                DatabaseManager.ReleaseLock();
            }
        }

        private static ComicItemData NewNoLock()
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

        private static void RemoveWithDirectoryNoLock(string dir)
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

        public static SealedTask UpdateSealed() => (RawTask _) => Update().Result;

        private static async RawTask Update()
        {
            await m_update_comic_data_lock.WaitAsync();
            try
            {
                await DatabaseManager.WaitLock();
                // get root folders
                List<string> root_folders = new List<string>(Database.AppSettings.ComicFolders.Count);
                foreach (string folder_path in Database.AppSettings.ComicFolders)
                {
                    root_folders.Add(folder_path);
                }
                DatabaseManager.ReleaseLock();

                // get all folders and subfolders
                List<StorageFolder> all_folders = new List<StorageFolder>();

                foreach (string folder_path in root_folders)
                {
                    StorageFolder folder = await Utils.Methods.TryGetFolder(folder_path);

                    // remove unreachable folders from database
                    if (folder == null)
                    {
                        await DatabaseManager.WaitLock();
                        Database.AppSettings.ComicFolders.Remove(folder_path);
                        DatabaseManager.ReleaseLock();
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
                    if (m_update_comic_data_lock.CancellationRequested)
                    {
                        return new TaskResult(TaskException.Cancellation);
                    }
                }

                Utils.TaskQueue.TaskQueueManager.AppendTask(
                    DatabaseManager.SaveSealed(DatabaseItem.Settings));

                // extracts StorageFolder.Path into a new string list
                List<string> all_dir = new List<string>(all_folders.Count);
                for (int i = 0; i < all_folders.Count; ++i)
                {
                    all_dir.Add(all_folders[i].Path);
                }

                // get all folder paths in database
                await DatabaseManager.WaitLock();
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
                    RemoveWithDirectoryNoLock(dir);
                }
                DatabaseManager.ReleaseLock();

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
                    if (m_update_comic_data_lock.CancellationRequested)
                    {
                        return new TaskResult(TaskException.Cancellation);
                    }

                    await DatabaseManager.WaitLock();
                    ComicItemData comic = NewNoLock();
                    comic.Directory = dir;
                    comic.Folder = folder;

                    TagData default_tag = new TagData
                    {
                        Name = "Default",
                        Tags = dir.Split("\\").ToHashSet()
                    };

                    comic.Tags.Add(default_tag);
                    await UpdateInfoNoLock(comic);
                    DatabaseManager.ReleaseLock();

                    // save for each 5 secs
                    if ((DateTimeOffset.Now - last_date_time).Seconds > 5.0)
                    {
                        Utils.TaskQueue.TaskQueueManager.AppendTask(
                            DatabaseManager.SaveSealed(DatabaseItem.Comics));
                        last_date_time = DateTimeOffset.Now;
                    }
                }

                // save
                Utils.TaskQueue.TaskQueueManager.AppendTask(
                    DatabaseManager.SaveSealed(DatabaseItem.Comics));
                return new TaskResult();
            }
            finally
            {
                m_update_comic_data_lock.Release();
            }
        }

        public static async Task Hide(ComicItemData comic)
        {
            await DatabaseManager.WaitLock();
            comic.Hidden = true;
            DatabaseManager.ReleaseLock();
            Utils.TaskQueue.TaskQueueManager.AppendTask(
                DatabaseManager.SaveSealed(DatabaseItem.Comics), "Saving...");
        }

        public static async Task Unhide(ComicItemData comic)
        {
            await DatabaseManager.WaitLock();
            comic.Hidden = false;
            DatabaseManager.ReleaseLock();
            Utils.TaskQueue.TaskQueueManager.AppendTask(
                DatabaseManager.SaveSealed(DatabaseItem.Comics), "Saving...");
        }

        // info
        public static async Task<string> InfoString(ComicItemData comic)
        {
            await DatabaseManager.WaitLock();

            try
            {
                return InfoStringNoLock(comic);
            }
            finally
            {
                DatabaseManager.ReleaseLock();
            }
        }

        private static string InfoStringNoLock(ComicItemData comic)
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

        public static SealedTask SaveInfoFileSealed(ComicItemData comic) =>
            (RawTask _) => SaveInfoFile(comic).Result;

        private static async RawTask SaveInfoFile(ComicItemData comic)
        {
            await DatabaseManager.WaitLock();

            try
            {
                return await SaveInfoFileNoLock(comic);
            }
            finally
            {
                DatabaseManager.ReleaseLock();
            }
        }

        private static async RawTask SaveInfoFileNoLock(ComicItemData comic)
        {
            TaskResult res = await CompleteInfoFileNoLock(comic, true);

            if (res.ExceptionType != TaskException.Success)
            {
                return new TaskResult(TaskException.Failure);
            }

            string text = InfoStringNoLock(comic);
            IBuffer buffer = Utils.Methods.GetBufferFromString(text);
            await FileIO.WriteBufferAsync(comic.InfoFile, buffer);
            return new TaskResult();
        }

        public static async Task ParseInfo(string text, ComicItemData comic)
        {
            await DatabaseManager.WaitLock();
            ParseInfoNoLock(text, comic);
            DatabaseManager.ReleaseLock();
        }

        private static void ParseInfoNoLock(string text, ComicItemData comic)
        {
            comic.Title = "";
            comic.Title2 = "";
            comic.Tags.Clear();
            text = text.Replace('\r', '\n');
            string[] properties = text.Split("\n", StringSplitOptions.RemoveEmptyEntries);

            foreach (string property in properties)
            {
                TagData tag_data = TagData.Parse(property);

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

        private static async RawTask CompleteInfoFileNoLock(ComicItemData comic, bool create_if_not_exists)
        {
            if (comic.InfoFile != null)
            {
                return new TaskResult();
            }

            if (comic.IsExternal)
            {
                return create_if_not_exists ?
                    new TaskResult(TaskException.NoPermission) :
                    new TaskResult(TaskException.FileNotExists);
            }

            TaskResult res = await CompleteFolderNoLock(comic);

            if (res.ExceptionType != TaskException.Success)
            {
                throw new Exception();
            }

            IStorageItem item = await comic.Folder.TryGetItemAsync(COMIC_INFO_FILE_NAME);

            if (item == null)
            {
                if (!create_if_not_exists)
                {
                    return new TaskResult(TaskException.FileNotExists);
                }

                comic.InfoFile = await comic.Folder.CreateFileAsync(COMIC_INFO_FILE_NAME, CreationCollisionOption.FailIfExists);
                return new TaskResult();
            }

            if (!(item is StorageFile))
            {
                return new TaskResult(TaskException.NameCollision);
            }

            comic.InfoFile = (StorageFile)item;
            return new TaskResult();
        }

        public static async RawTask UpdateInfo(ComicItemData comic)
        {
            await DatabaseManager.WaitLock();

            try
            {
                return await UpdateInfoNoLock(comic);
            }
            finally
            {
                DatabaseManager.ReleaseLock();
            }
        }

        public static async RawTask UpdateInfoNoLock(ComicItemData comic)
        {
            TaskResult res = await CompleteInfoFileNoLock(comic, false);

            if (res.ExceptionType != TaskException.Success)
            {
                return new TaskResult();
            }

            string content = await FileIO.ReadTextAsync(comic.InfoFile);
            ParseInfoNoLock(content, comic);
            return new TaskResult();
        }

        private static async RawTask CompleteFolder(ComicItemData comic)
        {
            await DatabaseManager.WaitLock();

            try
            {
                return await CompleteFolderNoLock(comic);
            }
            finally
            {
                DatabaseManager.ReleaseLock();
            }
        }

        private static async RawTask CompleteFolderNoLock(ComicItemData comic)
        {
            if (comic.Folder != null)
            {
                return new TaskResult();
            }

            if (comic.Directory == null)
            {
                return new TaskResult(TaskException.InvalidParameters);
            }

            StorageFolder folder = await Utils.Methods.TryGetFolder(comic.Directory);

            if (folder == null)
            {
                return new TaskResult(TaskException.NoPermission);
            }

            comic.Folder = folder;
            return new TaskResult();
        }

        public static SealedTask CompleteImagesSealed(ComicItemData comic) =>
            (RawTask _) => CompleteImages(comic).Result;

        public static async RawTask CompleteImages(ComicItemData comic)
        {
            await DatabaseManager.WaitLock();

            try
            {
                if (comic == null)
                {
                    return new TaskResult(TaskException.InvalidParameters, fatal: true);
                }

                if (comic.ImageFiles.Count != 0)
                {
                    return new TaskResult();
                }

                TaskResult res = await CompleteFolderNoLock(comic);

                if (res.ExceptionType != TaskException.Success)
                {
                    return new TaskResult(TaskException.NoPermission);
                }
            }
            finally
            {
                DatabaseManager.ReleaseLock();
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
            await DatabaseManager.WaitLock();
            comic.ImageFiles = img_files.OrderBy(x => x.DisplayName, new Utils.StringUtils.FileNameComparer()).ToList();
            DatabaseManager.ReleaseLock();
            return new TaskResult();
        }

        public static async Task LoadImages(IEnumerable<ImageLoaderToken> tokens,
            ImageConstrain max_width, ImageConstrain max_height,
            Utils.CancellationLock cancellation_lock)
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
            bool trig_update = false;

            for (; _tokens.Count > 0;)
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
                    TaskResult res = await CompleteImages(comic);

                    // skip the tokens whose comic folder cannot be reached
                    if (res.ExceptionType != TaskException.Success)
                    {
                        _tokens.RemoveAll(x => x.Comic == comic);
                        trig_update = true;
                        continue;
                    }
                }

                if (comic.ImageFiles.Count <= t.Index)
                {
                    trig_update = true;
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
                    trig_update = true;
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
            if (trig_update)
            {
                Utils.TaskQueue.TaskQueueManager.AppendTask(UpdateSealed(), "",
                    Utils.TaskQueue.TaskQueueManager.EmptyQueue());
            }
        }
    }
}