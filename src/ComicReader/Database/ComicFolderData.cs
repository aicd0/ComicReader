using ComicReader.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.Storage.Streams;

namespace ComicReader.Database
{
    public class ComicFolderData : ComicData
    {
        private StorageFolder Folder { get; set; }
        private StorageFile InfoFile { get; set; }
        private List<StorageFile> ImageFiles { get; set; } = new List<StorageFile>();

        public override int ImageCount => ImageFiles.Count;
        public override bool IsEditable => !(IsExternal && InfoFile == null);

        private ComicFolderData(bool is_external) :
            base(ComicType.Folder, is_external) { }

        public static ComicData FromDatabase(string location)
        {
            return new ComicFolderData(false)
            {
                Location = location,
            };
        }

        public static async Task<ComicData> FromExternal(string directory, List<StorageFile> image_files, StorageFile info_file)
        {
            image_files = image_files.OrderBy(x => x.DisplayName,
                new Utils.StringUtils.FileNameComparer()).ToList();

            ComicFolderData comic = new ComicFolderData(true)
            {
                Location = directory,
                ImageFiles = image_files,
                InfoFile = info_file,
            };

            if (info_file != null)
            {
                await comic.LoadFromInfoFile();
            }

            return comic;
        }

        private async Task<TaskException> SetFolder()
        {
            if (Folder != null)
            {
                return TaskException.Success;
            }

            if (Location == null)
            {
                return TaskException.InvalidParameters;
            }

            StorageFolder folder = await Utils.Storage.TryGetFolder(Location);

            if (folder == null)
            {
                return TaskException.NoPermission;
            }

            Folder = folder;
            return TaskException.Success;
        }

        private async Task<TaskException> SetInfoFile(bool create_if_not_exists)
        {
            if (InfoFile != null)
            {
                return TaskException.Success;
            }

            if (IsExternal)
            {
                return create_if_not_exists ? TaskException.NoPermission : TaskException.FileNotFound;
            }

            TaskException r = await SetFolder();

            if (!r.Successful())
            {
                return r;
            }

            IStorageItem item = await Folder.TryGetItemAsync(ComicInfoFileName);

            if (item == null)
            {
                if (!create_if_not_exists)
                {
                    return TaskException.FileNotFound;
                }

                InfoFile = await Folder.CreateFileAsync(ComicInfoFileName, CreationCollisionOption.OpenIfExists);
                return TaskException.Success;
            }

            if (!(item is StorageFile))
            {
                return TaskException.NameCollision;
            }

            InfoFile = (StorageFile)item;
            return TaskException.Success;
        }

        public override async Task<TaskException> LoadFromInfoFile()
        {
            TaskException r = await SetInfoFile(false);

            if (!r.Successful())
            {
                return r;
            }

            string info_text;

            try
            {
                info_text = await FileIO.ReadTextAsync(InfoFile);
            }
            catch (Exception e)
            {
                Log("Failed to access '" + InfoFile.Path + "'. " + e.ToString());
                return TaskException.Failure;
            }

            ParseInfo(info_text);
            return TaskException.Success;
        }

        protected override async Task<TaskException> SaveToInfoFile()
        {
            TaskException r = await SetInfoFile(true);

            if (!r.Successful())
            {
                return r;
            }

            string text = InfoString();
            IBuffer buffer = Utils.C0.GetBufferFromString(text);

            try
            {
                await FileIO.WriteBufferAsync(InfoFile, buffer);
            }
            catch (Exception e)
            {
                Log("Failed to access '" + InfoFile.Path + "'. " + e.ToString());
                return TaskException.Failure;
            }

            return TaskException.Success;
        }

        protected override async Task<TaskException> ReloadImages()
        {
            if (IsExternal)
            {
                return TaskException.Success;
            }

            TaskException result = await SetFolder();
            if (!result.Successful())
            {
                return result;
            }

            // Load all images.
            Log("Retrieving images in '" + Location + "'");
            QueryOptions query_options = new QueryOptions
            {
                FolderDepth = FolderDepth.Shallow,
                IndexerOption = IndexerOption.DoNotUseIndexer, // The results from UseIndexerWhenAvailable are incomplete.
            };

            foreach (string type in Common.AppInfoProvider.SupportedImageExtensions)
            {
                query_options.FileTypeFilter.Add(type);
            }

            StorageFileQueryResult query = Folder.CreateFileQueryWithOptions(query_options);
            IReadOnlyList<StorageFile> img_files = await query.GetFilesAsync();

            // Sort by display name.
            ImageFiles = img_files.OrderBy(x => x.DisplayName,
                new Utils.StringUtils.FileNameComparer()).ToList();
            Log(img_files.Count.ToString() + " images added.");
            return TaskException.Success;
        }

        protected override async Task<IRandomAccessStream> InternalGetImageStream(int index)
        {
            if (index >= ImageFiles.Count)
            {
                Log("Image index " + index.ToString() + " out of boundary " + ImageFiles.Count.ToString());
                return null;
            }

            try
            {
                return await ImageFiles[index].OpenAsync(FileAccessMode.Read);
            }
            catch (Exception e)
            {
                Log("Failed to access '" + ImageFiles[index].Path + "'. " + e.ToString());
                return null;
            }
        }
    }
}
