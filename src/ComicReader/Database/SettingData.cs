using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace ComicReader.Database
{
    using RawTask = Task<Utils.TaskResult>;
    using TaskResult = Utils.TaskResult;
    using TaskException = Utils.TaskException;

    public class SettingData : XmlData
    {
        public int DatabaseVersion = -1;
        public List<string> ComicFolders = new List<string>();
        public int DefaultArchiveCodePage = -1;
        public bool VerticalReading = true;
        public bool LeftToRight = false;
        public bool VerticalContinuous = true;
        public bool HorizontalContinuous = false;
        public bool TransitionAnimation = true;
        public DesignData.PageArrangementEnum VerticalPageArrangement = DesignData.PageArrangementEnum.Single;
        public DesignData.PageArrangementEnum HorizontalPageArrangement = DesignData.PageArrangementEnum.DualCoverMirror;
        public bool SaveHistory = true;

        public bool DebugMode =
#if DEBUG
        true;
#else
        false;
#endif

        // serialization
        public override string FileName => "Settings";

        [XmlIgnore]
        public override XmlData Target
        {
            get => XmlDatabase.Settings;
            set => XmlDatabase.Settings = value as SettingData;
        }
    }

    class SettingDataManager
    {
        public static async RawTask AddComicFolder(StorageFolder folder, bool final)
        {
            await XmlDatabaseManager.WaitLock();
            try
            {
                string path = folder.Path;
                string path_lower = path.ToLower();
                bool folder_inserted = false;

                foreach (string old_path in XmlDatabase.Settings.ComicFolders)
                {
                    string old_path_lower = old_path.ToLower();

                    if (path_lower.Length > old_path_lower.Length)
                    {
                        if (path_lower.Substring(0, old_path_lower.Length).Equals(old_path_lower))
                        {
                            return new TaskResult(TaskException.ItemExists);
                        }
                    }
                    else if (path_lower.Length < old_path_lower.Length)
                    {
                        if (old_path_lower.Substring(0, path_lower.Length).Equals(path_lower))
                        {
                            XmlDatabase.Settings.ComicFolders[XmlDatabase.Settings.ComicFolders.IndexOf(old_path)] = path;
                            folder_inserted = true;
                            break;
                        }
                    }
                    else if (path_lower.Equals(old_path_lower))
                    {
                        return new TaskResult(TaskException.ItemExists);
                    }
                }

                if (!folder_inserted)
                {
                    XmlDatabase.Settings.ComicFolders.Add(path);
                }

                Utils.Storage.AddToFutureAccessList(folder);
            }
            finally
            {
                XmlDatabaseManager.ReleaseLock();
            }

            if (final)
            {
                Utils.TaskQueueManager.AppendTask(
                    XmlDatabaseManager.SaveSealed(XmlDatabaseItem.Settings));
            }

            return new TaskResult();
        }

        public static async Task RemoveComicFolder(string path, bool final)
        {
            await XmlDatabaseManager.WaitLock();
            _ = XmlDatabase.Settings.ComicFolders.Remove(path);

            string token = Utils.StringUtils.TokenFromPath(path);
            Utils.Storage.RemoveFromFutureAccessList(token);
            XmlDatabaseManager.ReleaseLock();

            if (final)
            {
                Utils.TaskQueueManager.AppendTask(
                    XmlDatabaseManager.SaveSealed(XmlDatabaseItem.Settings));
            }
        }

        public static async Task<bool> AddComicFolderUsingPicker()
        {
            FolderPicker picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            StorageFolder folder = await picker.PickSingleFolderAsync();

            if (folder == null)
            {
                return false;
            }

            TaskResult r = await AddComicFolder(folder, true);
            return r.Successful;
        }
    }
}
