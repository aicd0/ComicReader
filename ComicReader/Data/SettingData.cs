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
    using TaskResult = Utils.TaskQueue.TaskResult;
    using TaskException = Utils.TaskQueue.TaskException;

    public class SettingData : XmlData
    {
        public List<string> ComicFolders = new List<string>();
        public bool RightToLeft = false;
        public bool SaveHistory = true;

        // serialization
        public override string FileName => "Settings";

        [XmlIgnore]
        public override XmlData Target
        {
            get => XmlDatabase.Settings;
            set => XmlDatabase.Settings = value as SettingData;
        }

        public override void Pack() { }

        public override void Unpack() { }
    }
    class SettingDataManager
    {
        public static async RawTask AddComicFolder(string folder, bool final)
        {
            await XmlDatabaseManager.WaitLock();
            try
            {
                string folder_lower = folder.ToLower();
                bool folder_inserted = false;

                foreach (string old_folder in XmlDatabase.Settings.ComicFolders)
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
                            XmlDatabase.Settings.ComicFolders[XmlDatabase.Settings.ComicFolders.IndexOf(old_folder)] = folder;
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
                    XmlDatabase.Settings.ComicFolders.Add(folder);
                }
            }
            finally
            {
                XmlDatabaseManager.ReleaseLock();
            }

            if (final)
            {
                Utils.TaskQueue.TaskQueueManager.AppendTask(
                    XmlDatabaseManager.SaveSealed(XmlDatabaseItem.Settings));
            }

            return new TaskResult();
        }

        public static async Task RemoveComicFolder(string folder, bool final = false)
        {
            await XmlDatabaseManager.WaitLock();
            _ = XmlDatabase.Settings.ComicFolders.Remove(folder);
            XmlDatabaseManager.ReleaseLock();

            if (final)
            {
                Utils.TaskQueue.TaskQueueManager.AppendTask(
                    XmlDatabaseManager.SaveSealed(XmlDatabaseItem.Settings));
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
