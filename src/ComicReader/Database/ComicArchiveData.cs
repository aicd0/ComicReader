using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ComicReader.Database
{
    using RawTask = Task<Utils.TaskResult>;
    using SealedTask = Func<Task<Utils.TaskResult>, Utils.TaskResult>;
    using TaskResult = Utils.TaskResult;
    using TaskException = Utils.TaskException;

    public class ComicArchiveData : ComicData
    {
        private StorageFile Archive;
        private List<string> Entries = new List<string>();

        public override int ImageCount => Entries.Count;
        public override bool IsEditable => !IsExternal;

        private ComicArchiveData(bool is_external) :
            base(ComicType.Archive, is_external) { }

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

            ComicArchiveData comic = new ComicArchiveData(true)
            {
                Title1 = archive.DisplayName,
                Archive = archive,
                Location = archive.Path,
            };

            _ = await comic.LoadFromInfoFile();
            _ = await comic.UpdateImages(cover_only: false, reload: true);
            return comic;
        }

        private async RawTask SetArchive()
        {
            if (Archive != null)
            {
                return new TaskResult();
            }

            if (Location == null)
            {
                return new TaskResult(TaskException.InvalidParameters);
            }

            string base_path = Utils.ArchiveAccess.GetBasePath(Location);
            StorageFile file = await Utils.Storage.TryGetFile(base_path);

            if (file == null)
            {
                return new TaskResult(TaskException.NoPermission);
            }

            Archive = file;
            return new TaskResult();
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

        public override async RawTask LoadFromInfoFile()
        {
            TaskResult r = await SetArchive();

            if (!r.Successful)
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
                        
                        if (filename.Equals(Manager.ComicInfoFileName))
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
                    return new TaskResult(TaskException.FileNotFound);
                }
            }
            else
            {
                sub_path = GetSubPathFromFilename(Manager.ComicInfoFileName);
            }

            using (Stream stream = await Utils.ArchiveAccess.TryGetFileStream(Archive, sub_path))
            {
                if (stream == null)
                {
                    return new TaskResult(TaskException.Failure);
                }

                using (StreamReader reader = new StreamReader(stream))
                {
                    info_text = await reader.ReadToEndAsync();
                }
            }

            ParseInfo(info_text);
            return new TaskResult();
        }

        protected override RawTask SaveToInfoFile()
        {
            return Task.FromResult(new TaskResult(TaskException.NotSupported));
        }

        protected override async RawTask ReloadImages()
        {
            TaskResult result = await SetArchive();
            if (!result.Successful)
            {
                return result;
            }

            // Load entries.
            Log("Retrieving images in '" + Location + "'");
            List<string> entries = new List<string>();

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
                List<string> subfiles = new List<string>();

                result = await Utils.ArchiveAccess.TryGetSubFiles(Archive, sub_path, subfiles);
                if (!result.Successful)
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

            Entries = entries.OrderBy(x => x, new Utils.StringUtils.FileNameComparer()).ToList();
            return new TaskResult();
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
}
