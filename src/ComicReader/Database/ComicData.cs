using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
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

    public abstract class ComicData
    {
        // Fields.
        public class Field
        {
            // Field comic.
            public const string Id = "id";
            public const string Type = "type";
            public const string Location = "location";
            public const string Title1 = "title1";
            public const string Title2 = "title2";
            public const string Hidden = "hidden";
            public const string Rating = "rating";
            public const string Progress = "progress";
            public const string LastVisit = "last_visit";
            public const string LastPosition = "last_pos";
            public const string ImageAspectRatios = "image_aspect_ratios";
            public const string CoverFileName = "cover_file_name";

            // Field tag category.
            public class TagCategory
            {
                public const string Id = "id";
                public const string Name = "name";
                public const string ComicId = "comic_id";
            }

            // Field tag.
            public class Tag
            {
                public const string Content = "content";
                public const string ComicId = "comic_id";
                public const string TagCategoryId = "cate_id";
            }
        }

        private SqlKey KeyType => new SqlKey(Field.Type, Type);
        private SqlKey KeyLocation => new SqlKey(Field.Location, Location);
        private SqlKey KeyTitle1 => new SqlKey(Field.Title1, Title1);
        private SqlKey KeyTitle2 => new SqlKey(Field.Title2, Title2);
        private SqlKey KeyHidden => new SqlKey(Field.Hidden, Hidden);
        private SqlKey KeyRating => new SqlKey(Field.Rating, Rating);
        private SqlKey KeyProgress => new SqlKey(Field.Progress, Progress);
        private SqlKey KeyLastVisit => new SqlKey(Field.LastVisit, LastVisit);
        private SqlKey KeyLastPosition => new SqlKey(Field.LastPosition, LastPosition);
        private SqlKey KeyImageAspectRatios => new SqlKey(Field.ImageAspectRatios, ImageAspectRatios, blob: true);
        private SqlKey KeyCoverFileName => new SqlKey(Field.CoverFileName, CoverFileName);

        private List<SqlKey> AllFields => new List<SqlKey>
        {
            KeyType,
            KeyLocation,
            KeyTitle1,
            KeyTitle2,
            KeyHidden,
            KeyRating,
            KeyProgress,
            KeyLastVisit,
            KeyLastPosition,
            KeyImageAspectRatios,
            KeyCoverFileName,
        };

        // Enums.
        protected enum ComicType : int
        {
            Folder = 1,
            Zip = 2,
        }

        // Local fields.
        public long Id { get; private set; }
        private ComicType Type;
        public string Location { get; protected set; } = "";
        public string Title1 { get; protected set; } = "";
        public string Title2 { get; protected set; } = "";
        public bool Hidden { get; protected set; } = false;
        public int Rating { get; protected set; } = -1;
        public int Progress { get; protected set; } = -1;
        public DateTimeOffset LastVisit { get; protected set; } = DateTimeOffset.MinValue;
        public double LastPosition { get; protected set; } = 0.0;
        public List<double> ImageAspectRatios = new List<double>();
        protected string CoverFileName = "";

        // Foriegn fields.
        public List<TagData> Tags = new List<TagData>();

        // Not in database.
        private static string m_UntitledCollectionString = null;
        protected static string UntitledCollectionString
        {
            get
            {
                if (m_UntitledCollectionString == null)
                {
                    m_UntitledCollectionString = Utils.C0.TryGetResourceString("UntitledCollection");
                }

                return m_UntitledCollectionString;
            }
        }

        public string Title => Title1.Length == 0 ?
            (Title2.Length == 0 ? UntitledCollectionString : Title2) :
            (Title2.Length == 0 ? Title1 : Title1 + " - " + Title2);
        public bool IsExternal { get; protected set; } = false;
        public abstract bool IsEditable { get; }
        public abstract int ImageCount { get; }

        protected ComicData(ComicType type)
        {
            Id = -1;
            Type = type;
        }

        private void From(long id, string location, string title1, string title2,
            bool hidden, int rating, int progress, DateTimeOffset last_visit,
            double last_position, string cover_file_name, List<TagData> tags,
            List<double> image_aspect_ratios)
        {
            Id = id;
            Location = location;
            Title1 = title1;
            Title2 = title2;
            Hidden = hidden;
            Rating = rating;
            Progress = progress;
            LastVisit = last_visit;
            LastPosition = last_position;
            ImageAspectRatios = image_aspect_ratios;
            CoverFileName = cover_file_name;
            Tags = tags;
        }

        // Updating.
        public async Task<bool> Update(LockContext db)
        {
            if (Id < 0)
            {
                System.Diagnostics.Debug.Assert(false);
                return false;
            }

            ComicData comic = await ComicData.Manager.FromId(db, Id);

            if (comic == null)
            {
                return false;
            }

            System.Diagnostics.Debug.Assert(Type == comic.Type);

            From(comic.Id, comic.Location, comic.Title1, comic.Title2,
                comic.Hidden, comic.Rating, comic.Progress, comic.LastVisit,
                comic.LastPosition, comic.CoverFileName, comic.Tags,
                comic.ImageAspectRatios);
            return true;
        }

        // Saving.
        private async Task InternalSaveTags(LockContext db, bool remove_old = true)
        {
            if (remove_old)
            {
                SqliteCommand command = SqliteDatabaseManager.NewCommand();
                command.CommandText = "DELETE FROM " + SqliteDatabaseManager.TagCategoryTable
                    + " WHERE " + Field.TagCategory.ComicId + "=" + Id;
                command.ExecuteNonQuery();
            }

            foreach (TagData category in Tags)
            {
                // Insert to tag category table.
                string name = category.Name;

                List<SqlKey> tag_category_fields = new List<SqlKey>
                {
                    new SqlKey(Field.TagCategory.Name, name),
                    new SqlKey(Field.TagCategory.ComicId, Id),
                };

                await ComicData.Manager.WaitLock(db);
                long rowid = await SqliteDatabaseManager.Insert(SqliteDatabaseManager.TagCategoryTable, tag_category_fields);
                ComicData.Manager.ReleaseLock(db);

                // Retrieve ID from inserted row.
                SqliteCommand command = SqliteDatabaseManager.NewCommand();
                command.CommandText = "SELECT " + Field.TagCategory.Id + " FROM " +
                    SqliteDatabaseManager.TagCategoryTable + " WHERE ROWID=$rowid";
                command.Parameters.AddWithValue("$rowid", rowid);

                long tag_category_id = (long)command.ExecuteScalar();

                foreach (string tag in category.Tags)
                {
                    List<SqlKey> tag_fields = new List<SqlKey>
                    {
                        new SqlKey(Field.Tag.Content, tag),
                        new SqlKey(Field.Tag.ComicId, Id),
                        new SqlKey(Field.Tag.TagCategoryId, tag_category_id),
                    };

                    await ComicData.Manager.WaitLock(db);
                    _ = await SqliteDatabaseManager.Insert(SqliteDatabaseManager.TagTable, tag_fields);
                    ComicData.Manager.ReleaseLock(db);
                }
            }
        }

        private async Task InternalInsert(LockContext db)
        {
            // Insert to comic table.
            await ComicData.Manager.WaitLock(db);
            long rowid = await SqliteDatabaseManager.Insert(SqliteDatabaseManager.ComicTable, AllFields);
            ComicData.Manager.ReleaseLock(db);

            // Retrieve ID from inserted row.
            SqliteCommand command = SqliteDatabaseManager.NewCommand();
            command.CommandText = "SELECT " + Field.Id + " FROM " +
                SqliteDatabaseManager.ComicTable + " WHERE ROWID=$rowid";
            command.Parameters.AddWithValue("$rowid", rowid);

            await ComicData.Manager.WaitLock(db);
            Id = (long)command.ExecuteScalar();
            ComicData.Manager.ReleaseLock(db);

            // Cleanups.
            command.Dispose();

            // Insert tags.
            await InternalSaveTags(db, remove_old: false);
        }

        private async Task Save(LockContext db, List<SqlKey> keys, bool save_tags)
        {
            await ComicData.Manager.WaitLock(db); // Lock on.
            try
            {
                if (Id < 0)
                {
                    await InternalInsert(db);
                    return;
                }

                if (keys.Count > 0)
                {
                    SqlKey id = new SqlKey(Field.Id, Id);
                    bool r = await SqliteDatabaseManager.Update(SqliteDatabaseManager.ComicTable, id, keys);
                    System.Diagnostics.Debug.Assert(r);
                }

                if (save_tags)
                {
                    await InternalSaveTags(db);
                }
            }
            finally
            {
                ComicData.Manager.ReleaseLock(db); // Lock off.
            }
        }

        public async Task SaveAll(LockContext db)
        {
            await Save(db, AllFields, save_tags: true);
        }

        public async Task SaveBasic(LockContext db)
        {
            List<SqlKey> fields = new List<SqlKey>
            {
                KeyType,
                KeyLocation,
                KeyTitle1,
                KeyTitle2,
                KeyHidden,
                KeyRating,
                KeyProgress,
                KeyLastVisit,
                KeyLastPosition,
                KeyCoverFileName,
            };

            await Save(db, fields, save_tags: false);
        }

        public async Task SaveTags(LockContext db)
        {
            List<SqlKey> fields = new List<SqlKey>();

            await Save(db, fields, save_tags: true);
        }

        public async Task SaveImageAspectRatios(LockContext db)
        {
            List<SqlKey> fields = new List<SqlKey>
            {
                KeyImageAspectRatios,
            };

            await Save(db, fields, save_tags: false);
        }

        public async Task SaveProgress(LockContext db, int progress, double last_position)
        {
            Progress = progress;
            LastPosition = last_position;

            List<SqlKey> fields = new List<SqlKey>
            {
                KeyProgress,
                KeyLastPosition,
            };

            await Save(db, fields, save_tags: false);
        }

        public async Task SaveLastVisit(LockContext db, DateTimeOffset last_visit)
        {
            LastVisit = last_visit;

            List<SqlKey> fields = new List<SqlKey>
            {
                KeyLastVisit,
            };

            await Save(db, fields, save_tags: false);
        }

        public async Task SaveRating(LockContext db, int rating)
        {
            Rating = rating;

            List<SqlKey> fields = new List<SqlKey>
            {
                KeyRating,
            };

            await Save(db, fields, save_tags: false);
        }

        //
        public abstract Task<IRandomAccessStream> GetImageStream(int index);

        public SealedTask SaveInfoFileSealed() =>
            (RawTask _) => SaveInfoFile().Result;

        protected abstract RawTask SaveInfoFile();

        public string TagString()
        {
            string text = "";

            foreach (TagData tag in Tags)
            {
                text += tag.Name + ": " + Utils.StringUtils.Join("/", tag.Tags) + "\n";
            }

            return text;
        }

        public string InfoString()
        {
            string text = "";
            text += "Title1: " + Title1 + "\n";
            text += "Title2: " + Title2 + "\n";
            text += TagString();
            return text;
        }

        public abstract RawTask UpdateInfo();

        public void ParseInfo(string text)
        {
            bool title1_set = false;
            bool title2_set = false;
            Title1 = "";
            Title2 = "";
            Tags.Clear();
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
                    Title1 = Utils.StringUtils.Join("/", tag_data.Tags);
                }
                else if (!title2_set && (name == "title2" || name == "t2"))
                {
                    title2_set = true;
                    Title2 = Utils.StringUtils.Join("/", tag_data.Tags);
                }
                else
                {
                    // combine duplicated tag
                    bool duplicated = false;

                    foreach (TagData t in Tags)
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
                        Tags.Add(tag_data);
                    }
                }
            }
        }

        public abstract RawTask UpdateImages(LockContext db, bool cover = false);

        public class Manager
        {
            public const string ComicInfoFileName = "info.txt";

            // Subscriptions.
            public static Action<LockContext> OnUpdated;

            // Observers.
            public static bool IsRescanning { get; private set; } = false;

            // Resources.
            private static string m_DefaultTagsString = null;
            private static string DefaultTagsString
            {
                get
                {
                    if (m_DefaultTagsString == null)
                    {
                        Utils.C0.Sync(delegate
                        {
                            m_DefaultTagsString = Utils.C0.TryGetResourceString("DefaultTags");
                        }).Wait();
                    }

                    return m_DefaultTagsString;
                }
            }

            // Locks.
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
                // Directly imported fields.
                long id = query.GetInt64(0);
                ComicType type = (ComicType)query.GetInt64(1);
                string location = query.GetString(2);
                string title1 = query.GetString(3);
                string title2 = query.GetString(4);
                bool hidden = query.GetBoolean(5);
                int rating = query.GetInt32(6);
                int progress = query.GetInt32(7);
                DateTimeOffset last_visit = query.GetDateTimeOffset(8);
                double last_position = query.GetDouble(9);
                string cover_file_name = query.GetString(11);

                // Tags
                List<TagData> tags = new List<TagData>();

                SqliteCommand tag_category_command = SqliteDatabaseManager.NewCommand();
                tag_category_command.CommandText = "SELECT * FROM " + SqliteDatabaseManager.TagCategoryTable +
                    " WHERE " + ComicData.Field.TagCategory.ComicId + "=" + id.ToString();

                await ComicData.Manager.WaitLock(db);
                SqliteDataReader tag_category_query = tag_category_command.ExecuteReader();
                ComicData.Manager.ReleaseLock(db);

                while (tag_category_query.Read())
                {
                    long tag_category_id = tag_category_query.GetInt64(0);
                    string name = tag_category_query.GetString(1);

                    TagData tag_data = new TagData
                    {
                        Name = name
                    };

                    SqliteCommand tag_command = SqliteDatabaseManager.NewCommand();
                    tag_command.CommandText = "SELECT * FROM " + SqliteDatabaseManager.TagTable +
                        " WHERE " + ComicData.Field.Tag.TagCategoryId + "=" + tag_category_id.ToString();

                    await ComicData.Manager.WaitLock(db);
                    SqliteDataReader tag_query = tag_command.ExecuteReader();
                    ComicData.Manager.ReleaseLock(db);

                    while (tag_query.Read())
                    {
                        string tag = tag_query.GetString(0);
                        tag_data.Tags.Add(tag);
                    }

                    tags.Add(tag_data);
                    tag_command.Dispose();
                }

                tag_category_command.Dispose();

                // ImageAspectRatios
                bool reset_image_aspect_ratios = false;
                List<double> image_aspect_ratios = new List<double>();

                try
                {
                    image_aspect_ratios = (List<double>)
                        await Utils.C0.DeserializeFromStream(query.GetStream(10));
                }
                catch (SerializationException)
                {
                    reset_image_aspect_ratios = true;
                }

                // Create an instance of ComicData.
                ComicData comic;

                switch (type)
                {
                    case ComicType.Folder:
                        comic = ComicFolderData.FromEmpty();
                        break;
                    case ComicType.Zip:
                        throw new NotImplementedException();
                    default:
                        System.Diagnostics.Debug.Assert(false);
                        return null;
                }

                comic.From(id, location, title1, title2, hidden, rating,
                    progress, last_visit, last_position, cover_file_name, tags,
                    image_aspect_ratios);

                // Post-procedures.
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

                await ComicData.Manager.WaitLock(db);
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
                    ComicData.Manager.ReleaseLock(db);
                }
            }

            public static async Task<ComicData> FromId(LockContext db, long id)
            {
                return await From(db, ComicData.Field.Id, id);
            }

            public static async Task<ComicData> FromLocation(LockContext db, string location)
            {
                return await From(db, ComicData.Field.Location, location);
            }

            private static async Task RemoveWithLocation(LockContext db, string location)
            {
                SqliteCommand command = SqliteDatabaseManager.NewCommand();
                command.CommandText = "DELETE FROM " + SqliteDatabaseManager.ComicTable +
                    " WHERE " + ComicData.Field.Location + " LIKE @pattern";
                command.Parameters.AddWithValue("@pattern", location + "%");

                await ComicData.Manager.WaitLock(db);
                command.ExecuteNonQuery();
                ComicData.Manager.ReleaseLock(db);
            }

            public static SealedTask UpdateSealed(bool lazy_load)
            {
                return (RawTask _) => IntenalUpdate(new LockContext(), lazy_load).Result;
            }

            private static async RawTask IntenalUpdate(LockContext db, bool lazy_load)
            {
                await m_update_lock.WaitAsync();
                try
                {
                    Utils.Debug.Log("Updating comics (lazy_load=" + lazy_load.ToString() + ")");

                    if (!lazy_load)
                    {
                        IsRescanning = true;
                        OnUpdated?.Invoke(db);
                    }

                    // Get all folder path of comics in the database.
                    List<string> dir_in_lib = new List<string>();

                    {
                        SqliteCommand command = SqliteDatabaseManager.NewCommand();
                        command.CommandText = "SELECT " + ComicData.Field.Location +
                            " FROM " + SqliteDatabaseManager.ComicTable;

                        await ComicData.Manager.WaitLock(db); // Lock on.
                        SqliteDataReader query = await command.ExecuteReaderAsync();

                        while (query.Read())
                        {
                            dir_in_lib.Add(query.GetString(0));
                        }
                        ComicData.Manager.ReleaseLock(db); // Lock off.
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
                    var dir_exist = new List<string>();
                    var dir_ignore = new List<string>();
                    Utils.Stopwatch watch = new Utils.Stopwatch();
                    watch.Start();

                    foreach (string folder_path in root_folders)
                    {
                        Utils.Debug.Log("Scanning root folder '" + folder_path + "'.");

                        StorageFolder root_folder = await Utils.C0.TryGetFolder(folder_path);

                        // Remove unreachable folders from database.
                        if (root_folder == null)
                        {
                            Utils.Debug.Log("Failed to get folder '" + folder_path + "', skipped.");

                            await XmlDatabaseManager.WaitLock();
                            XmlDatabase.Settings.ComicFolders.Remove(folder_path);
                            XmlDatabaseManager.ReleaseLock();
                            continue;
                        }

                        var ctx = new Utils.Win32IO.SubFolderDeepContext(folder_path);
                        bool initial_loop = true;
                        List<string> dir_found = new List<string>();

                        while (ctx.Search(dir_found, 500))
                        {
                            if (initial_loop)
                            {
                                dir_found.Add(folder_path);
                                initial_loop = false;
                            }

                            // Cancel this task if more requests have come in.
                            if (m_update_lock.CancellationRequested)
                            {
                                return new TaskResult(TaskException.Cancellation);
                            }

                            Utils.Debug.Log("Scanning " + dir_found.Count.ToString() + " folders.");

                            if (dir_found.Count == 0)
                            {
                                continue;
                            }

                            dir_exist.AddRange(dir_found);

                            // Generate a task queue for updating.
                            var queue = new List<KeyValuePair<string, bool>>();

                            // Get folders added.
                            List<string> dir_added = Utils.C3<string, string, string>.Except(dir_found, dir_in_lib,
                                Utils.StringUtils.UniquePath, Utils.StringUtils.UniquePath,
                                new Utils.C1<string>.DefaultEqualityComparer()).ToList();

                            foreach (string dir in dir_added)
                            {
                                // "False" indicates the dir will be directly added to
                                // the database instead of updating an existing one.
                                queue.Add(new KeyValuePair<string, bool>(dir, false));
                            }

                            if (!lazy_load)
                            {
                                List<string> dir_kept = Utils.C3<string, string, string>.Intersect(dir_found, dir_in_lib,
                                    Utils.StringUtils.UniquePath, Utils.StringUtils.UniquePath,
                                    new Utils.C1<string>.DefaultEqualityComparer()).ToList();

                                foreach (string dir in dir_kept)
                                {
                                    // "True" indicates the item with the same dir in the
                                    // database will be updated. No new items will be added.
                                    queue.Add(new KeyValuePair<string, bool>(dir, true));
                                }
                            }

                            for (int i = 0; i < queue.Count; ++i)
                            {
                                // Cancel this task if more requests have come in.
                                if (m_update_lock.CancellationRequested)
                                {
                                    return new TaskResult(TaskException.Cancellation);
                                }

                                var p = queue[i];
                                IntenalAddOrUpdateFolder(db, p.Key, p.Value).Wait();

                                if (watch.LapSpan().TotalSeconds > 1.5)
                                {
                                    OnUpdated?.Invoke(db);
                                    watch.Lap();
                                }
                            }
                        }

                        foreach (string dir in ctx.NoAccessFolders)
                        {
                            dir_ignore.Add(dir.ToLower());
                        }
                    }

                    // Get removed folders.
                    List<string> dir_removed = Utils.C3<string, string, string>.Except(dir_in_lib, dir_exist,
                        Utils.StringUtils.UniquePath, Utils.StringUtils.UniquePath,
                        new Utils.C1<string>.DefaultEqualityComparer()).ToList();

                    // Remove folders from database.
                    foreach (string dir in dir_removed)
                    {
                        // Skip directories in ignoring list.
                        string dir_lower = dir.ToLower();
                        bool ignore = false;

                        foreach (string base_dir in dir_ignore)
                        {
                            if (Utils.StringUtils.PathContain(base_dir, dir_lower))
                            {
                                ignore = true;
                                break;
                            }
                        }

                        if (ignore)
                        {
                            continue;
                        }

                        // Remove.
                        Utils.Debug.Log("Removing folder '" + dir + "'.");
                        await RemoveWithLocation(db, dir);

                        if (watch.LapSpan().TotalSeconds > 1.5)
                        {
                            OnUpdated?.Invoke(db);
                            watch.Lap();
                        }
                    }

                    return new TaskResult();
                }
                finally
                {
                    Utils.Debug.Log("Comic update stopped.");

                    IsRescanning = false;
                    OnUpdated?.Invoke(db);
                    m_update_lock.Release();
                }
            }

            private static async Task IntenalAddOrUpdateFolder(LockContext db, string path, bool update)
            {
                Utils.Debug.Log("Updating folder '" + path + "' (update=" + update.ToString() + ").");

                List<string> file_names = Utils.Win32IO.SubFiles(path, "*");
                Utils.Debug.Log(file_names.Count.ToString() + " images found.");
                bool info_file_exist = false;

                for (int i = file_names.Count - 1; i >= 0; i--)
                {
                    string file_path = file_names[i];
                    string filename = Utils.StringUtils.ItemNameFromPath(file_path).ToLower();
                    string extension = Utils.StringUtils.FilenameExtensionFromFilename(filename);

                    if (filename == ComicInfoFileName)
                    {
                        info_file_exist = true;
                    }

                    if (!Utils.AppInfoProvider.IsSupportedImageExtension(extension))
                    {
                        file_names.RemoveAt(i);
                    }
                }

                if (file_names.Count == 0)
                {
                    if (update)
                    {
                        await RemoveWithLocation(db, path);
                    }

                    return;
                }

                // Update or create a new one.
                ComicData comic;

                if (update)
                {
                    comic = await FromLocation(db, path);
                }
                else
                {
                    comic = ComicFolderData.FromEmpty();
                    comic.Location = path;
                }

                if (comic == null)
                {
                    return;
                }

                if (info_file_exist)
                {
                    // Load comic info locally.
                    TaskResult r = await comic.UpdateInfo();

                    if (r.Successful)
                    {
                        await comic.SaveAll(db);
                        return;
                    }
                }

                if (update)
                {
                    return;
                }

                // Auto-imported properties.
                List<string> tags = path.Split("\\").ToList();
                comic.Title1 = tags[tags.Count - 1];

                if (tags.Count > 1)
                {
                    TagData default_tag = new TagData
                    {
                        Name = DefaultTagsString,
                        Tags = tags.Skip(1).ToHashSet(),
                    };

                    comic.Tags.Add(default_tag);
                }

                await comic.SaveAll(db);
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
        };
    };

    public class ComicFolderData : ComicData
    {
        private StorageFolder Folder = null;
        private StorageFile InfoFile = null;
        private List<StorageFile> ImageFiles = new List<StorageFile>();
        public override int ImageCount => ImageFiles.Count;
        public override bool IsEditable => !(IsExternal && InfoFile == null);

        private ComicFolderData() : base(ComicType.Folder) {}

        public static ComicData FromEmpty()
        {
            return new ComicFolderData();
        }

        public static async Task<ComicData> FromExternal(string directory, List<StorageFile> image_files, StorageFile info_file)
        {
            image_files.OrderBy(x => x.DisplayName,
                new Utils.StringUtils.FileNameComparer());

            ComicFolderData comic = new ComicFolderData
            {
                Location = directory,
                IsExternal = true,
                ImageFiles = image_files,
                InfoFile = info_file
            };

            if (info_file != null)
            {
                await comic.UpdateInfo();
            }

            return comic;
        }

        private async RawTask CompleteFolder()
        {
            if (Folder != null)
            {
                return new TaskResult();
            }

            if (Location == null)
            {
                return new TaskResult(TaskException.InvalidParameters);
            }

            StorageFolder folder = await Utils.C0.TryGetFolder(Location);

            if (folder == null)
            {
                return new TaskResult(TaskException.NoPermission);
            }

            Folder = folder;
            return new TaskResult();
        }

        private async RawTask CompleteInfoFile(bool create_if_not_exists)
        {
            if (InfoFile != null)
            {
                return new TaskResult();
            }

            if (IsExternal)
            {
                return create_if_not_exists ?
                    new TaskResult(TaskException.NoPermission) :
                    new TaskResult(TaskException.FileNotExists);
            }

            TaskResult res = await CompleteFolder();

            System.Diagnostics.Debug.Assert(res.ExceptionType == TaskException.Success);

            IStorageItem item = await Folder.TryGetItemAsync(Manager.ComicInfoFileName);

            if (item == null)
            {
                if (!create_if_not_exists)
                {
                    return new TaskResult(TaskException.FileNotExists);
                }

                InfoFile = await Folder.CreateFileAsync(Manager.ComicInfoFileName, CreationCollisionOption.FailIfExists);
                return new TaskResult();
            }

            if (!(item is StorageFile))
            {
                return new TaskResult(TaskException.NameCollision);
            }

            InfoFile = (StorageFile)item;
            return new TaskResult();
        }

        protected override async RawTask SaveInfoFile()
        {
            TaskResult r = await CompleteInfoFile(true);

            if (!r.Successful)
            {
                return new TaskResult(TaskException.Failure);
            }

            string text = InfoString();
            IBuffer buffer = Utils.C0.GetBufferFromString(text);
            await FileIO.WriteBufferAsync(InfoFile, buffer);
            return new TaskResult();
        }

        public override async RawTask UpdateInfo()
        {
            string info_text;

            if (InfoFile != null)
            {
                info_text = await FileIO.ReadTextAsync(InfoFile);
            }
            else
            {
                string path = Location + "\\" + Manager.ComicInfoFileName;

                try
                {
                    info_text = await Utils.Win32IO.ReadFileFromPath(path);
                }
                catch (IOException)
                {
                    return new TaskResult(TaskException.Failure);
                }
            }

            ParseInfo(info_text);
            return new TaskResult();
        }

        public override async Task<IRandomAccessStream> GetImageStream(int index)
        {
            try
            {
                return await ImageFiles[index].OpenAsync(FileAccessMode.Read);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
        }

        public override async RawTask UpdateImages(LockContext db, bool cover = false)
        {
            // Try complete comic folder.
            await XmlDatabaseManager.WaitLock();
            try
            {
                TaskResult res = await CompleteFolder();

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
            if (cover && CoverFileName.Length > 0)
            {
                IStorageItem item = await Folder.TryGetItemAsync(CoverFileName);

                if (item != null && item.IsOfType(StorageItemTypes.File))
                {
                    StorageFile cover_file = (StorageFile)item;
                    ImageFiles.Clear();
                    ImageFiles.Add(cover_file);
                    return new TaskResult();
                }
            }

            Utils.Debug.Log("Retrieving images for '" + Folder.Path + "'.");

            // Load all images.
            QueryOptions query_options = new QueryOptions
            {
                FolderDepth = FolderDepth.Shallow,
                IndexerOption = IndexerOption.DoNotUseIndexer, // UseIndexerWhenAvailable may fail.
            };

            foreach (string type in Utils.AppInfoProvider.SupportedImageTypes)
            {
                query_options.FileTypeFilter.Add(type);
            }

            var query = Folder.CreateFileQueryWithOptions(query_options);
            var img_files = await query.GetFilesAsync();
            Utils.Debug.Log("Adding " + img_files.Count.ToString() + " images.");

            if (img_files.Count == 0)
            {
                return new TaskResult(TaskException.EmptySet);
            }

            // Sort by display name.
            ImageFiles = img_files.OrderBy(x => x.DisplayName,
                new Utils.StringUtils.FileNameComparer()).ToList();

            CoverFileName = ImageFiles[0].Name;
            await SaveBasic(db);
            return new TaskResult();
        }
    }
}
