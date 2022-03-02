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
        private string CoverFileName
        {
            get => ExtendedString1;
            set => ExtendedString1 = value;
        }

        private StorageFile Archive;
        private List<string> Entries = new List<string>();
        private bool ImageUpdated = false;

        private StorageFolder CacheFolder => ApplicationData.Current.LocalCacheFolder;
        public override int ImageCount => Entries.Count;
        public override bool IsEditable => true;

        private ComicArchiveData(bool is_external) :
            base(ComicType.Zip, is_external) { }

        public static ComicData FromDatabase(string location)
        {
            return new ComicArchiveData(false)
            {
                Location = location,
            };
        }

        public static ComicData FromExternal(StorageFile file)
        {
            ComicArchiveData comic = new ComicArchiveData(true)
            {
                Location = file.Path,
                Archive = file,
            };

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
            string sub_path = GetSubPathFromFilename(Manager.ComicInfoFileName);

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

        public override async RawTask UpdateImages(LockContext db, bool cover, bool reload)
        {
            if (reload)
            {
                ImageUpdated = false;
            }

            if (!ImageUpdated)
            {
                TaskResult r = await SetArchive();

                if (!r.Successful)
                {
                    return r;
                }

                if (cover && CoverFileName.Length > 0)
                {
                    // Load the cover only.
                    IStorageItem item = await CacheFolder.TryGetItemAsync(CoverFileName);

                    if (item is StorageFile)
                    {
                        return new TaskResult();
                    }
                }

                Log("Retrieving images in '" + Location + "'");

                // Load entries.
                string sub_path = Utils.ArchiveAccess.GetSubPath(Location);
                List<string> subfiles = new List<string>();
                r = await Utils.ArchiveAccess.TryGetSubFiles(Archive, sub_path, subfiles);

                if (!r.Successful)
                {
                    return r;
                }

                List<string> entries = new List<string>();

                foreach (string subfile in subfiles)
                {
                    string extension = Utils.StringUtils.ExtensionFromFilename(subfile);

                    if (!Utils.AppInfoProvider.IsSupportedImageExtension(extension))
                    {
                        continue;
                    }

                    entries.Add(subfile);
                }
                
                Entries = entries.OrderBy(x => x, new Utils.StringUtils.FileNameComparer()).ToList();
                ImageUpdated = true;
            }

            if (Entries.Count == 0)
            {
                return new TaskResult(TaskException.EmptySet);
            }

            return new TaskResult();
        }

        private async Task<StorageFile> TryGetCover()
        {
            if (CoverFileName.Length == 0)
            {
                return null;
            }

            IStorageItem item = await CacheFolder.TryGetItemAsync(CoverFileName);

            if (!(item is StorageFile))
            {
                return null;
            }
            
            StorageFile cover_file = (StorageFile)item;

            if (!Utils.AppInfoProvider.IsSupportedImageExtension(cover_file.FileType))
            {
                return null;
            }

            return cover_file;
        }

        private async RawTask CreateCoverCache(LockContext db, IRandomAccessStream stream, string extension)
        {
            StorageFile cache_file;
            string cover_file_name = Utils.StringUtils.RandomFileName(16) + extension;

            try
            {
                cache_file = await CacheFolder.CreateFileAsync(
                    cover_file_name, CreationCollisionOption.ReplaceExisting);
            }
            catch (Exception e)
            {
                Log("Failed to create cache. " + e.ToString());
                return new TaskResult(TaskException.Failure);
            }

            IRandomAccessStream cache_stream;

            try
            {
                cache_stream = await cache_file.OpenAsync(FileAccessMode.ReadWrite);
            }
            catch (Exception e)
            {
                Log("Failed to access cache. " + e.ToString());
                await cache_file.DeleteAsync();
                return new TaskResult(TaskException.Failure);
            }

            await RandomAccessStream.CopyAndCloseAsync(stream, cache_stream);
            CoverFileName = cover_file_name;
            await SaveExtendedString1(db);
            return new TaskResult();
        }

        public override async Task<IRandomAccessStream> GetImageStream(LockContext db, int index)
        {
            if (index == 0)
            {
                StorageFile cover_file = await TryGetCover();

                if (cover_file != null)
                {
                    try
                    {
                        return await cover_file.OpenAsync(FileAccessMode.Read);
                    }
                    catch (Exception e)
                    {
                        Log("Failed to access cover cache '" + cover_file.Path +
                            "', load from archive instead. " + e.ToString());
                    }
                }
            }

            if (index >= Entries.Count)
            {
                Log("Image index " + index.ToString() + " out of boundary " + Entries.Count.ToString());
                return null;
            }

            string sub_path = GetSubPathFromFilename(Entries[index]);
            Stream stream = await Utils.ArchiveAccess.TryGetFileStream(Archive, sub_path);

            if (stream == null)
            {
                Log("Failed to access entry '" + Entries[index] + "'");
                return null;
            }

            IRandomAccessStream win_stream = stream.AsRandomAccessStream();

            if (index == 0)
            {
                await CreateCoverCache(db, win_stream, Utils.StringUtils.ExtensionFromFilename(sub_path));
            }

            return win_stream;
        }
    }
}
