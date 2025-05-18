// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.DebugTools;

using Windows.Storage;
using Windows.Storage.Streams;

namespace ComicReader.Data.Models.Comic;

internal class ComicArchiveData : ComicData
{
    private const string TAG = "ComicArchiveData";

    private StorageFile Archive;
    private List<string> Entries = new();

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
        var comic = new ComicArchiveData(true)
        {
            Title1 = archive.DisplayName,
            Archive = archive,
            Location = archive.Path,
        };

        _ = await comic.LoadFromInfoFile();
        _ = await comic.ReloadImageFiles();
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

        string base_path = ArchiveAccess.GetBasePath(Location, false);
        StorageFile file = await Storage.TryGetFile(base_path);

        if (file == null)
        {
            return TaskException.NoPermission;
        }

        Archive = file;
        return TaskException.Success;
    }

    private string GetSubPathFromFilename(string filename)
    {
        Logger.Assert(!IsExternal, "E811B52BC50C1652");
        string sub_path = ArchiveAccess.GetSubPath(Location, false);

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
            var ctx = new SearchContext(Location, PathType.File);
            string base_path = ArchiveAccess.GetBasePath(Location, false) + ArchiveAccess.FileSeperator;

            while (await ctx.Search(512))
            {
                foreach (string filepath in ctx.Files)
                {
                    if (filepath.Length <= base_path.Length)
                    {
                        Logger.AssertNotReachHere("F280B20B790F9D49");
                        continue;
                    }

                    string filename = StringUtils.ItemNameFromPath(filepath);

                    if (filename.Equals(COMIC_INFO_FILE_NAME))
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
            sub_path = GetSubPathFromFilename(COMIC_INFO_FILE_NAME);
        }

        using (Stream stream = await ArchiveAccess.TryGetFileStream(Archive, sub_path))
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
            var ctx = new SearchContext(Location, PathType.File);
            string base_path = ArchiveAccess.GetBasePath(Location, false) + ArchiveAccess.FileSeperator;

            while (await ctx.Search(512))
            {
                foreach (string filepath in ctx.Files)
                {
                    if (filepath.Length <= base_path.Length)
                    {
                        Logger.AssertNotReachHere("46158BE005A1988A");
                        continue;
                    }

                    string filename = StringUtils.ItemNameFromPath(filepath);
                    string extension = StringUtils.ExtensionFromFilename(filename);

                    if (AppInfoProvider.IsSupportedImageExtension(extension))
                    {
                        entries.Add(filepath.Substring(base_path.Length));
                    }
                }
            }
        }
        else
        {
            string sub_path = ArchiveAccess.GetSubPath(Location, false);
            var subfiles = new List<string>();

            result = await ArchiveAccess.TryGetSubFiles(Archive, sub_path, subfiles);
            if (!result.Successful())
            {
                return result;
            }

            foreach (string subfile in subfiles)
            {
                string extension = StringUtils.ExtensionFromFilename(subfile);

                if (!AppInfoProvider.IsSupportedImageExtension(extension))
                {
                    continue;
                }

                entries.Add(subfile);
            }
        }

        Entries = entries
            .OrderBy(x => StringUtils.SmartFileNameKeySelector(x), StringUtils.SmartFileNameComparer)
            .ToList();
        return TaskException.Success;
    }

    public override string GetImageCacheKey(int index)
    {
        if (index >= Entries.Count)
        {
            Logger.F(TAG, "InternalGetImageStream");
            return null;
        }

        string subPath = IsExternal ? Entries[index] : GetSubPathFromFilename(Entries[index]);
        return Archive.Path + ArchiveAccess.FileSeperator + subPath;
    }

    public override int GetImageSignature(int index)
    {
        return FileUtils.GetFileHashCode(Archive);
    }

    public override async Task<IComicConnection> OpenComicAsync()
    {
        await LoadImageFiles();
        List<string> entries = IsExternal ? Entries : Entries.Select(GetSubPathFromFilename).ToList();
        return new ArchiveComicConnection(Archive, entries);
    }

    private class ArchiveComicConnection : IComicConnection
    {
        private readonly StorageFile _archiveFile;
        private readonly List<string> _entries;

        public ArchiveComicConnection(StorageFile archiveFile, List<string> entries)
        {
            _archiveFile = archiveFile;
            _entries = entries;
        }

        public void Dispose()
        {
        }

        public int GetImageCount()
        {
            return _entries.Count;
        }

        public async Task<IRandomAccessStream> GetImageStream(int index)
        {
            if (index < 0 || index >= _entries.Count)
            {
                Logger.F(TAG, "InternalGetImageStream");
                return null;
            }

            string sub_path = _entries[index];
            Stream stream = await ArchiveAccess.TryGetFileStream(_archiveFile, sub_path);

            if (stream == null)
            {
                Log("Failed to access entry '" + _entries[index] + "'");
                return null;
            }

            IRandomAccessStream win_stream = stream.AsRandomAccessStream();
            return win_stream;
        }
    }
}
