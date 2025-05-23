// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.DebugTools;
using ComicReader.Common.Threading;
using ComicReader.Data.SqlHelpers;

using LiteDB;

using Microsoft.Data.Sqlite;

using Windows.Storage;

namespace ComicReader.Data.Comic;

internal abstract class ComicData
{
    //
    // Constants
    //

    private const string TAG = "ComicData";
    public const string COMIC_INFO_FILE_NAME = "info.txt";

    //
    // Variables
    //

    private static string _defaultTagsString = null;
    private static string DefaultTagsString
    {
        get
        {
            _defaultTagsString ??= StringResourceProvider.GetResourceString("DefaultTags");
            return _defaultTagsString;
        }
    }

    private static readonly ITaskDispatcher _tableQueue = TaskDispatcher.Factory.NewQueue("ComicDataQueue");
    private static int _pendingUpdateTaskCount = 0;

    private bool _imageUpdated = false;

    //
    // Constructor
    //

    protected ComicData(ComicType type, bool is_external)
    {
        Id = -1;
        Type = type;
        IsExternal = is_external;
    }

    //
    // Public Interfaces
    //

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
    public string CoverCacheKey { get; private set; } = "";
    public string Description { get; private set; } = "";

    public List<TagData> Tags { get; private set; } = new List<TagData>();

