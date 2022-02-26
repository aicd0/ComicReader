using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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

    public class ComicZipData : ComicData
    {
        private StorageFile Archive;
        private List<string> Entries = new List<string>();
        public override int ImageCount => Entries.Count;
        public override bool IsEditable => true;

        private ComicZipData(bool is_external) :
            base(ComicType.Zip, is_external) { }

        public static ComicData FromDatabase(string location)
        {
            return new ComicZipData(false)
            {
                Location = location,
            };
        }

        public static async Task<ComicData> FromExternal(LockContext db, StorageFile file)
        {
            ComicZipData comic = new ComicZipData(true)
            {
                Location = file.Path,
                Archive = file,
            };

            await comic.UpdateImages(db);
            return comic;
        }

        private async RawTask CompleteArchive()
        {
            if (Archive != null)
            {
                return new TaskResult();
            }

            if (Location == null)
            {
                return new TaskResult(TaskException.InvalidParameters);
            }

            StorageFile file = await Utils.C0.TryGetFile(Location);

            if (file == null)
            {
                return new TaskResult(TaskException.NoPermission);
            }

            Archive = file;
            return new TaskResult();
        }

        public override async RawTask UpdateInfo()
        {
            TaskResult r = await CompleteArchive();

            if (!r.Successful)
            {
                return r;
            }

            string info_text;

            using (Stream stream = await Archive.OpenStreamForReadAsync())
            using (ZipArchive archive = new ZipArchive(stream))
            {
                ZipArchiveEntry info_entry = null;

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName.Contains('/'))
                    {
                        continue;
                    }

                    if (entry.FullName.ToLower().Equals(Manager.ComicInfoFileName))
                    {
                        info_entry = entry;
                        break;
                    }
                }

                if (info_entry == null)
                {
                    return new TaskResult(TaskException.Failure);
                }

                using (StreamReader reader = new StreamReader(info_entry.Open()))
                {
                    info_text = await reader.ReadToEndAsync();
                }
            }

            ParseInfo(info_text);
            return new TaskResult();
        }

        protected override async RawTask SaveInfoFile()
        {
            TaskResult r = await CompleteArchive();

            if (!r.Successful)
            {
                return r;
            }

            string text = InfoString();

            using (IRandomAccessStream stream = await Archive.OpenAsync(FileAccessMode.ReadWrite))
            using (ZipArchive archive = new ZipArchive(stream.AsStream(), ZipArchiveMode.Update))
            {
                ZipArchiveEntry info_entry = null;

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName.Contains('/'))
                    {
                        continue;
                    }

                    if (entry.FullName.ToLower().Equals(Manager.ComicInfoFileName))
                    {
                        info_entry = entry;
                        break;
                    }
                }

                if (info_entry != null)
                {
                    info_entry.Delete();
                }

                info_entry = archive.CreateEntry(Manager.ComicInfoFileName);

                using (StreamWriter writer = new StreamWriter(info_entry.Open()))
                {
                    await writer.WriteAsync(text);
                }
            }

            return new TaskResult();
        }

        public override async RawTask UpdateImages(LockContext db, bool cover = false)
        {
            TaskResult r = await CompleteArchive();

            if (!r.Successful)
            {
                return r;
            }

            Entries.Clear();

            using (Stream stream = await Archive.OpenStreamForReadAsync())
            using (ZipArchive archive = new ZipArchive(stream))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName.Contains('/'))
                    {
                        continue;
                    }

                    string extension = Utils.StringUtils.ExtensionFromFilename(entry.FullName);

                    if (!Utils.AppInfoProvider.IsSupportedImageExtension(extension))
                    {
                        continue;
                    }

                    Entries.Add(entry.FullName);

                    if (cover)
                    {
                        break;
                    }
                }
            }

            return new TaskResult();
        }

        public override async Task<IRandomAccessStream> GetImageStream(int index)
        {
            if (index >= Entries.Count)
            {
                System.Diagnostics.Debug.Assert(false);
                return null;
            }

            using (Stream stream = await Archive.OpenStreamForReadAsync())
            using (ZipArchive archive = new ZipArchive(stream))
            {
                ZipArchiveEntry entry = archive.GetEntry(Entries[index]);

                // Create a .NET memory stream.
                var mem_stream = new MemoryStream();

                // Convert the stream to the memory stream, because a memory stream supports seeking.
                await entry.Open().CopyToAsync(mem_stream);

                // Set the start position.
                mem_stream.Position = 0;

                return mem_stream.AsRandomAccessStream();
            }
        }

        public static async Task Update(LockContext db, string location, bool is_exist)
        {
            StorageFile archive_file = await Utils.C0.TryGetFile(location);

            if (archive_file == null)
            {
                Log("Failed to open '" + location + "', no permission");
                return;
            }

            Stream stream = await archive_file.OpenStreamForReadAsync();
            ZipArchive archive = new ZipArchive(stream);

            List<string> filenames = new List<string>();
            bool info_file_exist = false;

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string filename = entry.FullName;

                if (filename.IndexOf('/') != -1)
                {
                    continue;
                }

                if (filename == Manager.ComicInfoFileName)
                {
                    info_file_exist = true;
                }

                string extension = Utils.StringUtils.ExtensionFromFilename(filename);

                if (!Utils.AppInfoProvider.IsSupportedImageExtension(extension))
                {
                    continue;
                }

                filenames.Add(filename);
            }

            Log(filenames.Count.ToString() + " images found.");

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
