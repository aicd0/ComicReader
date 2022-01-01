//#define DEBUG_LOG_IMAGE_LOADED

using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    
    [Serializable]
    public class TagData
    {
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
        public ComicData(long id = -1)
        {
            Id = id;
        }

        // Basic fields.
        public long Id { get; private set; }
        public string Title1;
        public string Title2;
        public string Directory;
        public bool Hidden = false;
        public int Rating = -1;
        public int Progress = 0;
        public DateTimeOffset LastVisit = DateTimeOffset.MinValue;
        public double LastPosition = 0.0;

        // Blob fields.
        public List<TagData> Tags = new List<TagData>();
        public List<double> ImageAspectRatios = new List<double>();

        // Field names.
        public const string FieldId = "id";
        public const string FieldTitle1 = "title1";
        public const string FieldTitle2 = "title2";
        public const string FieldDirectory = "dir";
        public const string FieldHidden = "hidden";
        public const string FieldRating = "rating";
        public const string FieldProgress = "progress";
        public const string FieldLastVisit = "last_visit";
        public const string FieldLastPosition = "last_pos";
        public const string FieldTags = "tags";
        public const string FieldImageAspectRatios = "image_aspect_ratios";

        // Not in database.
        public string Title => Title1.Length == 0 ?
            (Title2.Length == 0 ? "Untitled Collection" : Title2) :
            (Title2.Length == 0 ? Title1 : Title1 + " - " + Title2);
        public bool IsExternal = false;
        public StorageFolder Folder = null;
        public StorageFile InfoFile = null;
        public List<StorageFile> ImageFiles = new List<StorageFile>();

        // Saving.
        private async Task Save(List<Key> keys)
        {
            if (Id < 0) throw new Exception();

            Key id = new Key(FieldId, Id);
            await DatabaseManager.Update(DatabaseManager.ComicTable, id, keys);
        }

        public async Task Save()
        {
            List<Key> keys = new List<Key>
            {
                new Key(FieldTitle1, Title1),
                new Key(FieldTitle2, Title2),
                new Key(FieldDirectory, Directory),
                new Key(FieldHidden, Hidden),
                new Key(FieldRating, Rating),
                new Key(FieldProgress, Progress),
                new Key(FieldLastVisit, LastVisit),
                new Key(FieldLastPosition, LastPosition),
                new Key(FieldTags, Tags, blob: true),
                new Key(FieldImageAspectRatios, ImageAspectRatios, blob: true),
            };

            if (Id < 0)
            {
                long rowid = await DatabaseManager.Insert(DatabaseManager.ComicTable, keys);

                // Retrieve ID from inserted row.
                SqliteCommand command = DatabaseManager.Connection.CreateCommand();
                command.CommandText = "SELECT " + FieldId + " FROM " +
                    DatabaseManager.ComicTable + " WHERE ROWID=$rowid";
                command.Parameters.AddWithValue("$rowid", rowid);

                await ComicDataManager.WaitLock();
                Id = (long)command.ExecuteScalar();
                ComicDataManager.ReleaseLock();
            }
            else
            {
                await Save(keys);
            }
        }

        public async Task SaveBasic()
        {
            List<Key> keys = new List<Key>
            {
                new Key(FieldTitle1, Title1),
                new Key(FieldTitle2, Title2),
                new Key(FieldDirectory, Directory),
                new Key(FieldHidden, Hidden),
                new Key(FieldRating, Rating),
                new Key(FieldProgress, Progress),
                new Key(FieldLastVisit, LastVisit),
                new Key(FieldLastPosition, LastPosition),
                new Key(FieldTags, Tags, blob: true),
            };
            await Save(keys);
        }

        public async Task SaveImageAspectRatios()
        {
            List<Key> keys = new List<Key>
            {
                new Key(FieldImageAspectRatios, ImageAspectRatios, blob: true)
            };
            await Save(keys);
        }
    };

    public class ImageLoaderToken
    {
        public ComicData Comic;
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
            System.Diagnostics.Debug.Assert(!double.IsNaN(val));

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
        private const string COMIC_INFO_FILE_NAME = "info.txt";

        private static readonly SemaphoreSlim TableLock = new SemaphoreSlim(1);
        private static readonly Utils.CancellationLock m_update_lock = new Utils.CancellationLock();

        public static async Task WaitLock()
        {
            await TableLock.WaitAsync();
        }

        public static void ReleaseLock()
        {
            TableLock.Release();
        }

        public static async Task<ComicData> From(SqliteDataReader query)
        {
            ComicData comic = new ComicData(query.GetInt32(0))
            {
                Title1 = query.GetString(1),
                Title2 = query.GetString(2),
                Directory = query.GetString(3),
                Hidden = query.GetBoolean(4),
                Rating = query.GetInt32(5),
                Progress = query.GetInt32(6),
                LastVisit = query.GetDateTimeOffset(7),
                LastPosition = query.GetDouble(8),
            };

            MemoryStream stream = new MemoryStream();

            stream.Seek(0, SeekOrigin.Begin);
            await query.GetStream(9).CopyToAsync(stream);
            comic.Tags = (List<TagData>)Utils.Methods.DeserializeFromStream(stream);

            stream.Seek(0, SeekOrigin.Begin);
            await query.GetStream(10).CopyToAsync(stream);
            comic.ImageAspectRatios = (List<double>)Utils.Methods.DeserializeFromStream(stream);

            return comic;
        }

        private static async Task<ComicData> From(string col, object entry)
        {
            SqliteCommand command = new SqliteCommand();
            command.Connection = DatabaseManager.Connection;
            command.CommandText = "SELECT * FROM " + DatabaseManager.ComicTable +
                " WHERE " + col + "=@entry LIMIT 1";
            command.Parameters.AddWithValue("@entry", entry);

            await WaitLock();
            try
            {
                SqliteDataReader query = command.ExecuteReader();
                if (!query.Read()) return null;
                return await From(query);
            }
            finally
            {
                ReleaseLock();
            }
        }

        public static async Task<ComicData> FromId(long id)
        {
            return await From(ComicData.FieldId, id);
        }

        public static async Task<ComicData> FromDirectory(string dir)
        {
            return await From(ComicData.FieldDirectory, dir);
        }

        private static async Task<ComicData> New(string title1, string title2, string dir)
        {
            ComicData comic = new ComicData
            {
                Title1 = title1,
                Title2 = title2,
                Directory = dir
            };
            await comic.Save();
            return comic;
        }

        private static async Task RemoveWithDirectory(string dir)
        {
            SqliteCommand command = new SqliteCommand();
            command.Connection = DatabaseManager.Connection;
            command.CommandText = @"DELETE FROM " + DatabaseManager.ComicTable +
                " WHERE " + ComicData.FieldDirectory + " LIKE @pattern";
            command.Parameters.AddWithValue("@pattern", dir + "%");

            await WaitLock();
            command.ExecuteNonQuery();
            ReleaseLock();
        }

        public static async RawTask Update(bool lazy_load)
        {
            await m_update_lock.WaitAsync();

            try
            {
                // get root folders
                await DatabaseManager.WaitLock();
                List<string> root_folders = new List<string>(Database.AppSettings.ComicFolders.Count);

                foreach (string folder_path in Database.AppSettings.ComicFolders)
                {
                    root_folders.Add(folder_path);
                }

                // get all folders and subfolders
                DatabaseManager.ReleaseLock();
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

                    StorageFolderQueryResult query = folder.CreateFolderQueryWithOptions(new QueryOptions
                    {
                        FolderDepth = FolderDepth.Deep,
                        IndexerOption = IndexerOption.UseIndexerWhenAvailable
                    });

                    IReadOnlyList<StorageFolder> folders = await query.GetFoldersAsync(); // 20s per 1k folders
                    all_folders = all_folders.Concat(folders).ToList();

                    // cancel this task if more requests came in
                    if (m_update_lock.CancellationRequested)
                    {
                        return new TaskResult(TaskException.Cancellation);
                    }
                }

                Utils.TaskQueue.TaskQueueManager.AppendTask(DatabaseManager.SaveSealed(DatabaseItem.AppSettings));

                // extracts StorageFolder.Path into a new string list
                List<string> all_dir = new List<string>(all_folders.Count);

                for (int i = 0; i < all_folders.Count; ++i)
                {
                    all_dir.Add(all_folders[i].Path);
                }

                // get all folder paths in database
                List<string> all_dir_in_lib = new List<string>();

                {
                    SqliteCommand command = new SqliteCommand();
                    command.Connection = DatabaseManager.Connection;
                    command.CommandText = "SELECT " + ComicData.FieldDirectory +
                        " FROM " + DatabaseManager.ComicTable;

                    await ComicDataManager.WaitLock(); // Lock on.
                    SqliteDataReader query = command.ExecuteReader();

                    while (query.Read())
                    {
                        all_dir_in_lib.Add(query.GetString(0));
                    }
                    ComicDataManager.ReleaseLock(); // Lock off.
                }

                // get all folders added or removed.
                List<string> dir_added = Utils.Methods3<string, string, string>.Except(all_dir, all_dir_in_lib,
                    Utils.StringUtils.UniquePath, Utils.StringUtils.UniquePath,
                    new Utils.StringUtils.DefaultEqualityComparer()).ToList();
                List<string> dir_removed = Utils.Methods3<string, string, string>.Except(all_dir_in_lib, all_dir,
                    Utils.StringUtils.UniquePath, Utils.StringUtils.UniquePath,
                    new Utils.StringUtils.DefaultEqualityComparer()).ToList();

                // remove items from database.
                foreach (var dir in dir_removed)
                {
                    await RemoveWithDirectory(dir);
                }

                // define a local method which will be used later to add or update dir.
                async Task add_or_update_dir(string dir, bool update)
                {
                    // Exclude folders which do not directly contain images. (120s per 1k folders)
                    QueryOptions queryOptions = new QueryOptions
                    {
                        FolderDepth = FolderDepth.Shallow,
                        IndexerOption = IndexerOption.UseIndexerWhenAvailable,
                        FileTypeFilter = { ".jpg", ".jpeg", ".png", ".bmp" }
                    };

                    StorageFolder folder = await Utils.Methods.TryGetFolder(dir);

                    if (folder == null)
                    {
                        return;
                    }

                    StorageFileQueryResult query = folder.CreateFileQueryWithOptions(queryOptions);
                    uint file_count = await query.GetItemCountAsync();

                    if (file_count == 0)
                    {
                        if (update) await RemoveWithDirectory(dir);
                        return;
                    }

                    // Update or create a new one.
                    ComicData comic;

                    if (update)
                    {
                        comic = await FromDirectory(dir);
                    }
                    else
                    {
                        comic = await New("", "", dir);
                    }

                    if (comic == null)
                    {
                        return;
                    }
                    
                    comic.Folder = folder;

                    // Try load comic info locally.
                    TaskResult r = await UpdateInfoNoLock(comic);

                    if (!r.Successful && !update)
                    {
                        // Auto-imported properties.
                        comic.Title1 = folder.DisplayName;

                        TagData default_tag = new TagData
                        {
                            Name = "Default",
                            Tags = dir.Split("\\").ToHashSet()
                        };

                        comic.Tags.Add(default_tag);
                    }

                    await comic.SaveBasic();
                }

                // generate a task queue.
                List<KeyValuePair<string, bool>> queue = new List<KeyValuePair<string, bool>>();

                foreach (string dir in dir_added)
                {
                    // "false" means the dir will be directly added to the database instead of updating an existing one.
                    queue.Add(new KeyValuePair<string, bool>(dir, false));
                }

                if (!lazy_load)
                {
                    List<string> dir_kept = Utils.Methods3<string, string, string>.Intersect(all_dir, all_dir_in_lib,
                        Utils.StringUtils.UniquePath, Utils.StringUtils.UniquePath,
                        new Utils.StringUtils.DefaultEqualityComparer()).ToList();

                    foreach (string dir in dir_kept)
                    {
                        // "true" means the item with the same dir in the database will be updated. no new items will be added.
                        queue.Add(new KeyValuePair<string, bool>(dir, true));
                    }
                }

                DateTimeOffset last_date_time = DateTimeOffset.Now;

                foreach (KeyValuePair<string, bool> p in queue)
                {
                    await add_or_update_dir(p.Key, p.Value);

                    // cancel this task if more requests came in
                    if (m_update_lock.CancellationRequested)
                    {
                        return new TaskResult(TaskException.Cancellation);
                    }
                }

                return new TaskResult();
            }
            finally
            {
                m_update_lock.Release();
            }
        }

        public static async Task Hide(ComicData comic)
        {
            comic.Hidden = true;
            await comic.SaveBasic();
        }

        public static async Task Unhide(ComicData comic)
        {
            comic.Hidden = false;
            await comic.SaveBasic();
        }

        // info
        public static async Task<string> InfoString(ComicData comic)
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

        private static string InfoStringNoLock(ComicData comic)
        {
            string text = "";
            text += "Title1: " + comic.Title1;
            text += "\nTitle2: " + comic.Title2;

            foreach (TagData tag in comic.Tags)
            {
                text += "\n" + tag.Name + ": " + Utils.StringUtils.Join("/", tag.Tags);
            }

            return text;
        }

        public static SealedTask SaveInfoFileSealed(ComicData comic) =>
            (RawTask _) => SaveInfoFile(comic).Result;

        private static async RawTask SaveInfoFile(ComicData comic)
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

        private static async RawTask SaveInfoFileNoLock(ComicData comic)
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

        public static async Task ParseInfo(string text, ComicData comic)
        {
            await DatabaseManager.WaitLock();
            ParseInfoNoLock(text, comic);
            DatabaseManager.ReleaseLock();
        }

        private static void ParseInfoNoLock(string text, ComicData comic)
        {
            comic.Title1 = "";
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

                if (name == "title1" || name == "title" || name == "t1" || name == "to")
                {
                    comic.Title1 = Utils.StringUtils.Join("/", tag_data.Tags);
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

        private static async RawTask CompleteInfoFileNoLock(ComicData comic, bool create_if_not_exists)
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

            System.Diagnostics.Debug.Assert(res.ExceptionType == TaskException.Success);

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

        public static async RawTask UpdateInfo(ComicData comic)
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

        public static async RawTask UpdateInfoNoLock(ComicData comic)
        {
            TaskResult res = await CompleteInfoFileNoLock(comic, false);

            if (res.ExceptionType != TaskException.Success)
            {
                return res;
            }

            string content = await FileIO.ReadTextAsync(comic.InfoFile);
            ParseInfoNoLock(content, comic);
            return new TaskResult();
        }

        private static async RawTask CompleteFolder(ComicData comic)
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

        private static async RawTask CompleteFolderNoLock(ComicData comic)
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

        public static SealedTask CompleteImagesSealed(ComicData comic) =>
            (RawTask _) => CompleteImages(comic).Result;

        public static async RawTask CompleteImages(ComicData comic)
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
                if (cancellation_lock.CancellationRequested)
                {
                    return;
                }

                ImageLoaderToken token = _tokens.First();
                _tokens.RemoveAt(0);
                ComicData comic = token.Comic;

                if (comic.ImageFiles.Count == 0)
                {
                    TaskResult res = await CompleteImages(comic);

                    // skip tokens whose comic folder cannot be reached
                    if (res.ExceptionType != TaskException.Success)
                    {
                        _tokens.RemoveAll(x => x.Comic == comic);
                        trig_update = true;
                        continue;
                    }
                }

                if (comic.ImageFiles.Count <= token.Index)
                {
                    trig_update = true;
                    continue;
                }

                StorageFile image_file = comic.ImageFiles[token.Index];
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

                    token.Callback?.Invoke(image);
#if DEBUG_LOG_IMAGE_LOADED
                    System.Diagnostics.Debug.Print("Image " + token.Index.ToString() + " loaded.\n");
#endif
                });
            }

            // Not all the images are successfully loaded, most likely that some of the
            // files or directories has been renamed, moved or deleted. We trigger a
            // DatabaseManager.Update() here to sync the changes.
            if (trig_update)
            {
                Utils.TaskQueue.TaskQueueManager.AppendTask(DatabaseManager.UpdateSealed(), "",
                    Utils.TaskQueue.TaskQueueManager.EmptyQueue());
            }
        }
    }
}
