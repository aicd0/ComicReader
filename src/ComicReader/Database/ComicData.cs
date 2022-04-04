using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
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

    public enum ComicType : int
    {
        Folder = 1,
        Archive = 2,
        PDF = 3,
    }

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
            public const string CoverFileCache = "cover_file_name";

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
        private SqlKey KeyCoverFileCache => new SqlKey(Field.CoverFileCache, CoverFileCache);

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
            KeyCoverFileCache,
        };

        // Local fields.
        public long Id { get; private set; }
        private ComicType Type { get; set; }
        public string Location { get; protected set; } = "";
        public string Title1 { get; protected set; } = "";
        public string Title2 { get; protected set; } = "";
        public bool Hidden { get; protected set; } = false;
        public int Rating { get; protected set; } = -1;
        public int Progress { get; protected set; } = -1;
        public DateTimeOffset LastVisit { get; protected set; } = DateTimeOffset.MinValue;
        public double LastPosition { get; protected set; } = 0.0;
        public List<double> ImageAspectRatios { get; set; } = new List<double>();
        protected string CoverFileCache { get; set; } = "";

        // Foriegn fields.
        public List<TagData> Tags = new List<TagData>();

        // Not in database.
        public string Title
        {
            get
            {
                if (Title1.Length == 0)
                {
                    if (Title2.Length == 0)
                    {
                        return Utils.StringResourceProvider.GetResourceString("UntitledCollection");
                    }
                    else
                    {
                        return Title2;
                    }
                }
                else if (Title2.Length == 0)
                {
                    return Title1;
                }
                else
                {
                    return Title1 + " - " + Title2;
                }
            }
        }

        public bool IsExternal { get; private set; }
        public abstract bool IsEditable { get; }
        public abstract int ImageCount { get; }
        protected StorageFolder CacheFolder => ApplicationData.Current.LocalCacheFolder;

        private bool ImageUpdated = false;
        private static Utils.TaskQueue SaveQueue = Utils.TaskQueueManager.EmptyQueue();

        protected ComicData(ComicType type, bool is_external)
        {
            Id = -1;
            Type = type;
            IsExternal = is_external;
        }

        private void From(long id, string title1, string title2, bool hidden,
            int rating, int progress, DateTimeOffset last_visit, double last_position,
            List<double> image_aspect_ratios, string cover_file_cache, List<TagData> tags)
        {
            Id = id;
            Title1 = title1;
            Title2 = title2;
            Hidden = hidden;
            Rating = rating;
            Progress = progress;
            LastVisit = last_visit;
            LastPosition = last_position;
            ImageAspectRatios = image_aspect_ratios;
            CoverFileCache = cover_file_cache;
            Tags = tags;
        }

        // Saving.
        private async Task InternalSaveTagsNoLock(LockContext db, bool remove_old = true)
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

                long rowid = await SqliteDatabaseManager.Insert(SqliteDatabaseManager.TagCategoryTable, tag_category_fields);

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

                    _ = await SqliteDatabaseManager.Insert(SqliteDatabaseManager.TagTable, tag_fields);
                }
            }
        }

        private async Task InternalInsertNoLock(LockContext db)
        {
            // Insert to comic table.
            long rowid = await SqliteDatabaseManager.Insert(SqliteDatabaseManager.ComicTable, AllFields);

            // Retrieve ID from inserted row.
            SqliteCommand command = SqliteDatabaseManager.NewCommand();
            command.CommandText = "SELECT " + Field.Id + " FROM " +
                SqliteDatabaseManager.ComicTable + " WHERE ROWID=$rowid";
            command.Parameters.AddWithValue("$rowid", rowid);
            Id = (long)command.ExecuteScalar();

            // Cleanups.
            command.Dispose();

            // Insert tags.
            await InternalSaveTagsNoLock(db, remove_old: false);
        }

        private SealedTask SaveSealed(List<SqlKey> keys, bool save_tags) =>
            (RawTask _) => SaveUnsealed(new LockContext(), keys, save_tags).Result;

        private async RawTask SaveUnsealed(LockContext db, List<SqlKey> keys, bool save_tags)
        {
            if (IsExternal)
            {
                return new TaskResult();
            }

            await ComicData.Manager.WaitLock(db); // Lock on.
            try
            {
                if (Id < 0)
                {
                    await InternalInsertNoLock(db);
                    return new TaskResult();
                }

                if (keys.Count > 0)
                {
                    SqlKey id = new SqlKey(Field.Id, Id);
                    await SqliteDatabaseManager.Update(SqliteDatabaseManager.ComicTable, id, keys);
                }

                if (save_tags)
                {
                    await InternalSaveTagsNoLock(db);
                }

                return new TaskResult();
            }
            finally
            {
                ComicData.Manager.ReleaseLock(db); // Lock off.
            }
        }

        public async Task SaveAllAsync(LockContext db)
        {
            await SaveUnsealed(db, AllFields, save_tags: true);
        }

        public void SaveBasic()
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
                KeyCoverFileCache,
            };

            Utils.TaskQueueManager.AppendTask(SaveSealed(fields, save_tags: false), "", SaveQueue);
        }

        public async Task SaveHiddenAsync(LockContext db, bool hidden)
        {
            Hidden = hidden;

            List<SqlKey> fields = new List<SqlKey>
            {
                KeyHidden,
            };

            await SaveUnsealed(db, fields, save_tags: false);
        }

        public void SaveRating(int rating)
        {
            Rating = rating;

            List<SqlKey> fields = new List<SqlKey>
            {
                KeyRating,
            };

            Utils.TaskQueueManager.AppendTask(SaveSealed(fields, save_tags: false), "", SaveQueue);
        }

        public void SaveProgress(int progress, double last_position)
        {
            Progress = progress;
            LastPosition = last_position;

            List<SqlKey> fields = new List<SqlKey>
            {
                KeyProgress,
                KeyLastPosition,
            };

            Utils.TaskQueueManager.AppendTask(SaveSealed(fields, save_tags: false), "", SaveQueue);
        }

        public void SetAsRead()
        {
            LastVisit = DateTimeOffset.Now;
            Progress = Math.Max(Progress, 0);

            List<SqlKey> fields = new List<SqlKey>
            {
                KeyProgress,
                KeyLastVisit,
            };

            Utils.TaskQueueManager.AppendTask(SaveSealed(fields, save_tags: false), "", SaveQueue);
        }

        public void SaveImageAspectRatios()
        {
            List<SqlKey> fields = new List<SqlKey>
            {
                KeyImageAspectRatios,
            };

            Utils.TaskQueueManager.AppendTask(SaveSealed(fields, save_tags: false), "", SaveQueue);
        }

        public void SaveCoverFileCache()
        {
            List<SqlKey> fields = new List<SqlKey>
            {
                KeyCoverFileCache,
            };

            Utils.TaskQueueManager.AppendTask(SaveSealed(fields, save_tags: false), "", SaveQueue);
        }

        public void SaveTags()
        {
            List<SqlKey> fields = new List<SqlKey>();
            Utils.TaskQueueManager.AppendTask(SaveSealed(fields, save_tags: true), "", SaveQueue);
        }

        // Info
        public void SetAsDefaultInfo()
        {
            Title1 = "";
            Title2 = "";
            Tags.Clear();

            List<string> sub_paths = Location.Split(Utils.ArchiveAccess.FileSeperator).ToList();
            List<string> tags = new List<string>();

            foreach (string path in sub_paths)
            {
                List<string> sub_tags = path.Split('\\').ToList();

                if (sub_tags.Count == 0)
                {
                    continue;
                }

                if (Type != ComicType.Folder)
                {
                    sub_tags[sub_tags.Count - 1] = Utils.StringUtils.DisplayNameFromFilename(sub_tags[sub_tags.Count - 1]);
                }

                tags.AddRange(sub_tags);
            }

            if (tags.Count <= 1)
            {
                return;
            }

            Title1 = tags[tags.Count - 1];

            TagData default_tag = new TagData
            {
                Name = Manager.DefaultTagsString,
                Tags = tags.Skip(1).ToHashSet(),
            };

            Tags.Add(default_tag);
        }

        public SealedTask SaveToInfoFileSealed() =>
            (RawTask _) => SaveToInfoFile().Result;

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

        // Interface
        private static ComicData FromDatabase(ComicType type, string location)
        {
            switch (type)
            {
                case ComicType.Folder:
                    return ComicFolderData.FromDatabase(location);
                case ComicType.Archive:
                    return ComicArchiveData.FromDatabase(location);
                case ComicType.PDF:
                    return ComicPdfData.FromDatabase(location);
                default:
                    System.Diagnostics.Debug.Assert(false);
                    return null;
            }
        }

        protected virtual async RawTask CreateCoverCache()
        {
            double req_width = 300.0;
            double req_height = 300.0;

            IRandomAccessStream stream = await InternalGetImageStream(0);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var resized_stream = new InMemoryRandomAccessStream();
            BitmapEncoder encoder = await BitmapEncoder.CreateForTranscodingAsync(resized_stream, decoder);

            double width_ratio = req_width / decoder.PixelWidth;
            double height_ratio = req_height / decoder.PixelHeight;
            double scale_ratio = Math.Min(width_ratio, height_ratio);
            if (req_width == 0) scale_ratio = height_ratio;
            if (req_height == 0) scale_ratio = width_ratio;
            scale_ratio = Math.Min(1.0, scale_ratio);
            uint aspect_height = (uint)Math.Floor(decoder.PixelHeight * scale_ratio);
            uint aspect_width = (uint)Math.Floor(decoder.PixelWidth * scale_ratio);

            encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
            encoder.BitmapTransform.ScaledHeight = aspect_height;
            encoder.BitmapTransform.ScaledWidth = aspect_width;

            await encoder.FlushAsync();
            resized_stream.Seek(0);
            var out_buffer = new byte[resized_stream.Size];
            await resized_stream.ReadAsync(out_buffer.AsBuffer(), (uint)resized_stream.Size, InputStreamOptions.None);

            string filename = Utils.StringUtils.RandomFileName(16) + ".jpg";

            StorageFile sample_file;
            try
            {
                sample_file = await CacheFolder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);
            }
            catch (Exception e)
            {
                Log("Failed to create cache. " + e.ToString());
                return new TaskResult(TaskException.Failure);
            }

            await FileIO.WriteBytesAsync(sample_file, out_buffer);
            CoverFileCache = filename;
            SaveCoverFileCache();
            return new TaskResult();
        }

        protected virtual async Task<StorageFile> GetCoverCache()
        {
            if (IsExternal) return null;
            if (CoverFileCache.Length == 0) return null;
            IStorageItem item = await CacheFolder.TryGetItemAsync(CoverFileCache);
            if (!(item is StorageFile)) return null;
            return (StorageFile)item;
        }

        public async RawTask UpdateImages(LockContext db, bool cover_only, bool reload)
        {
            if (reload) ImageUpdated = false;

            if (cover_only)
            {
                if (await GetCoverCache() != null)
                {
                    return new TaskResult();
                }
            }

            if (!ImageUpdated)
            {
                TaskResult r = await ReloadImages(db);
                if (!r.Successful) return r;
                ImageUpdated = true;
            }

            if (ImageCount == 0) return new TaskResult(TaskException.EmptySet);

            if (cover_only && !IsExternal)
            {
                await CreateCoverCache();
            }

            return new TaskResult();
        }

        protected abstract RawTask ReloadImages(LockContext db);

        public abstract RawTask LoadFromInfoFile();

        protected abstract RawTask SaveToInfoFile();

        public async Task<IRandomAccessStream> GetImageStream(LockContext db, int index)
        {
            if (index < 0)
            {
                StorageFile cover_file = await GetCoverCache();

                if (cover_file != null)
                {
                    try
                    {
                        return await cover_file.OpenAsync(FileAccessMode.Read);
                    }
                    catch (Exception e)
                    {
                        Log("Failed to access cover cache '" + cover_file.Path +
                            "', load from file instead. " + e.ToString());
                    }
                }
            }

            return await InternalGetImageStream(Math.Max(0, index));
        }

        protected abstract Task<IRandomAccessStream> InternalGetImageStream(int index);

        public class Manager
        {
            public const string ComicInfoFileName = "info.txt";

            // Subscriptions.
            public static Action<LockContext> OnUpdated;

            // Observers.
            public static bool IsRescanning { get; private set; } = false;

            // Resources.
            private static string m_DefaultTagsString = null;
            public static string DefaultTagsString
            {
                get
                {
                    if (m_DefaultTagsString == null)
                    {
                        Utils.C0.Sync(delegate
                        {
                            m_DefaultTagsString = Utils.StringResourceProvider.GetResourceString("DefaultTags");
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

            public static async Task<T> CommandBlock<T>(LockContext db, Func<SqliteCommand, Task<T>> op)
            {
                await WaitLock(db);
                try
                {
                    using (SqliteCommand command = SqliteDatabaseManager.NewCommand())
                    {
                        return await op(command);
                    }
                }
                finally
                {
                    ReleaseLock(db);
                }
            }

            public static async Task CommandBlock(LockContext db, Func<SqliteCommand, Task> op)
            {
                await WaitLock(db);
                try
                {
                    using (SqliteCommand command = SqliteDatabaseManager.NewCommand())
                    {
                        await op(command);
                    }
                }
                finally
                {
                    ReleaseLock(db);
                }
            }

            public static async Task TransactionBlock(LockContext db, Func<Task> op)
            {
                await WaitLock(db);
                try
                {
                    using (var transaction = SqliteDatabaseManager.NewTransaction())
                    {
                        await op();
                        transaction.Commit();
                    }
                }
                finally
                {
                    ReleaseLock(db);
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
                string extended_string_1 = query.GetString(11);

                // Tags
                List<TagData> tags = new List<TagData>();

                await CommandBlock(db, async delegate (SqliteCommand command)
                {
                    List<long> tag_category_ids = new List<long>();

                    command.CommandText = "SELECT * FROM " + SqliteDatabaseManager.TagCategoryTable +
                        " WHERE " + ComicData.Field.TagCategory.ComicId + "=" + id.ToString();

                    using (SqliteDataReader tag_category_query = await command.ExecuteReaderAsync())
                    {
                        while (tag_category_query.Read())
                        {
                            long tag_category_id = tag_category_query.GetInt64(0);
                            string name = tag_category_query.GetString(1);

                            TagData tag_data = new TagData
                            {
                                Name = name
                            };

                            tags.Add(tag_data);
                            tag_category_ids.Add(tag_category_id);
                        }
                    }

                    for (int i = 0; i < tags.Count; ++i)
                    {
                        command.CommandText = "SELECT * FROM " + SqliteDatabaseManager.TagTable +
                            " WHERE " + ComicData.Field.Tag.TagCategoryId + "=" + tag_category_ids[i].ToString();

                        using (SqliteDataReader tag_query = await command.ExecuteReaderAsync())
                        {
                            while (tag_query.Read())
                            {
                                string tag = tag_query.GetString(0);
                                _ = tags[i].Tags.Add(tag);
                            }
                        }
                    }
                });

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
                ComicData comic = FromDatabase(type, location);
                if (comic == null)
                {
                    return null;
                }

                comic.From(id, title1, title2, hidden, rating, progress,
                    last_visit, last_position, image_aspect_ratios,
                    extended_string_1, tags);

                // Post-procedures.
                if (reset_image_aspect_ratios)
                {
                    comic.SaveImageAspectRatios();
                }

                return comic;
            }

            private static async Task<ComicData> From(LockContext db, string col, object entry)
            {
                return await CommandBlock(db, async delegate (SqliteCommand command)
                {
                    command.CommandText = "SELECT * FROM " + SqliteDatabaseManager.ComicTable +
                        " WHERE " + col + "=@entry LIMIT 1";
                    command.Parameters.AddWithValue("@entry", entry);

                    SqliteDataReader query = command.ExecuteReader();

                    if (!query.Read())
                    {
                        return null;
                    }

                    return await From(db, query);
                });
            }

            public static async Task<ComicData> FromId(LockContext db, long id)
            {
                return await From(db, ComicData.Field.Id, id);
            }

            public static async Task<ComicData> FromLocation(LockContext db, string location)
            {
                return await From(db, ComicData.Field.Location, location);
            }

            public static async Task RemoveWithLocation(LockContext db, string location)
            {
                await CommandBlock(db, async delegate (SqliteCommand command)
                {
                    command.CommandText = "DELETE FROM " + SqliteDatabaseManager.ComicTable +
                        " WHERE " + ComicData.Field.Location + " LIKE @pattern";
                    command.Parameters.AddWithValue("@pattern", location + "%");
                    await command.ExecuteNonQueryAsync();
                });
            }

            public static SealedTask UpdateSealed(bool lazy_load) =>
                (RawTask _) => UpdateUnsealed(new LockContext(), lazy_load).Result;

            private struct UpdateItemInfo
            {
                public string Location;
                public ComicType ItemType;
                public bool IsExist;
            };

            private static async RawTask UpdateUnsealed(LockContext db, bool lazy_load)
            {
                await m_update_lock.WaitAsync();
                try
                {
                    Log("Updating comics" + (lazy_load ? " (lazy load)" : ""));

                    if (!lazy_load)
                    {
                        IsRescanning = true;
                        OnUpdated?.Invoke(db);
                    }

                    // Fetch all locations in the database.
                    List<string> loc_exist = new List<string>();

                    await CommandBlock(db, async delegate (SqliteCommand command)
                    {
                        command.CommandText = "SELECT " + ComicData.Field.Location +
                            " FROM " + SqliteDatabaseManager.ComicTable;
                        SqliteDataReader query = await command.ExecuteReaderAsync();

                        while (query.Read())
                        {
                            loc_exist.Add(query.GetString(0));
                        }
                    });

                    // Get all root folders from setting.
                    List<string> root_folders = new List<string>(XmlDatabase.Settings.ComicFolders.Count);

                    await XmlDatabaseManager.WaitLock();
                    foreach (string folder_path in XmlDatabase.Settings.ComicFolders)
                    {
                        root_folders.Add(folder_path);
                    }
                    XmlDatabaseManager.ReleaseLock();

                    // Get all subfolders in root folders.
                    var loc_in_lib = new List<string>();
                    var loc_ignore = new List<string>();
                    Utils.Stopwatch watch = new Utils.Stopwatch();
                    watch.Start();

                    foreach (string folder_path in root_folders)
                    {
                        Log("Scanning folder '" + folder_path + "'");
                        StorageFolder root_folder = await Utils.Storage.TryGetFolder(folder_path);

                        // Remove unreachable folders from database.
                        if (root_folder == null)
                        {
                            Log("Failed to reach folder '" + folder_path + "', skipped");
                            await XmlDatabaseManager.WaitLock();
                            XmlDatabase.Settings.ComicFolders.Remove(folder_path);
                            XmlDatabaseManager.ReleaseLock();
                            continue;
                        }

                        var ctx = new Utils.StorageItemSearchEngine.SearchContext(folder_path, Utils.StorageItemSearchEngine.PathType.Folder);

                        while (await ctx.Search(1024))
                        {
                            // Cancel this task if more requests have come in.
                            if (m_update_lock.CancellationRequested)
                            {
                                return new TaskResult(TaskException.Cancellation);
                            }

                            Log("Scanning " + ctx.ItemFound.ToString() + " items.");
                            var loc_scanned_dict = new Dictionary<string, ComicType>();

                            foreach (string file_path in ctx.Files)
                            {
                                string filename = Utils.StringUtils.ItemNameFromPath(file_path);
                                string extension = Utils.StringUtils.ExtensionFromFilename(filename).ToLower();
                                
                                if (Utils.AppInfoProvider.IsSupportedImageExtension(extension))
                                {
                                    string loc = Utils.StringUtils.ParentLocationFromLocation(file_path);

                                    if (!loc_scanned_dict.ContainsKey(loc))
                                    {
                                        loc_scanned_dict[loc] =
                                            file_path.Contains(Utils.ArchiveAccess.FileSeperator) ?
                                            ComicType.Archive : ComicType.Folder;
                                    }
                                }
                                else
                                {
                                    switch (extension)
                                    {
                                        case ".pdf":
                                            loc_scanned_dict[file_path] = ComicType.PDF;
                                            break;
                                        default:
                                            break;
                                    }
                                }
                            }

                            List<string> loc_scanned = new List<string>();

                            foreach (var item in loc_scanned_dict)
                            {
                                loc_scanned.Add(item.Key);
                            }

                            loc_in_lib.AddRange(loc_scanned);

                            foreach (string dir in ctx.NoAccessItems)
                            {
                                loc_ignore.Add(dir.ToLower());
                            }

                            // Generate a task queue for updating.
                            var queue = new List<UpdateItemInfo>();

                            // Get folders added.
                            List<string> loc_added = Utils.C3<string, string, string>.Except(
                                loc_scanned, loc_exist,
                                Utils.StringUtils.UniquePath, Utils.StringUtils.UniquePath,
                                new Utils.C1<string>.DefaultEqualityComparer()).ToList();

                            foreach (string loc in loc_added)
                            {
                                queue.Add(new UpdateItemInfo
                                {
                                    Location = loc,
                                    ItemType = loc_scanned_dict[loc],
                                    IsExist = false,
                                });
                            }

                            if (!lazy_load)
                            {
                                List<string> loc_kept = Utils.C3<string, string, string>.Intersect(
                                    loc_scanned, loc_exist,
                                    Utils.StringUtils.UniquePath, Utils.StringUtils.UniquePath,
                                    new Utils.C1<string>.DefaultEqualityComparer()).ToList();

                                foreach (string loc in loc_kept)
                                {
                                    queue.Add(new UpdateItemInfo
                                    {
                                        Location = loc,
                                        ItemType = loc_scanned_dict[loc],
                                        IsExist = true,
                                    });
                                }
                            }

                            await TransactionBlock(db, async delegate
                            {
                                foreach (UpdateItemInfo info in queue)
                                {
                                    await InternalUpdateComic(db, info.Location, info.ItemType, info.IsExist);
                                }
                            });

                            if (watch.LapSpan().TotalSeconds > 2)
                            {
                                OnUpdated?.Invoke(db);
                                watch.Lap();
                            }
                        }
                    }

                    // Get removed folders.
                    List<string> loc_removed = Utils.C3<string, string, string>.Except(loc_exist, loc_in_lib,
                        Utils.StringUtils.UniquePath, Utils.StringUtils.UniquePath,
                        new Utils.C1<string>.DefaultEqualityComparer()).ToList();

                    await TransactionBlock(db, async delegate
                    {
                        // Remove folders from database.
                        foreach (string loc in loc_removed)
                        {
                            // Skip directories in ignoring list.
                            string loc_lower = loc.ToLower();
                            bool ignore = false;

                            foreach (string base_loc in loc_ignore)
                            {
                                if (Utils.StringUtils.PathContain(base_loc, loc_lower))
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
                            Log("Removing item '" + loc + "'");
                            await RemoveWithLocation(db, loc);
                        }
                    });

                    return new TaskResult();
                }
                finally
                {
                    Log("Comic update completed");
                    IsRescanning = false;
                    OnUpdated?.Invoke(db);
                    m_update_lock.Release();
                }
            }

            protected static async Task InternalUpdateComic(LockContext db, string location, ComicType type, bool is_exist)
            {
                Log((is_exist ? "Updat" : "Add") + "ing comic '" + location + "'");

                // Update or create a new one.
                ComicData comic;

                if (is_exist)
                {
                    comic = await Manager.FromLocation(db, location);
                }
                else
                {
                    comic = FromDatabase(type, location);
                }

                if (comic == null)
                {
                    return;
                }

                // Load comic info locally.
                TaskResult r = await comic.LoadFromInfoFile();

                if (r.Successful)
                {
                    await comic.SaveAllAsync(db);
                }
                else if (!is_exist)
                {
                    comic.SetAsDefaultInfo();
                    await comic.SaveAllAsync(db);
                }
            }
        };

        protected static void Log(string content)
        {
            Utils.Debug.Log("ComicData: " + content);
        }
    };
}
