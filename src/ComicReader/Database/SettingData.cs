using ComicReader.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace ComicReader.Database
{
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
        public PageArrangementType VerticalPageArrangement = PageArrangementType.Single;
        public PageArrangementType HorizontalPageArrangement = PageArrangementType.DualCoverMirror;
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

    public enum PageArrangementType
    {
        Single, // 1 2 3 4 5
        DualCover, // 1 23 45
        DualCoverMirror, // 1 32 54
        DualNoCover, // 12 34 5
        DualNoCoverMirror, // 21 43 5
    }

    class SettingDataManager
    {
        public static async Task<TaskException> AddComicFolder(StorageFolder folder, bool final)
        {
            await XmlDatabaseManager.WaitLock();
            try
            {
                AddComicFolderNoLock(folder);
            }
            finally
            {
                XmlDatabaseManager.ReleaseLock();
            }

            if (final)
            {
                Utils.TaskQueue.DefaultQueue.Enqueue(XmlDatabaseManager.SaveSealed(XmlDatabaseItem.Settings));
            }

            return TaskException.Success;
        }

        private static TaskException AddComicFolderNoLock(StorageFolder folder)
        {
            if (!Utils.Storage.AllowAddToFutureAccessList())
            {
                return TaskException.MaximumExceeded;
            }

            Utils.Storage.AddToFutureAccessList(folder);

            string path = folder.Path;
            bool folder_added = false;
            foreach (string old_path in XmlDatabase.Settings.ComicFolders)
            {
                if (StringUtils.FolderContain(old_path, path))
                {
                    return TaskException.ItemExists;
                }
                else if (StringUtils.FolderContain(path, old_path))
                {
                    XmlDatabase.Settings.ComicFolders[XmlDatabase.Settings.ComicFolders.IndexOf(old_path)] = path;
                    folder_added = true;
                    break;
                }
            }

            if (!folder_added)
            {
                XmlDatabase.Settings.ComicFolders.Add(path);
            }

            return TaskException.Success;
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
                Utils.TaskQueue.DefaultQueue.Enqueue(XmlDatabaseManager.SaveSealed(XmlDatabaseItem.Settings));
            }
        }

        public static async Task<bool> AddComicFolderUsingPicker()
        {
            FolderPicker picker = InitializeWithWindow(new FolderPicker(), App.Window.WindowHandle);
            picker.FileTypeFilter.Add("*");
            StorageFolder folder = await picker.PickSingleFolderAsync();
            if (folder == null)
            {
                return false;
            }

            TaskException r = await AddComicFolder(folder, true);
            return r.Successful();
        }

        private static FolderPicker InitializeWithWindow(FolderPicker obj, IntPtr windowHandle)
        {
            WinRT.Interop.InitializeWithWindow.Initialize(obj, windowHandle);
            return obj;
        }
    }
}