    public string Title
    {
        get
        {
            if (Title1.Length == 0)
            {
                if (Title2.Length == 0)
                {
                    return StringResourceProvider.GetResourceString("UntitledCollection");
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
    public bool IsRead => Progress >= 100;
    public bool IsUnread => Progress < 0;
    public abstract bool IsEditable { get; }
    protected StorageFolder CacheFolder => ApplicationData.Current.LocalCacheFolder;

    private ComicType ValueType => Type;
    private string ValueLocation => Location;
    private string ValueTitle1 => Title1;
    private string ValueTitle2 => Title2;
    private bool ValueHidden => Hidden;
    private int ValueRating => Rating;
    private int ValueProgress => Progress;
    private DateTimeOffset ValueLastVisit => LastVisit;
    private double ValueLastPosition => LastPosition;
    private string ValueCoverCacheKey => CoverCacheKey;
    private string ValueDescription => Description;

    public static Action OnUpdated { get; set; }

    public static bool IsRescanning { get; private set; } = false;

    private void SaveAllNoLock()
    {
        SaveNoLock(delegate
        {
            new UpdateCommand<ComicTable>(ComicTable.Instance)
                .AppendColumn(ComicTable.ColumnType, ValueType)
                .AppendColumn(ComicTable.ColumnLocation, ValueLocation)
                .AppendColumn(ComicTable.ColumnTitle1, ValueTitle1)
                .AppendColumn(ComicTable.ColumnTitle2, ValueTitle2)
                .AppendColumn(ComicTable.ColumnHidden, ValueHidden)
                .AppendColumn(ComicTable.ColumnRating, ValueRating)
                .AppendColumn(ComicTable.ColumnProgress, ValueProgress)
                .AppendColumn(ComicTable.ColumnLastVisit, ValueLastVisit)
                .AppendColumn(ComicTable.ColumnLastPosition, ValueLastPosition)
                .AppendColumn(ComicTable.ColumnCoverCacheKey, ValueCoverCacheKey)
                .AppendColumn(ComicTable.ColumnDescription, ValueDescription)
                .AppendCondition(ComicTable.ColumnId, Id)
                .Execute();

            InternalSaveTagsNoLock();
        });
    }

    public void SaveBasic()
    {
        _ = Enqueue(delegate
        {
            return SaveNoLock(delegate
            {
                new UpdateCommand<ComicTable>(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnType, ValueType)
                    .AppendColumn(ComicTable.ColumnLocation, ValueLocation)
                    .AppendColumn(ComicTable.ColumnTitle1, ValueTitle1)
                    .AppendColumn(ComicTable.ColumnTitle2, ValueTitle2)
                    .AppendColumn(ComicTable.ColumnHidden, ValueHidden)
                    .AppendColumn(ComicTable.ColumnRating, ValueRating)
                    .AppendColumn(ComicTable.ColumnProgress, ValueProgress)
                    .AppendColumn(ComicTable.ColumnLastVisit, ValueLastVisit)
                    .AppendColumn(ComicTable.ColumnLastPosition, ValueLastPosition)
                    .AppendColumn(ComicTable.ColumnCoverCacheKey, ValueCoverCacheKey)
                    .AppendColumn(ComicTable.ColumnDescription, ValueDescription)
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();

                InternalSaveTagsNoLock();
            });
        }, "SaveBasic");
    }

    public async Task SaveHiddenAsync(bool hidden)
    {
        Hidden = hidden;

        await Enqueue(delegate
        {
            return SaveNoLock(delegate
            {
                new UpdateCommand<ComicTable>(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnHidden, ValueHidden)
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
        }, "SaveHiddenAsync");
    }

    public void SaveRating(int rating)
    {
        Rating = rating;

        _ = Enqueue(delegate
        {
            return SaveNoLock(delegate
            {
                new UpdateCommand<ComicTable>(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnRating, ValueRating)
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
        }, "SaveRating");
    }

    public async Task SaveProgressAsync(int progress, double last_position)
    {
        Progress = progress;
        LastPosition = last_position;

        await Enqueue(delegate
        {
            return SaveNoLock(delegate
            {
                new UpdateCommand<ComicTable>(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnProgress, ValueProgress)
                    .AppendColumn(ComicTable.ColumnLastPosition, ValueLastPosition)
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
        }, "SaveProgress");
    }

    public void SetAsRead()
    {
        _ = SaveProgressAsync(100, LastPosition);
    }

    public void SetAsUnread()
    {
        _ = SaveProgressAsync(-1, 0);
    }

    public void SetAsStarted()
    {
        LastVisit = DateTimeOffset.Now;
        Progress = Math.Max(Progress, 0);

        _ = Enqueue(delegate
        {
            return SaveNoLock(delegate
            {
                new UpdateCommand<ComicTable>(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnProgress, ValueProgress)
                    .AppendColumn(ComicTable.ColumnLastVisit, ValueLastVisit)
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
        }, "SetAsRead");
    }

    public void SetCoverCacheKey(string key)
    {
        CoverCacheKey = key;

        _ = Enqueue(delegate
        {
            return SaveNoLock(delegate
            {
                new UpdateCommand<ComicTable>(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnCoverCacheKey, ValueCoverCacheKey)
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
        }, "SetCoverCacheKey");
    }

    public void SetAsDefaultInfo()
    {
        Title1 = "";
        Title2 = "";
        Tags.Clear();

        var sub_paths = Location.Split(ArchiveAccess.FileSeperator).ToList();
        var tags = new List<string>();

        foreach (string path in sub_paths)
        {
            var sub_tags = path.Split('\\').ToList();

            if (sub_tags.Count == 0)
            {
                continue;
            }

            if (Type != ComicType.Folder)
            {
                sub_tags[sub_tags.Count - 1] = StringUtils.DisplayNameFromFilename(sub_tags[sub_tags.Count - 1]);
            }

            tags.AddRange(sub_tags);
        }

        if (tags.Count <= 1)
        {
            return;
        }

        Title1 = tags[tags.Count - 1];

        var default_tag = new TagData
        {
            Name = DefaultTagsString,
            Tags = tags.Skip(1).ToHashSet(),
        };

        Tags.Add(default_tag);
    }

    public Action SaveToInfoFileSealed() =>
        () => SaveToInfoFile().Wait();

    public string TagString()
    {
        string text = "";

        foreach (TagData tag in Tags)
        {
            text += tag.Name + ": " + StringUtils.Join("/", tag.Tags) + "\n";
        }

        return text;
    }

    public static string InfoString(string title1, string title2, string description, string tags)
    {
        string[] descriptions = description.Replace('\r', '\n').Split('\n', StringSplitOptions.RemoveEmptyEntries);

        StringBuilder sb = new();
        sb.Append("Title1: ");
        sb.Append(title1);
        sb.Append('\n');
        sb.Append("Title2: ");
        sb.Append(title2);
        sb.Append('\n');
        foreach (string desc in descriptions)
        {
            sb.Append("Description: ");
            sb.Append(desc);
            sb.Append('\n');
        }
        sb.Append(tags);
        return sb.ToString();
    }

    public void ParseInfo(string text)
    {
        Title1 = "";
        Title2 = "";
        Description = "";
        Tags.Clear();

        text = text.Replace('\r', '\n');
        string[] properties = text.Split("\n", StringSplitOptions.RemoveEmptyEntries);
        bool title1Set = false;
        bool title2Set = false;

        foreach (string property in properties)
        {
            ParsePropertyResult parseResult = ParseProperty(property);

            if (parseResult == null)
            {
                continue;
            }

            string name = parseResult.Name.ToLower();

            if (!title1Set && (name == "title1" || name == "title" || name == "t1" || name == "to"))
            {
                title1Set = true;
                Title1 = parseResult.Content;
            }
            else if (!title2Set && (name == "title2" || name == "t2"))
            {
                title2Set = true;
                Title2 = parseResult.Content;
            }
            else if (name == "description")
            {
                if (parseResult.Content.Length > 0)
                {
                    if (Description.Length > 0)
                    {
                        Description += '\n';
                    }
                    Description += parseResult.Content;
                }
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
                        t.Tags.UnionWith(parseResult.Tags);
                        break;
                    }
                }

                if (!duplicated)
                {
                    Tags.Add(new TagData
                    {
                        Name = parseResult.Name,
                        Tags = parseResult.Tags,
                    });
                }
            }
        }
    }

    public async Task<TaskException> LoadImageFiles()
    {
        if (_imageUpdated)
        {
            return TaskException.Success;
        }

        TaskException result = await ReloadImages();

        if (!result.Successful())
        {
            return result;
        }

        _imageUpdated = true;

        using IComicConnection connection = await OpenComicAsync();
        if (connection.GetImageCount() == 0)
        {
            return TaskException.EmptySet;
        }

        return TaskException.Success;
    }

    public async Task<TaskException> ReloadImageFiles()
    {
        _imageUpdated = false;
        return await LoadImageFiles();
    }

    public abstract Task<TaskException> LoadFromInfoFile();

    public static async Task<ComicData> FromId(long id, string taskName)
    {
        return await Enqueue(delegate
        {
            return FromIdNoLock(id);
        }, taskName);
    }

    public string GetCoverImageCacheKey()
    {
        string coverCacheKey = CoverCacheKey;
        if (coverCacheKey != null && coverCacheKey.Length > 0)
        {
            return coverCacheKey;
        }

        if (!LoadImageFiles().Result.Successful())
        {
            return null;
        }
        coverCacheKey = GetImageCacheKey(0);
        SetCoverCacheKey(coverCacheKey);
        return coverCacheKey;
    }

    public static async Task CommandBlock2(Func<SqliteCommand, Task> op, string taskName)
    {
        await Enqueue(delegate
        {
            CommandBlock2NoLock(op);
            return true;
        }, taskName);
    }

    public static async Task EnqueueCommand(Action op, string taskName)
    {
        await Enqueue(delegate
        {
            op();
            return true;
        }, taskName);
    }

    public static async Task<ComicData> FromLocation(string location, string taskName)
    {
        return await Enqueue(delegate
        {
            return FromLocationNoLock(location);
        }, taskName);
    }

    public static void UpdateAllComics(string reason, bool lazy)
    {
        int pendingCount = Interlocked.Increment(ref _pendingUpdateTaskCount);
        Log($"UpdateAllComics#Enqueue(pending={pendingCount},reason={reason},lazy={lazy})");

        TaskDispatcher.LongRunningThreadPool.Submit($"{TAG}#UpdateAllComics", delegate
        {
            int pendingCount = Interlocked.Decrement(ref _pendingUpdateTaskCount);
            if (pendingCount > 0)
            {
                Log($"UpdateAllComics#Skip(pending={pendingCount})");
                return;
            }

            long session = Random.Shared.NextInt64();
            Log($"UpdateAllComics#Start(session={session},lazy={lazy})");
            IsRescanning = true;
            TaskException result = UpdateAllComicsInternal(lazy).Result;
            IsRescanning = false;
            Log($"UpdateAllComics#End(result={result},session={session})");

            OnUpdated?.Invoke();
        });
    }

    //
    // Abstract Methods
    //

    public abstract string GetImageCacheKey(int index);

    public abstract int GetImageSignature(int index);

    public abstract Task<IComicConnection> OpenComicAsync();

    protected abstract Task<TaskException> ReloadImages();

    protected abstract Task<TaskException> SaveToInfoFile();

    //
    // Protected Methods
    //

    protected static void Log(string message)
    {
        Logger.I("ComicData", message);
    }

    //
    // Private Methods
    //

    private static ComicData FromIdNoLock(long id)
    {
        ComicType type;
        string location;
        string title1;
        string title2;
        bool hidden;
        int rating;
        int progress;
        DateTimeOffset lastVisit;
        double lastPosition;
        string coverCacheKey;
        string description;

        {
            SelectCommand<ComicTable> command = new SelectCommand<ComicTable>(ComicTable.Instance)
                .AppendCondition(ComicTable.ColumnId, id)
                .Limit(1);
            SelectCommand<ComicTable>.IToken<long> typeToken = command.PutQueryInt64(ComicTable.ColumnType);
            SelectCommand<ComicTable>.IToken<string> locationToken = command.PutQueryString(ComicTable.ColumnLocation);
            SelectCommand<ComicTable>.IToken<string> title1Token = command.PutQueryString(ComicTable.ColumnTitle1);
            SelectCommand<ComicTable>.IToken<string> title2Token = command.PutQueryString(ComicTable.ColumnTitle2);
            SelectCommand<ComicTable>.IToken<bool> hiddenToken = command.PutQueryBoolean(ComicTable.ColumnHidden);
            SelectCommand<ComicTable>.IToken<int> ratingToken = command.PutQueryInt32(ComicTable.ColumnRating);
            SelectCommand<ComicTable>.IToken<int> progressToken = command.PutQueryInt32(ComicTable.ColumnProgress);
            SelectCommand<ComicTable>.IToken<DateTimeOffset> lastVisitToken = command.PutQueryDateTimeOffset(ComicTable.ColumnLastVisit);
            SelectCommand<ComicTable>.IToken<double> lastPositionToken = command.PutQueryDouble(ComicTable.ColumnLastPosition);
            SelectCommand<ComicTable>.IToken<string> coverCacheKeyToken = command.PutQueryString(ComicTable.ColumnCoverCacheKey);
            SelectCommand<ComicTable>.IToken<string> descriptionToken = command.PutQueryString(ComicTable.ColumnDescription);
            using SelectCommand<ComicTable>.IReader reader = command.Execute();

            if (!reader.Read())
            {
                return null;
            }

            type = (ComicType)typeToken.GetValue();
            location = locationToken.GetValue();
            title1 = title1Token.GetValue();
            title2 = title2Token.GetValue();
            hidden = hiddenToken.GetValue();
            rating = ratingToken.GetValue();
            progress = progressToken.GetValue();
            lastVisit = lastVisitToken.GetValue();
            lastPosition = lastPositionToken.GetValue();
            coverCacheKey = coverCacheKeyToken.GetValue();
            description = descriptionToken.GetValue();
        }

        var tags = new List<TagData>();
        var tagCategoryIds = new List<long>();

        {
            SelectCommand<TagCategoryTable> command = new SelectCommand<TagCategoryTable>(TagCategoryTable.Instance)
                .AppendCondition(TagCategoryTable.ColumnComicId, id);
            SelectCommand<TagCategoryTable>.IToken<long> tagCategoryIdToken = command.PutQueryInt64(TagCategoryTable.ColumnId);
            SelectCommand<TagCategoryTable>.IToken<string> nameToken = command.PutQueryString(TagCategoryTable.ColumnName);
            using SelectCommand<TagCategoryTable>.IReader reader = command.Execute();

            while (reader.Read())
            {
                long tagCategoryId = tagCategoryIdToken.GetValue();
                string name = nameToken.GetValue();

                var tagData = new TagData
                {
                    Name = name
                };

                tags.Add(tagData);
                tagCategoryIds.Add(tagCategoryId);
            }
        }

        for (int i = 0; i < tags.Count; ++i)
        {
            SelectCommand<TagTable> command = new SelectCommand<TagTable>(TagTable.Instance)
                .AppendCondition(TagTable.ColumnTagCategoryId, tagCategoryIds[i]);
            SelectCommand<TagTable>.IToken<string> tagToken = command.PutQueryString(TagTable.ColumnContent);
            using SelectCommand<TagTable>.IReader reader = command.Execute();

            while (reader.Read())
            {
                string tag = tagToken.GetValue();
                _ = tags[i].Tags.Add(tag);
            }
        }

        ComicData comic = FromDatabase(type, location);
        if (comic == null)
        {
            return null;
        }

        comic.From(id, title1, title2, hidden, rating, progress,
            lastVisit, lastPosition, coverCacheKey, description, tags);
        return comic;
    }

    private static async Task UpdateComicNoLock(string location, ComicType type, bool is_exist)
    {
        Log((is_exist ? "Updat" : "Add") + "ing comic '" + location + "'");

        // Update or create a new one.
        ComicData comic;

        if (is_exist)
        {
            comic = FromLocationNoLock(location);
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
        TaskException r = await comic.LoadFromInfoFile();
        if (r.Successful())
        {
            comic.SaveAllNoLock();
        }
        else if (!is_exist)
        {
            comic.SetAsDefaultInfo();
            comic.SaveAllNoLock();
        }
    }

    private void From(long id, string title1, string title2, bool hidden,
        int rating, int progress, DateTimeOffset lastVisit, double lastPosition,
        string coverCacheKey, string description, List<TagData> tags)
    {
        Id = id;
        Title1 = title1;
        Title2 = title2;
        Hidden = hidden;
        Rating = rating;
        Progress = progress;
        LastVisit = lastVisit;
        LastPosition = lastPosition;
        CoverCacheKey = coverCacheKey;
        Description = description;
        Tags = tags;
    }

    private void InternalSaveTagsNoLock(bool removeOld = true)
    {
        if (removeOld)
        {
            new DeleteCommand<TagCategoryTable>(TagCategoryTable.Instance)
                .AppendCondition(TagCategoryTable.ColumnComicId, Id)
                .Execute();
        }

        foreach (TagData category in Tags)
        {
            long tagCategoryId = new InsertCommand<TagCategoryTable>(TagCategoryTable.Instance)
                .AppendColumn(TagCategoryTable.ColumnName, category.Name)
                .AppendColumn(TagCategoryTable.ColumnComicId, Id)
                .Execute();

            foreach (string tag in category.Tags)
            {
                new InsertCommand<TagTable>(TagTable.Instance)
                    .AppendColumn(TagTable.ColumnContent, tag)
                    .AppendColumn(TagTable.ColumnComicId, Id)
                    .AppendColumn(TagTable.ColumnTagCategoryId, tagCategoryId)
                    .Execute();
            }
        }
    }

    private void InternalInsertNoLock()
    {
        Id = new InsertCommand<ComicTable>(ComicTable.Instance)
            .AppendColumn(ComicTable.ColumnType, ValueType)
            .AppendColumn(ComicTable.ColumnLocation, ValueLocation)
            .AppendColumn(ComicTable.ColumnTitle1, ValueTitle1)
            .AppendColumn(ComicTable.ColumnTitle2, ValueTitle2)
            .AppendColumn(ComicTable.ColumnHidden, ValueHidden)
            .AppendColumn(ComicTable.ColumnRating, ValueRating)
            .AppendColumn(ComicTable.ColumnProgress, ValueProgress)
            .AppendColumn(ComicTable.ColumnLastVisit, ValueLastVisit)
            .AppendColumn(ComicTable.ColumnLastPosition, ValueLastPosition)
            .AppendColumn(ComicTable.ColumnCoverCacheKey, CoverCacheKey)
            .AppendColumn(ComicTable.ColumnDescription, Description)
            .Execute();

        InternalSaveTagsNoLock(removeOld: false);
    }

    private TaskException SaveNoLock(Action action)
    {
        if (IsExternal)
        {
            return TaskException.Success;
        }

        if (Id < 0)
        {
            InternalInsertNoLock();
            return TaskException.Success;
        }

        action();
        return TaskException.Success;
    }

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
                DebugUtils.Assert(false);
                return null;
        }
    }

    private static async Task<T> Enqueue<T>(Func<T> op, string taskName)
    {
        var taskResult = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _tableQueue.Submit($"{TAG}#Enqueue#{taskName}", delegate
        {
            taskResult.SetResult(op());
        });
        return await taskResult.Task;
    }

    private static void CommandBlock2NoLock(Func<SqliteCommand, Task> op)
    {
        using (SqliteCommand command = SqliteDatabaseManager.NewCommand())
        {
            op(command).Wait();
        }
    }

    private static async Task TransactionBlock(Func<Task> op, string taskName)
    {
        await Enqueue(delegate
        {
            using (SqliteTransaction transaction = SqliteDatabaseManager.NewTransaction())
            {
                op().Wait();
                transaction.Commit();
            }

            return true;
        }, taskName);
    }

    private static ComicData FromLocationNoLock(string location)
    {
        SelectCommand<ComicTable> command = new SelectCommand<ComicTable>(ComicTable.Instance)
            .AppendCondition(ComicTable.ColumnLocation, location)
            .Limit(1);
        SelectCommand<ComicTable>.IToken<long> comicIdToken = command.PutQueryInt64(ComicTable.ColumnId);
        using SelectCommand<ComicTable>.IReader reader = command.Execute();

        if (!reader.Read())
        {
            return null;
        }

        long comicId = comicIdToken.GetValue();
        return FromIdNoLock(comicId);
    }

    private static void RemoveWithLocationNoLock(string location)
    {
        CommandBlock2NoLock(async delegate (SqliteCommand command)
        {
            await new DeleteCommand<ComicTable>(ComicTable.Instance)
                .AppendCondition(new LikeCondition(ComicTable.ColumnLocation, location + "%"))
                .ExecuteAsync();
        });
    }

    private static async Task<TaskException> UpdateAllComicsInternal(bool lazy)
    {
        // Fetch all locations in the database.
        var locExist = new List<string>();

        await Enqueue(delegate
        {
            var command = new SelectCommand<ComicTable>(ComicTable.Instance);
            SelectCommand<ComicTable>.IToken<string> locationToken = command.PutQueryString(ComicTable.ColumnLocation);
            using SelectCommand<ComicTable>.IReader reader = command.Execute();

            while (reader.Read())
            {
                locExist.Add(locationToken.GetValue());
            }

            return true;
        }, "GetLocationsFromDatabase");

        // Get all root folders from setting.
        var root_folders = new List<string>(XmlDatabase.Settings.ComicFolders.Count);

        await XmlDatabaseManager.WaitLock();
        foreach (string folder_path in XmlDatabase.Settings.ComicFolders)
        {
            root_folders.Add(folder_path);
        }

        XmlDatabaseManager.ReleaseLock();

        // Get all subfolders in root folders.
        var loc_in_lib = new List<string>();
        var loc_ignore = new List<string>();
        var watch = new Stopwatch();
        watch.Start();

        foreach (string folderPath in root_folders)
        {
            Log("Scanning folder '" + folderPath + "'");

            // Remove unreachable folders from database.
            if (!Directory.Exists(folderPath))
            {
                Log("Failed to reach folder '" + folderPath + "', skipped");
                await XmlDatabaseManager.WaitLock();
                XmlDatabase.Settings.ComicFolders.Remove(folderPath);
                XmlDatabaseManager.ReleaseLock();
                continue;
            }

            var ctx = new SearchContext(folderPath, PathType.Folder);

            while (await ctx.Search(1024))
            {
                // Cancel this task if more requests have come in.
                if (_pendingUpdateTaskCount > 0)
                {
                    return TaskException.Cancellation;
                }

                Log("Scanning " + ctx.ItemFound.ToString() + " items.");
                var loc_scanned_dict = new Dictionary<string, ComicType>();

                foreach (string file_path in ctx.Files)
                {
                    string filename = StringUtils.ItemNameFromPath(file_path);
                    string extension = StringUtils.ExtensionFromFilename(filename).ToLower();

                    if (AppInfoProvider.IsSupportedImageExtension(extension))
                    {
                        string loc = StringUtils.ParentLocationFromLocation(file_path);

                        if (!loc_scanned_dict.ContainsKey(loc))
                        {
                            loc_scanned_dict[loc] =
                                ArchiveAccess.IsArchivePath(file_path) ?
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

                var loc_scanned = new List<string>();

                foreach (KeyValuePair<string, ComicType> item in loc_scanned_dict)
                {
                    loc_scanned.Add(item.Key);
                }

                loc_in_lib.AddRange(loc_scanned);

                foreach (string dir in ctx.NoAccessItems)
                {
                    loc_ignore.Add(dir);
                }

                // Generate a task queue for updating.
                var queue = new List<UpdateItemInfo>();

                // Get folders added.
                var loc_added = C3<string, string, string>.Except(
                    loc_scanned, locExist,
                    StringUtils.UniquePath, StringUtils.UniquePath,
                    new C1<string>.DefaultEqualityComparer()).ToList();

                foreach (string loc in loc_added)
                {
                    queue.Add(new UpdateItemInfo
                    {
                        Location = loc,
                        ItemType = loc_scanned_dict[loc],
                        IsExist = false,
                    });
                }

                if (!lazy)
                {
                    var loc_kept = C3<string, string, string>.Intersect(
                        loc_scanned, locExist,
                        StringUtils.UniquePath, StringUtils.UniquePath,
                        new C1<string>.DefaultEqualityComparer()).ToList();

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

                await TransactionBlock(async delegate
                {
                    foreach (UpdateItemInfo info in queue)
                    {
                        await UpdateComicNoLock(info.Location, info.ItemType, info.IsExist);
                    }
                }, "Update comic");

                if (watch.LapSpan().TotalSeconds > 2)
                {
                    OnUpdated?.Invoke();
                    watch.Lap();
                }
            }
        }

        // Get removed folders.
        var loc_removed = C3<string, string, string>.Except(locExist, loc_in_lib,
            StringUtils.UniquePath, StringUtils.UniquePath,
            new C1<string>.DefaultEqualityComparer()).ToList();

        await TransactionBlock(delegate
        {
            // Remove folders from database.
            foreach (string loc in loc_removed)
            {
                // Skip directories in ignoring list.
                bool ignore = false;

                foreach (string base_loc in loc_ignore)
                {
                    if (StringUtils.FolderContain(base_loc, loc))
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
                RemoveWithLocationNoLock(loc);
            }

            return Task.CompletedTask;
        }, "RemoveLocationsFromDatabase");

        return TaskException.Success;
    }

    private static ParsePropertyResult ParseProperty(string src)
    {
        string[] pieces = src.Split(":", 2, StringSplitOptions.RemoveEmptyEntries);

        if (pieces.Length != 2)
        {
            return null;
        }

        var result = new ParsePropertyResult
        {
            Name = pieces[0].Trim(),
            Content = pieces[1].Trim(),
        };

        var tags = new List<string>(pieces[1].Split("/", StringSplitOptions.RemoveEmptyEntries));

        foreach (string tag in tags)
        {
            string tagTrimed = tag.Trim();

            if (tagTrimed.Length != 0)
            {
                result.Tags.Add(tagTrimed);
            }
        }

        return result;
    }

    //
    // Classes
    //

    private struct UpdateItemInfo
    {
        public string Location;
        public ComicType ItemType;
        public bool IsExist;
    };

    internal class TagData
    {
        public string Name;
        public HashSet<string> Tags = new();
    };

    internal class ParsePropertyResult
    {
        public string Name;
        public string Content;
        public HashSet<string> Tags = new();
    };

    internal enum ComicType : int
    {
        Folder = 1,
        Archive = 2,
        PDF = 3,
    }
};
