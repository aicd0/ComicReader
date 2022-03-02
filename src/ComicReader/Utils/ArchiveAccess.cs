using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ComicReader.Utils
{
    using RawTask = Task<Utils.TaskResult>;
    using SealedTask = Func<Task<Utils.TaskResult>, Utils.TaskResult>;

    class ArchiveAccess
    {
        public const string FileSeperator = "\\\\";

        public static string GetBasePath(string location, bool reverse = false)
        {
            int i = reverse ?
                location.LastIndexOf(FileSeperator) :
                location.IndexOf(FileSeperator);

            if (i == -1)
            {
                return location;
            }

            return location.Substring(0, i);
        }

        public static string GetSubPath(string location, bool reverse = false)
        {
            int i = reverse ?
                location.LastIndexOf(FileSeperator) :
                location.IndexOf(FileSeperator);

            if (i == -1)
            {
                return "";
            }

            return location.Substring(i + FileSeperator.Length);
        }

        public static async Task<Stream> TryGetFileStream(string location)
        {
            string base_path = GetBasePath(location);
            string sub_path = GetSubPath(location);
            StorageFile base_file = await Utils.Storage.TryGetFile(base_path);

            if (base_file == null)
            {
                return null;
            }

            return await TryGetFileStream(base_file, sub_path);
        }

        public static async Task<Stream> TryGetFileStream(StorageFile base_file, string sub_path)
        {
            if (sub_path.Length == 0)
            {
                try
                {
                    return await base_file.OpenStreamForReadAsync();
                }
                catch (Exception e)
                {
                    Log("Failed to access '" + base_file.Path + FileSeperator + sub_path + "'. " + e.ToString());
                    return null;
                }
            }

            var mem_stream = new MemoryStream();

            TaskResult r = await TryAccessFileStream(base_file, sub_path, FileAccessMode.Read,
                async (Stream stream) =>
                {
                    await stream.CopyToAsync(mem_stream);
                    mem_stream.Position = 0;
                    return new TaskResult();
                });

            if (!r.Successful)
            {
                mem_stream.Dispose();
                return null;
            }

            return mem_stream;
        }

        public static async RawTask TryAccessFileStream(StorageFile base_file, string sub_path, FileAccessMode mode, Func<Stream, RawTask> func)
        {
            if (base_file == null)
            {
                return new TaskResult(TaskException.InvalidParameters);
            }

            Stream stream;

            try
            {
                if (mode == FileAccessMode.Read)
                {
                    stream = await base_file.OpenStreamForReadAsync();
                }
                else
                {
                    stream = await base_file.OpenStreamForWriteAsync();
                }
            }
            catch (Exception e)
            {
                Log("Failed to access '" + base_file.Path + FileSeperator + sub_path + "'. " + e.ToString());
                return null;
            }

            TaskResult r = await TryAccessFileStream(stream, base_file.FileType, sub_path, mode, func);
            stream.Dispose();
            return r;
        }

        private static void Log(string text)
        {
            Utils.Debug.Log("ArchiveAccess: " + text);
        }

        public static async RawTask TryRemoveFile(StorageFile base_file, string sub_path)
        {
            return await TryAccessDeepestArchive(base_file, sub_path, FileAccessMode.ReadWrite,
                async (Stream stream, ArchiveAccessContext ctx) =>
                {
                    return await Task.Run(() =>
                    {
                        return TryRemoveEntry(stream, ctx.Extension, ctx.Entry);
                    });
                });
        }

        public static async RawTask TryReplaceFile(StorageFile base_file, string sub_path, Stream stream_replace)
        {
            System.Diagnostics.Debug.Assert(false); // deprecated.
            return await TryAccessDeepestArchive(base_file, sub_path, FileAccessMode.ReadWrite,
                async (Stream stream, ArchiveAccessContext ctx) =>
                {
                    return await TryReplaceEntry(stream, ctx.Extension, ctx.Entry, stream_replace);
                });
        }

        public static async RawTask TryGetSubFiles(StorageFile base_file, string sub_path, List<string> output)
        {
            return await TryAccessDeepestArchive(base_file, sub_path, FileAccessMode.Read,
                async (Stream stream, ArchiveAccessContext ctx) =>
                {
                    return await Task.Run(() =>
                    {
                        return TryReadEntries(stream, ctx.Extension, ctx.Entry, output);
                    });
                });
        }

        private class ArchiveAccessContext
        {
            public string Entry;
            public string Extension;
        }

        private static async RawTask TryAccessDeepestArchive(StorageFile base_file, string sub_path,
            FileAccessMode mode, Func<Stream, ArchiveAccessContext, RawTask> func)
        {
            string sub_base_path = GetBasePath(sub_path, reverse: true);
            string entry = GetSubPath(sub_path, reverse: true);
            string extension = Utils.StringUtils.ExtensionFromFilename(sub_base_path);

            if (entry.Length == 0)
            {
                entry = sub_base_path;
                sub_base_path = "";
                extension = base_file.FileType;
            }

            ArchiveAccessContext ctx = new ArchiveAccessContext
            {
                Entry = entry,
                Extension = extension,
            };

            return await TryAccessFileStream(base_file, sub_base_path, mode,
                async (Stream stream) => await func(stream, ctx));
        }

        private static ZipArchiveMode ToZipArchiveMode(FileAccessMode mode)
        {
            switch (mode)
            {
                case FileAccessMode.Read:
                    return ZipArchiveMode.Read;
                case FileAccessMode.ReadWrite:
                    return ZipArchiveMode.Update;
                default:
                    throw new Exception();
            }
        }

        private static async RawTask TryAccessFileStream(Stream stream, string extension, string sub_path, FileAccessMode mode, Func<Stream, RawTask> func)
        {
            if (stream == null)
            {
                System.Diagnostics.Debug.Assert(false);
                return new TaskResult(TaskException.InvalidParameters);
            }

            return await _TryAccessFileStream(stream, extension.ToLower(), sub_path, mode, func);
        }

        private static async RawTask _TryAccessFileStream(Stream stream,
            string extension, string sub_path, FileAccessMode mode, Func<Stream, RawTask> func)
        {
            if (sub_path.Length == 0)
            {
                return await func(stream);
            }

            string entry_name = GetBasePath(sub_path);
            string sub_entry_name = GetSubPath(sub_path);
            string filename = Utils.StringUtils.ItemNameFromPath(entry_name);
            string sub_extension = Utils.StringUtils.ExtensionFromFilename(filename);
            TaskResult res = new TaskResult(TaskException.Unknown);

            switch (extension)
            {
                case ".zip":
                    entry_name = entry_name.Replace('\\', '/');

                    using (ZipArchive archive = new ZipArchive(stream, ToZipArchiveMode(mode)))
                    {
                        ZipArchiveEntry entry = archive.GetEntry(entry_name);

                        if (entry == null)
                        {
                            res = new TaskResult(TaskException.FileNotFound);
                            break;
                        }

                        using (Stream sub_stream = entry.Open())
                        {
                            res = await _TryAccessFileStream(sub_stream, sub_extension, sub_entry_name, mode, func);
                        }
                    }
                    break;
                
                default:
                    res = new TaskResult(TaskException.UnknownEnum);
                    break;
            }

            return res;
        }

        private static TaskResult TryRemoveEntry(Stream stream, string extension, string entry)
        {
            if (stream == null)
            {
                return new TaskResult(TaskException.InvalidParameters);
            }

            switch (extension.ToLower())
            {
                case ".zip":
                    entry = entry.Replace('\\', '/');

                    using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Update))
                    {
                        ZipArchiveEntry x = archive.GetEntry(entry);

                        if (x == null)
                        {
                            return new TaskResult(TaskException.FileNotFound);
                        }

                        x.Delete();
                    }
                    break;

                default:
                    return new TaskResult(TaskException.UnknownEnum);
            }

            return new TaskResult();
        }

        private static async RawTask TryReplaceEntry(Stream stream, string extension, string entry, Stream entry_stream)
        {
            if (stream == null || !stream.CanRead || !stream.CanWrite)
            {
                return new TaskResult(TaskException.InvalidParameters);
            }

            if (entry_stream == null || !stream.CanRead)
            {
                return new TaskResult(TaskException.InvalidParameters);
            }

            switch (extension.ToLower())
            {
                case ".zip":
                    entry = entry.Replace('\\', '/');

                    using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Update))
                    {
                        ZipArchiveEntry x = archive.GetEntry(entry);

                        if (x == null)
                        {
                            return new TaskResult(TaskException.FileNotFound);
                        }

                        x.Delete();
                        x = archive.CreateEntry(entry);

                        using (Stream new_entry_stream = x.Open())
                        {
                            await entry_stream.CopyToAsync(new_entry_stream);
                        }
                    }
                    break;

                default:
                    return new TaskResult(TaskException.UnknownEnum);
            }

            return new TaskResult();
        }

        private static TaskResult TryReadEntries(Stream stream, string extension, string entry, List<string> output)
        {
            if (stream == null || !stream.CanRead)
            {
                return new TaskResult(TaskException.InvalidParameters);
            }

            switch (extension.ToLower())
            {
                case ".zip":
                    entry = entry.Replace('\\', '/');

                    using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
                    {
                        foreach (ZipArchiveEntry e in archive.Entries)
                        {
                            if (Utils.StringUtils.PathContain(entry, e.FullName))
                            {
                                string subpath = e.FullName.Substring(entry.Length);

                                if (subpath.Length == 0)
                                {
                                    continue;
                                }

                                if (subpath[0] == '/')
                                {
                                    subpath = subpath.Substring(1);
                                }

                                if (subpath.Contains('/'))
                                {
                                    continue;
                                }

                                output.Add(subpath);
                            }
                        }
                    }
                    break;

                default:
                    return new TaskResult(TaskException.UnknownEnum);
            }

            return new TaskResult();
        }
    }
}
