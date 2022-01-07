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

namespace ComicReader.Database
{
    using RawTask = Task<Utils.TaskResult>;
    using SealedTask = Func<Task<Utils.TaskResult>, Utils.TaskResult>;
    using TaskResult = Utils.TaskResult;
    using TaskException = Utils.TaskException;
    
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

        // Fields.
        private List<SqlKey> AllFields
        {
            get
            {
                List<SqlKey> all = new List<SqlKey>();
                all.AddRange(BasicFields);
                all.AddRange(ImageAspectRatiosFields);
                return all;
            }
        }

        private List<SqlKey> BasicFields => new List<SqlKey>
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

        private List<SqlKey> ImageAspectRatiosFields => new List<SqlKey>
        {
            new SqlKey(FieldImageAspectRatios, ImageAspectRatios, blob: true),
        };

        // Not in database.
        public string Title => Title1.Length == 0 ?
            (Title2.Length == 0 ? "Untitled Collection" : Title2) :
            (Title2.Length == 0 ? Title1 : Title1 + " - " + Title2);
        public bool IsExternal = false;
        public StorageFolder Folder = null;
        public StorageFile InfoFile = null;
        public List<StorageFile> ImageFiles = new List<StorageFile>();

        public ComicData(long id = -1)
        {
            Id = id;
        }

        public void From(long id, string title1, string title2, string directory,
            bool hidden, int rating, int progress, DateTimeOffset last_visit,
            double last_position, string cover_file_name, List<TagData> tags, 
            List<double> image_aspect_ratios)
        {
            Id = id;
            Title1 = title1;
            Title2 = title2;
            Directory = directory;
            Hidden = hidden;
            Rating = rating;
            Progress = progress;
            LastVisit = last_visit;
            LastPosition = last_position;
            CoverFileName = cover_file_name;
            Tags = tags;
            ImageAspectRatios = image_aspect_ratios;
        }

        // Updating.
        public async Task<bool> Update(LockContext db)
        {
            if (Id < 0)
            {
                System.Diagnostics.Debug.Assert(false);
                return false;
            }

            ComicData comic = await ComicDataManager.FromId(db, Id);

            if (comic == null)
            {
                return false;
            }

            From(comic.Id, comic.Title1, comic.Title2, comic.Directory, comic.Hidden, comic.Rating,
                comic.Progress, comic.LastVisit, comic.LastPosition,
                comic.CoverFileName, comic.Tags, comic.ImageAspectRatios);
            return true;
        }

        // Saving.
        private async Task Save(LockContext db, List<SqlKey> keys)
        {
            System.Diagnostics.Debug.Assert(Id >= 0);
            SqlKey id = new SqlKey(FieldId, Id);
            await SqliteDatabaseManager.Update(db, SqliteDatabaseManager.ComicTable, id, keys);
        }

        public async Task Save(LockContext db)
        {
            if (Id < 0)
            {
                long rowid = await SqliteDatabaseManager.Insert(db, SqliteDatabaseManager.ComicTable, AllFields);

                // Retrieve ID from inserted row.
                SqliteCommand command = SqliteDatabaseManager.NewCommand();
                command.CommandText = "SELECT " + FieldId + " FROM " +
                    SqliteDatabaseManager.ComicTable + " WHERE ROWID=$rowid";
                command.Parameters.AddWithValue("$rowid", rowid);

                await ComicDataManager.WaitLock(db);
                Id = (long)command.ExecuteScalar();
                ComicDataManager.ReleaseLock(db);

                command.Dispose();
            }
            else
            {
                await Save(db, AllFields);
            }
        }

        public async Task SaveBasic(LockContext db)
        {
            await Save(db, BasicFields);
        }

        public async Task SaveImageAspectRatios(LockContext db)
        {
            await Save(db, ImageAspectRatiosFields);
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
            bool reset_basic = false;
            bool reset_image_aspect_ratios = false;

            // Non-blob fields.
            long id = query.GetInt64(0);
            string title1 = query.GetString(1);
            string title2 = query.GetString(2);
            string directory = query.GetString(3);
            bool hidden = query.GetBoolean(4);
            int rating = query.GetInt32(5);
            int progress = query.GetInt32(6);
            DateTimeOffset last_visit = query.GetDateTimeOffset(7);
            double last_position = query.GetDouble(8);
            string cover_file_name = query.GetString(9);

            // Blob fields.
            List<TagData> tags = new List<TagData>();
            List<double> image_aspect_ratios = new List<double>();

            // Tags
            try
            {
                tags = (List<TagData>)await Utils.C0.DeserializeFromStream(query.GetStream(10));
            }
            catch (SerializationException)
            {
                reset_basic = true;
            }

            // ImageAspectRatios
            try
            {
                image_aspect_ratios = (List<double>)await Utils.C0.DeserializeFromStream(query.GetStream(11));
            }
            catch (SerializationException)
            {
                reset_image_aspect_ratios = true;
            }

            // Create an instance of ComicData.
            ComicData comic = new ComicData();

            comic.From(id, title1, title2, directory, hidden, rating, progress,
                last_visit, last_position, cover_file_name, tags, image_aspect_ratios);

            // Post processes.
            if (reset_basic)
            {
                await UpdateInfo(comic);
                await comic.SaveBasic(db);
            }

            if (reset_image_aspect_ratios)
            {
                await comic.SaveImageAspectRatios(db);
            }

            return comic;
        }

