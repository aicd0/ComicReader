//#define DEBUG_LOG_IMAGE_LOADED

using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.Storage.Streams;

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

        // Non-blob fields.
        public long Id { get; private set; }
        public string Title1 = "";
        public string Title2 = "";
        public string Directory = "";
        public bool Hidden = false;
        public int Rating = -1;
        public int Progress = -1;
        public DateTimeOffset LastVisit = DateTimeOffset.MinValue;
        public double LastPosition = 0.0;
        public string CoverFileName = "";

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
        public const string FieldCoverFileName = "cover_file_name";
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
        private async Task Save(LockContext db, List<SqlKey> keys)
        {
            System.Diagnostics.Debug.Assert(Id >= 0);
            SqlKey id = new SqlKey(FieldId, Id);
            await DatabaseManager.Update(db, DatabaseManager.ComicTable, id, keys);
        }

        public async Task Save(LockContext db)
        {
            List<SqlKey> keys = new List<SqlKey>
            {
                new SqlKey(FieldTitle1, Title1),
                new SqlKey(FieldTitle2, Title2),
                new SqlKey(FieldDirectory, Directory),
                new SqlKey(FieldHidden, Hidden),
                new SqlKey(FieldRating, Rating),
                new SqlKey(FieldProgress, Progress),
                new SqlKey(FieldLastVisit, LastVisit),
                new SqlKey(FieldLastPosition, LastPosition),
                new SqlKey(FieldCoverFileName, CoverFileName),
                new SqlKey(FieldTags, Tags, blob: true),
                new SqlKey(FieldImageAspectRatios, ImageAspectRatios, blob: true),
            };

            if (Id < 0)
            {
                long rowid = await DatabaseManager.Insert(db, DatabaseManager.ComicTable, keys);

                // Retrieve ID from inserted row.
                SqliteCommand command = DatabaseManager.Connection.CreateCommand();
                command.CommandText = "SELECT " + FieldId + " FROM " +
                    DatabaseManager.ComicTable + " WHERE ROWID=$rowid";
                command.Parameters.AddWithValue("$rowid", rowid);

                await ComicDataManager.WaitLock(db);
                Id = (long)command.ExecuteScalar();
                ComicDataManager.ReleaseLock(db);

                command.Dispose();
            }
            else
            {
                await Save(db, keys);
            }
        }

        public async Task SaveBasic(LockContext db)
        {
            List<SqlKey> keys = new List<SqlKey>
            {
                new SqlKey(FieldTitle1, Title1),
                new SqlKey(FieldTitle2, Title2),
                new SqlKey(FieldDirectory, Directory),
                new SqlKey(FieldHidden, Hidden),
                new SqlKey(FieldRating, Rating),
                new SqlKey(FieldProgress, Progress),
                new SqlKey(FieldLastVisit, LastVisit),
                new SqlKey(FieldLastPosition, LastPosition),
                new SqlKey(FieldCoverFileName, CoverFileName),
                new SqlKey(FieldTags, Tags, blob: true),
            };

            await Save(db, keys);
        }

        public async Task SaveImageAspectRatios(LockContext db)
        {
            List<SqlKey> keys = new List<SqlKey>
            {
                new SqlKey(FieldImageAspectRatios, ImageAspectRatios, blob: true)
            };

            await Save(db, keys);
        }
    };

    class ComicDataManager
    {
        private const string COMIC_INFO_FILE_NAME = "info.txt";
        private static readonly SemaphoreSlim m_table_lock = new SemaphoreSlim(1);
        private static readonly Utils.CancellationLock m_update_lock = new Utils.CancellationLock();

        public static async Task WaitLock(LockContext db)
        {
            int depth = Interlocked.Increment(ref db.ComicTableLockDepth);

            System.Diagnostics.Debug.Assert(depth > 0);

            if (depth == 1)
            {
                await m_table_lock.WaitAsync();
            }
        }

        public static void ReleaseLock(LockContext db)
        {
            int depth = Interlocked.Decrement(ref db.ComicTableLockDepth);

            System.Diagnostics.Debug.Assert(depth >= 0);

            if (depth == 0)
            {
                m_table_lock.Release();
            }
        }

        public static async Task<ComicData> From(LockContext db, SqliteDataReader query)
        {
            // Non-blob fields.
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
                CoverFileName = query.GetString(9),
            };

            // Blob fields.
            bool reload_info = false;

            // Tags
            try
            {
                comic.Tags = (List<TagData>)await Utils.Methods.DeserializeFromStream(query.GetStream(10));
            }
            catch (SerializationException)
            {
                comic.Tags = new List<TagData>();
                reload_info = true;
            }

            if (reload_info)
            {
                await UpdateInfo(comic);
                await comic.SaveBasic(db);
            }

            // ImageAspectRatios
            try
            {
                comic.ImageAspectRatios = (List<double>)await Utils.Methods.DeserializeFromStream(query.GetStream(11));
            }
            catch (SerializationException)
            {
                comic.ImageAspectRatios= new List<double>();
                await comic.SaveImageAspectRatios(db);
            }

            return comic;
        }

        private static async Task<ComicData> From(LockContext db, string col, object entry)
        {
            SqliteCommand command = new SqliteCommand();
            command.Connection = DatabaseManager.Connection;
            command.CommandText = "SELECT * FROM " + DatabaseManager.ComicTable +
                " WHERE " + col + "=@entry LIMIT 1";
            command.Parameters.AddWithValue("@entry", entry);

            await ComicDataManager.WaitLock(db);
            try
            {
                SqliteDataReader query = command.ExecuteReader();
                if (!query.Read()) return null;
                return await From(db, query);
            }
            finally
            {
                ComicDataManager.ReleaseLock(db);
            }
        }

        public static async Task<ComicData> FromId(LockContext db, long id)
        {
            return await From(db, ComicData.FieldId, id);
        }

        public static async Task<ComicData> FromDirectory(LockContext db, string dir)
        {
            return await From(db, ComicData.FieldDirectory, dir);
        }

        private static async Task<ComicData> New(LockContext db, string title1, string title2, string dir)
        {
            ComicData comic = new ComicData
            {
                Title1 = title1,
                Title2 = title2,
                Directory = dir
            };
            await comic.Save(db);
            return comic;
        }

        private static async Task RemoveWithDirectory(LockContext db, string dir)
        {
            SqliteCommand command = new SqliteCommand();
            command.Connection = DatabaseManager.Connection;
            command.CommandText = @"DELETE FROM " + DatabaseManager.ComicTable +
                " WHERE " + ComicData.FieldDirectory + " LIKE @pattern";
            command.Parameters.AddWithValue("@pattern", dir + "%");

            await ComicDataManager.WaitLock(db);
            command.ExecuteNonQuery();
            ComicDataManager.ReleaseLock(db);
        }

        public static async RawTask Update(LockContext db, bool lazy_load)
        {
            await m_update_lock.WaitAsync();
            try
            {
                // get root folders
                await XmlDatabaseManager.WaitLock();
                List<string> root_folders = new List<string>(XmlDatabase.Settings.ComicFolders.Count);

                foreach (string folder_path in XmlDatabase.Settings.ComicFolders)
                {
                    root_folders.Add(folder_path);
                }

                // get all folders and subfolders
                XmlDatabaseManager.ReleaseLock();
                List<StorageFolder> all_folders = new List<StorageFolder>();

                foreach (string folder_path in root_folders)
                {
                    StorageFolder folder = await Utils.Methods.TryGetFolder(folder_path);

                    // remove unreachable folders from database
                    if (folder == null)
                    {
                        await XmlDatabaseManager.WaitLock();
                        XmlDatabase.Settings.ComicFolders.Remove(folder_path);
                        XmlDatabaseManager.ReleaseLock();
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

                Utils.TaskQueue.TaskQueueManager.AppendTask(XmlDatabaseManager.SaveSealed(XmlDatabaseItem.Settings));

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

                    await ComicDataManager.WaitLock(db); // Lock on.
                    SqliteDataReader query = command.ExecuteReader();

                    while (query.Read())
                    {
                        all_dir_in_lib.Add(query.GetString(0));
                    }
                    ComicDataManager.ReleaseLock(db); // Lock off.
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
                    await RemoveWithDirectory(db, dir);
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
                        if (update) await RemoveWithDirectory(db, dir);
                        return;
                    }

                    // Update or create a new one.
                    ComicData comic;

                    if (update)
                    {
                        comic = await FromDirectory(db, dir);
                    }
                    else
                    {
                        comic = await New(db, "", "", dir);
                    }

                    if (comic == null)
                    {
                        return;
                    }
                    
                    comic.Folder = folder;

                    // Try load comic info locally.
                    TaskResult r = await UpdateInfo(comic);

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

                    await comic.SaveBasic(db);
                }

                // generate a task queue.
                List<KeyValuePair<string, bool>> queue = new List<KeyValuePair<string, bool>>();

                foreach (string dir in dir_added)
                {
                    // "false" indicates the dir will be directly added to the database instead of updating an existing one.
                    queue.Add(new KeyValuePair<string, bool>(dir, false));
                }

                if (!lazy_load)
                {
                    List<string> dir_kept = Utils.Methods3<string, string, string>.Intersect(all_dir, all_dir_in_lib,
                        Utils.StringUtils.UniquePath, Utils.StringUtils.UniquePath,
                        new Utils.StringUtils.DefaultEqualityComparer()).ToList();

                    foreach (string dir in dir_kept)
                    {
                        // "true" indicates the item with the same dir in the database will be updated. no new items will be added.
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

        public static async Task Hide(LockContext db, ComicData comic)
        {
            comic.Hidden = true;
            await comic.SaveBasic(db);
        }

        public static async Task Unhide(LockContext db, ComicData comic)
        {
            comic.Hidden = false;
            await comic.SaveBasic(db);
        }

        // info
        public static string InfoString(ComicData comic)
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
            TaskResult res = await CompleteInfoFile(comic, true);

            if (res.ExceptionType != TaskException.Success)
            {
                return new TaskResult(TaskException.Failure);
            }

            string text = InfoString(comic);
            IBuffer buffer = Utils.Methods.GetBufferFromString(text);
            await FileIO.WriteBufferAsync(comic.InfoFile, buffer);
            return new TaskResult();
        }

        public static void ParseInfo(string text, ComicData comic)
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

        private static async RawTask CompleteInfoFile(ComicData comic, bool create_if_not_exists)
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

            TaskResult res = await CompleteFolder(comic);

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
            TaskResult res = await CompleteInfoFile(comic, false);

            if (res.ExceptionType != TaskException.Success)
            {
                return res;
            }

            string content = await FileIO.ReadTextAsync(comic.InfoFile);
            ParseInfo(content, comic);
            return new TaskResult();
        }

        private static async RawTask CompleteFolder(ComicData comic)
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

        public static async RawTask UpdateImages(LockContext db, ComicData comic, bool cover = false)
        {
            // Try complete comic folder.
            await XmlDatabaseManager.WaitLock();
            try
            {
                if (comic == null)
                {
                    return new TaskResult(TaskException.InvalidParameters, fatal: true);
                }

                TaskResult res = await CompleteFolder(comic);

                if (!res.Successful)
                {
                    return new TaskResult(TaskException.NoPermission);
                }
            }
            finally
            {
                XmlDatabaseManager.ReleaseLock();
            }

            // Try load cover on cover=true.
            if (cover && comic.CoverFileName.Length > 0)
            {
                IStorageItem item = await comic.Folder.TryGetItemAsync(comic.CoverFileName);

                if (item != null && item.IsOfType(StorageItemTypes.File))
                {
                    StorageFile cover_file = (StorageFile)item;
                    comic.ImageFiles.Clear();
                    comic.ImageFiles.Add(cover_file);
                    return new TaskResult();
                }
            }

            // Load all images.
            QueryOptions queryOptions = new QueryOptions
            {
                FolderDepth = FolderDepth.Shallow,
                IndexerOption = IndexerOption.UseIndexerWhenAvailable,
                FileTypeFilter = { ".jpg", ".jpeg", ".png", ".bmp" }
            };

            var query = comic.Folder.CreateFileQueryWithOptions(queryOptions);
            var img_files = await query.GetFilesAsync();

            if (img_files.Count == 0)
            {
                return new TaskResult(TaskException.EmptySet);
            }

            // Sort by display name.
            comic.ImageFiles = img_files.OrderBy(x => x.DisplayName,
                new Utils.StringUtils.FileNameComparer()).ToList();

            comic.CoverFileName = comic.ImageFiles[0].Name;
            await comic.SaveBasic(db);
            return new TaskResult();
        }
    }
}
