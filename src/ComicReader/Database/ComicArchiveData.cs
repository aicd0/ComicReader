using ComicReader.Utils;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ComicReader.Database;

internal class ComicArchiveData : ComicData
{
    private StorageFile Archive;
    private List<string> Entries = new();

    public override int ImageCount => Entries.Count;
    public override bool IsEditable => !IsExternal;

    private ComicArchiveData(bool is_external) :
        base(ComicType.Archive, is_external)
    { }

    public static ComicData FromDatabase(string location)
    {
        return new ComicArchiveData(false)
        {
            Location = location,
        };
    }

    public static async Task<ComicData> FromExternal(StorageFile archive)
    {
        Utils.Storage.AddTrustedFile(archive);

        var comic = new ComicArchiveData(true)
        {
            Title1 = archive.DisplayName,
            Archive = archive,
            Location = archive.Path,
        };

        _ = await comic.LoadFromInfoFile();
        _ = await comic.UpdateImages(cover_only: false, reload: true);
        return comic;
    }

    private async Task<TaskException> SetArchive()
    {
        if (Archive != null)
        {
            return TaskException.Success;
        }

        if (Location == null)
        {
            return TaskException.InvalidParameters;
        }

        string base_path = Utils.ArchiveAccess.GetBasePath(Location);
        StorageFile file = await Utils.Storage.TryGetFile(base_path);

        if (file == null)
        {
            return TaskException.NoPermission;
        }

        Archive = file;
        return TaskException.Success;
    }

    private string GetSubPathFromFilename(string filename)
    {
        System.Diagnostics.Debug.Assert(!IsExternal);
        string sub_path = Utils.ArchiveAccess.GetSubPath(Location);

        if (sub_path.Length == 0)
        {
            return filename;
        }
        else
        {
            return sub_path + "\\" + filename;
        }
    }

    public override async Task<TaskException> LoadFromInfoFile()
    {
        TaskException r = await SetArchive();
        if (!r.Successful())
        {
            return r;
        }

        string info_text;
        string sub_path = null;

        if (IsExternal)
        {
            var ctx = new Utils.StorageItemSearchEngine.SearchContext(Location, Utils.StorageItemSearchEngine.PathType.File);
            string base_path = Utils.ArchiveAccess.GetBasePath(Location) + Utils.ArchiveAccess.FileSeperator;

            while (await ctx.Search(512))
            {
                foreach (string filepath in ctx.Files)
                {
                    if (filepath.Length <= base_path.Length)
                    {
                        System.Diagnostics.Debug.Assert(false);
                        continue;
                    }

                    string filename = Utils.StringUtils.ItemNameFromPath(filepath);

                    if (filename.Equals(ComicInfoFileName))
                    {
                        sub_path = filepath.Substring(base_path.Length);
                        break;
                    }
                }

                if (sub_path != null)
                {
                    break;
                }
            }

            if (sub_path == null)
            {
                return TaskException.FileNotFound;
            }
        }
        else
        {
            sub_path = GetSubPathFromFilename(ComicInfoFileName);
        }

        using (Stream stream = await Utils.ArchiveAccess.TryGetFileStream(Archive, sub_path))
        {
            if (stream == null)
            {
                return TaskException.Failure;
            }

            using (var reader = new StreamReader(stream))
            {
                info_text = await reader.ReadToEndAsync();
            }
        }

        ParseInfo(info_text);
        return TaskException.Success;
    }

    protected override Task<TaskException> SaveToInfoFile()
    {
        return Task.FromResult(TaskException.NotSupported);
    }

    protected override async Task<TaskException> ReloadImages()
    {
        TaskException result = await SetArchive();
        if (!result.Successful())
        {
            return result;
        }

        // Load entries.
        Log("Retrieving images in '" + Location + "'");
        var entries = new List<string>();

        if (IsExternal)
        {
            var ctx = new Utils.StorageItemSearchEngine.SearchContext(Location, Utils.StorageItemSearchEngine.PathType.File);
            string base_path = Utils.ArchiveAccess.GetBasePath(Location) + Utils.ArchiveAccess.FileSeperator;

            while (await ctx.Search(512))
            {
                foreach (string filepath in ctx.Files)
                {
                    if (filepath.Length <= base_path.Length)
                    {
                        System.Diagnostics.Debug.Assert(false);
                        continue;
                    }

                    string filename = Utils.StringUtils.ItemNameFromPath(filepath);
                    string extension = Utils.StringUtils.ExtensionFromFilename(filename);

                    if (Common.AppInfoProvider.IsSupportedImageExtension(extension))
                    {
                        entries.Add(filepath.Substring(base_path.Length));
                    }
                }
            }
        }
        else
        {
            string sub_path = Utils.ArchiveAccess.GetSubPath(Location);
            var subfiles = new List<string>();

            result = await Utils.ArchiveAccess.TryGetSubFiles(Archive, sub_path, subfiles);
            if (!result.Successful())
            {
                return result;
            }

            foreach (string subfile in subfiles)
            {
                string extension = Utils.StringUtils.ExtensionFromFilename(subfile);

                if (!Common.AppInfoProvider.IsSupportedImageExtension(extension))
                {
                    continue;
                }

                entries.Add(subfile);
            }
        }

        Entries = entries
            .OrderBy(x => Utils.StringUtils.SmartFileNameKeySelector(x), Utils.StringUtils.SmartFileNameComparer)
            .ToList();
        return TaskException.Success;
    }

    protected override async Task<IRandomAccessStream> InternalGetImageStream(int index)
    {
        if (index >= Entries.Count)
        {
            Log("Image index " + index.ToString() + " out of boundary " + Entries.Count.ToString());
            return null;
        }

        string sub_path = IsExternal ? Entries[index] : GetSubPathFromFilename(Entries[index]);
        Stream stream = await Utils.ArchiveAccess.TryGetFileStream(Archive, sub_path);

        if (stream == null)
        {
            Log("Failed to access entry '" + Entries[index] + "'");
            return null;
        }

        IRandomAccessStream win_stream = stream.AsRandomAccessStream();
        return win_stream;
    }
}