        private static async Task<ComicData> From(LockContext db, string col, object entry)
        {
            SqliteCommand command = SqliteDatabaseManager.NewCommand();
            command.CommandText = "SELECT * FROM " + SqliteDatabaseManager.ComicTable +
                " WHERE " + col + "=@entry LIMIT 1";
            command.Parameters.AddWithValue("@entry", entry);

            await ComicDataManager.WaitLock(db);
            try
            {
                SqliteDataReader query = command.ExecuteReader();

                if (!query.Read())
                {
                    return null;
                }

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
            SqliteCommand command = SqliteDatabaseManager.NewCommand();
            command.CommandText = @"DELETE FROM " + SqliteDatabaseManager.ComicTable +
                " WHERE " + ComicData.FieldDirectory + " LIKE @pattern";
            command.Parameters.AddWithValue("@pattern", dir + "%");

            await ComicDataManager.WaitLock(db);
            command.ExecuteNonQuery();
            ComicDataManager.ReleaseLock(db);
        }

        public static SealedTask UpdateSealed(bool lazy_load)
        {
            return (RawTask _) => _Update(new LockContext(), lazy_load).Result;
        }

        private static async RawTask _Update(LockContext db, bool lazy_load)
        {
            await m_update_lock.WaitAsync();
            try
            {
                // Get all folder path of comics in the database.
                List<string> all_dir_in_lib = new List<string>();

                {
                    SqliteCommand command = SqliteDatabaseManager.NewCommand();
                    command.CommandText = "SELECT " + ComicData.FieldDirectory +
                        " FROM " + SqliteDatabaseManager.ComicTable;

                    await ComicDataManager.WaitLock(db); // Lock on.
                    SqliteDataReader query = await command.ExecuteReaderAsync();

                    while (query.Read())
                    {
                        all_dir_in_lib.Add(query.GetString(0));
                    }
                    ComicDataManager.ReleaseLock(db); // Lock off.
                }

                // Get all root folders from setting.
                await XmlDatabaseManager.WaitLock();
                List<string> root_folders = new List<string>(XmlDatabase.Settings.ComicFolders.Count);

                foreach (string folder_path in XmlDatabase.Settings.ComicFolders)
                {
                    root_folders.Add(folder_path);
                }
                XmlDatabaseManager.ReleaseLock();

                // Get all subfolders in root folders.
                var all_dir = new List<string>();

                foreach (string folder_path in root_folders)
                {
                    // Cancel this task if more requests have come in.
                    if (m_update_lock.CancellationRequested)
                    {
                        return new TaskResult(TaskException.Cancellation);
                    }

                    StorageFolder root_folder = await Utils.C0.TryGetFolder(folder_path);

                    // Remove unreachable folders from database.
                    if (root_folder == null)
                    {
                        await XmlDatabaseManager.WaitLock();
                        XmlDatabase.Settings.ComicFolders.Remove(folder_path);
                        XmlDatabaseManager.ReleaseLock();
                        continue;
                    }

                    // Create a folder query.
                    QueryOptions query_options;
                    IndexedState indexes_state = await root_folder.GetIndexedStateAsync();

                    if (indexes_state == IndexedState.FullyIndexed)
                    {
                        query_options = new QueryOptions
                        {
                            FolderDepth = FolderDepth.Deep,
                            IndexerOption = IndexerOption.OnlyUseIndexerAndOptimizeForIndexedProperties,
                        };
                    }
                    else
                    {
                        query_options = new QueryOptions
                        {
                            FolderDepth = FolderDepth.Deep,
                            IndexerOption = IndexerOption.UseIndexerWhenAvailable,
                        };
                    }

                    query_options.SetPropertyPrefetch(Windows.Storage.FileProperties.PropertyPrefetchOptions.BasicProperties, new string[] { });
                    StorageFolderQueryResult query = root_folder.CreateFolderQueryWithOptions(query_options);

                    uint start_index = 0;
                    const uint step_size = 1;

                    while (true)
                    {
                        IReadOnlyList<StorageFolder> subfolders = await query.GetFoldersAsync(start_index, step_size); // Really slow.

                        var all_folders = new List<StorageFolder>(subfolders);

                        if (start_index == 0)
                        {
                            all_folders.Add(root_folder);
                        }

                        if (all_folders.Count == 0)
                        {
                            break;
                        }

                        // Extracts StorageFolder.Path into a new string list.
                        var all_dir_iter = new List<string>(all_folders.Count);

                        for (int i = 0; i < all_folders.Count; ++i)
                        {
                            all_dir_iter.Add(all_folders[i].Path);
                        }

                        all_dir.AddRange(all_dir_iter);

                        // Generate a task queue for updating.
                        var queue = new List<KeyValuePair<StorageFolder, bool>>();

                        // Get folders added.
                        List<string> dir_added = Utils.C3<string, string, string>.Except(all_dir_iter, all_dir_in_lib,
                            Utils.StringUtils.UniquePath, Utils.StringUtils.UniquePath,
                            new Utils.StringUtils.DefaultEqualityComparer()).ToList();

                        foreach (string dir in dir_added)
                        {
                            StorageFolder folder = await Utils.C0.TryGetFolder(root_folder, dir);

                            // "False" indicates the dir will be directly added to
                            // the database instead of updating an existing one.
                            queue.Add(new KeyValuePair<StorageFolder, bool>(folder, false));
                        }

                        if (!lazy_load)
                        {
                            List<string> dir_kept = Utils.C3<string, string, string>.Intersect(all_dir_iter, all_dir_in_lib,
                                Utils.StringUtils.UniquePath, Utils.StringUtils.UniquePath,
                                new Utils.StringUtils.DefaultEqualityComparer()).ToList();

                            foreach (string dir in dir_kept)
                            {
                                StorageFolder folder = await Utils.C0.TryGetFolder(root_folder, dir);

                                // "True" indicates the item with the same dir in the
                                // database will be updated. No new items will be added.
                                queue.Add(new KeyValuePair<StorageFolder, bool>(folder, true));
                            }
                        }

                        foreach (KeyValuePair<StorageFolder, bool> p in queue)
                        {
                            // Cancel this task if more requests have come in.
                            if (m_update_lock.CancellationRequested)
                            {
                                return new TaskResult(TaskException.Cancellation);
                            }

                            await _AddOrUpdateFolder(db, p.Key, p.Value);
                        }

                        start_index += step_size;
                    }
                }

                // Get folders removed.
                List<string> dir_removed = Utils.C3<string, string, string>.Except(all_dir_in_lib, all_dir,
                    Utils.StringUtils.UniquePath, Utils.StringUtils.UniquePath,
                    new Utils.StringUtils.DefaultEqualityComparer()).ToList();

                // remove items from database.
                foreach (string dir in dir_removed)
                {
                    await RemoveWithDirectory(db, dir);
                }

                return new TaskResult();
            }
            finally
            {
                m_update_lock.Release();
            }
        }

        private static async Task _AddOrUpdateFolder(LockContext db, StorageFolder folder, bool update)
        {
            // Exclude folders which do not directly contain images. (120s per 1k folders)
            QueryOptions queryOptions = new QueryOptions
            {
                FolderDepth = FolderDepth.Shallow,
                IndexerOption = IndexerOption.UseIndexerWhenAvailable,
                FileTypeFilter = { ".jpg", ".jpeg", ".png", ".bmp" }
            };

            StorageFileQueryResult query = folder.CreateFileQueryWithOptions(queryOptions);
            uint file_count = await query.GetItemCountAsync();

            if (file_count == 0)
            {
                if (update)
                {
                    await RemoveWithDirectory(db, folder.Path);
                }

                return;
            }

            // Update or create a new one.
            ComicData comic;

            if (update)
            {
                comic = await FromDirectory(db, folder.Path);
            }
            else
            {
                comic = await New(db, "", "", folder.Path);
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
                List<string> tags = folder.Path.Split("\\").ToList();

                if (tags.Count > 1)
                {
                    TagData default_tag = new TagData
                    {
                        Name = "Default",
                        Tags = tags.Skip(1).ToHashSet(),
                    };

                    comic.Tags.Add(default_tag);
                }
            }

            await comic.SaveBasic(db);
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
            text += "Title1: " + comic.Title1 + "\n";
            text += "Title2: " + comic.Title2 + "\n";
            text += TagString(comic);
            return text;
        }

        public static string TagString(ComicData comic)
        {
            string text = "";

            foreach (TagData tag in comic.Tags)
            {
                text += tag.Name + ": " + Utils.StringUtils.Join("/", tag.Tags) + "\n";
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
            IBuffer buffer = Utils.C0.GetBufferFromString(text);
            await FileIO.WriteBufferAsync(comic.InfoFile, buffer);
            return new TaskResult();
        }

        public static void ParseInfo(string text, ComicData comic)
        {
            bool title1_set = false;
            bool title2_set = false;
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

                if (!title1_set && (name == "title1" || name == "title" || name == "t1" || name == "to"))
                {
                    title1_set = true;
                    comic.Title1 = Utils.StringUtils.Join("/", tag_data.Tags);
                }
                else if (!title2_set && (name == "title2" || name == "t2"))
                {
                    title2_set = true;
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

            StorageFolder folder = await Utils.C0.TryGetFolder(comic.Directory);

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
