using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    public class ComicFolderData : ComicData
    {
        private StorageFolder Folder = null;
        private StorageFile InfoFile = null;
        private List<StorageFile> ImageFiles = new List<StorageFile>();
        public override int ImageCount => ImageFiles.Count;
        public override bool IsEditable => !(IsExternal && InfoFile == null);

        private ComicFolderData(bool is_external) :
            base(ComicType.Folder, is_external)
        {
        }

        public static ComicData FromDatabase(string location)
        {
            return new ComicFolderData(false)
            {
                Location = location,
            };
        }

        public static async Task<ComicData> FromExternal(string directory, List<StorageFile> image_files, StorageFile info_file)
        {
            image_files.OrderBy(x => x.DisplayName,
                new Utils.StringUtils.FileNameComparer());

            ComicFolderData comic = new ComicFolderData(true)
            {
                Location = directory,
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

        protected override async RawTask SaveInfoFile()
        {
            TaskResult r = await CompleteInfoFile(true);

            if (!r.Successful)
            {
                return r;
            }

            string text = InfoString();
            IBuffer buffer = Utils.C0.GetBufferFromString(text);
            await FileIO.WriteBufferAsync(InfoFile, buffer);
            return new TaskResult();
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

            Log("Retrieving images for '" + Folder.Path + "'.");

            // Load all images.
            QueryOptions query_options = new QueryOptions
            {
                FolderDepth = FolderDepth.Shallow,
                IndexerOption = IndexerOption.DoNotUseIndexer, // UseIndexerWhenAvailable may fail.
            };

            foreach (string type in Utils.AppInfoProvider.SupportedImageExtensions)
            {
                query_options.FileTypeFilter.Add(type);
            }

            var query = Folder.CreateFileQueryWithOptions(query_options);
            var img_files = await query.GetFilesAsync();
            Log("Adding " + img_files.Count.ToString() + " images.");

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

        public static async Task Update(LockContext db, string location, bool is_exist)
        {
            Log((is_exist ? "Updat" : "Add") + "ing folder '" + location + "'");
            List<string> filenames = Utils.Win32IO.SubFiles(location, "*");
            Log(filenames.Count.ToString() + " files found.");
            bool info_file_exist = false;

            for (int i = filenames.Count - 1; i >= 0; i--)
            {
                string file_path = filenames[i];
                string filename = Utils.StringUtils.ItemNameFromPath(file_path).ToLower();
                string extension = Utils.StringUtils.ExtensionFromFilename(filename);

                if (filename == Manager.ComicInfoFileName)
                {
                    info_file_exist = true;
                }

                if (!Utils.AppInfoProvider.IsSupportedImageExtension(extension))
                {
                    filenames.RemoveAt(i);
                }
            }

            if (filenames.Count == 0)
            {
                if (is_exist)
                {
                    await Manager.RemoveWithLocation(db, location);
                }
                return;
            }

            // Update or create a new one.
            ComicData comic;

            if (is_exist)
            {
                comic = await Manager.FromLocation(db, location);
            }
            else
            {
                comic = FromDatabase(location);
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

            if (is_exist)
            {
                return;
            }

            comic.SetAsDefaultInfo();
            await comic.SaveAll(db);
        }
    }
}
