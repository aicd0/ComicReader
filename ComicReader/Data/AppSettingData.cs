using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace ComicReader.Data
{
    using RawTask = Task<Utils.TaskQueue.TaskResult>;
    using SealedTask = Func<Task<Utils.TaskQueue.TaskResult>, Utils.TaskQueue.TaskResult>;
    using TaskResult = Utils.TaskQueue.TaskResult;
    using TaskException = Utils.TaskQueue.TaskException;

    public class AppSettingData : AppData
    {
        public List<string> ComicFolders = new List<string>();
        public bool RightToLeft = false;
        public bool SaveHistory = true;

        // serialization
        public override string FileName => "Settings";

        [XmlIgnore]
        public override AppData Target
        {
            get => Database.AppSettings;
            set
            {
                Database.AppSettings = value as AppSettingData;
            }
        }

        public override void Pack() { }

        public override void Unpack() { }
    }
    class AppSettingDataManager
    {
        public static async RawTask AddComicFolder(string folder, bool final)
        {
            await DatabaseManager.WaitLock();

            try
            {
                string folder_lower = folder.ToLower();
                bool folder_inserted = false;

                foreach (string old_folder in Database.AppSettings.ComicFolders)
                {
                    string old_folder_lower = old_folder.ToLower();

                    if (folder_lower.Length > old_folder_lower.Length)
                    {
                        if (folder_lower.Substring(0, old_folder_lower.Length).Equals(old_folder_lower))
                        {
                            return new TaskResult(TaskException.ItemExists);
                        }
                    }
                    else if (folder_lower.Length < old_folder_lower.Length)
                    {
                        if (old_folder_lower.Substring(0, folder_lower.Length).Equals(folder_lower))
                        {
                            Database.AppSettings.ComicFolders[Database.AppSettings.ComicFolders.IndexOf(old_folder)] = folder;
                            folder_inserted = true;
                            break;
                        }
                    }
                    else if (folder_lower.Equals(old_folder_lower))
                    {
                        return new TaskResult(TaskException.ItemExists);
                    }
                }

                if (!folder_inserted)
                {
                    Database.AppSettings.ComicFolders.Add(folder);
                }
            }
            finally
            {
                DatabaseManager.ReleaseLock();
            }

            if (final)
            {
                Utils.TaskQueue.TaskQueueManager.AppendTask(
                    DatabaseManager.SaveSealed(DatabaseItem.AppSettings));
            }

            return new TaskResult();
        }

        public static async Task RemoveComicFolder(string folder, bool final = false)
        {
            await DatabaseManager.WaitLock();
            _ = Database.AppSettings.ComicFolders.Remove(folder);
            DatabaseManager.ReleaseLock();

            if (final)
            {
                Utils.TaskQueue.TaskQueueManager.AppendTask(
                    DatabaseManager.SaveSealed(DatabaseItem.AppSettings));
            }
        }

        public static async Task<bool> AddComicFolderUsingPicker()
        {
            FolderPicker picker = new FolderPicker();
            _ = picker.FileTypeFilter.Append("*");
            StorageFolder folder = await picker.PickSingleFolderAsync();

            if (folder == null)
            {
                return false;
            }

            TaskResult res = await AddComicFolder(folder.Path, true);

            if (res.ExceptionType != TaskException.Success)
            {
                return false;
            }

            StorageApplicationPermissions.FutureAccessList.AddOrReplace(
                Utils.StringUtils.TokenFromPath(folder.Path), folder);
            return true;
        }
    }
}
