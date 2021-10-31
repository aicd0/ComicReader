using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ComicReader.Data
{
    using RawTask = Task<Utils.TaskQueue.TaskResult>;
    using SealedTask = Func<Task<Utils.TaskQueue.TaskResult>, Utils.TaskQueue.TaskResult>;
    using TaskResult = Utils.TaskQueue.TaskResult;
    using TaskException = Utils.TaskQueue.TaskException;

    public class FavoritesData
    {
        public List<FavoritesNodeData> RootNodes = new List<FavoritesNodeData>();

        public void Pack() { }

        public void Unpack() { }
    }

    public class FavoritesNodeData
    {
        [XmlAttribute]
        public string Type;
        [XmlAttribute]
        public string Name;
        [XmlAttribute]
        public string Id;
        public List<FavoritesNodeData> Children = new List<FavoritesNodeData>();
    };

    class FavoritesDataManager
    {
        private const string FAVORITES_DATA_FILE_NAME = "fav";

        public static async Task Save(StorageFolder user_folder)
        {
            await DatabaseManager.WaitLock();
            StorageFile file = await user_folder.CreateFileAsync(
                FAVORITES_DATA_FILE_NAME, CreationCollisionOption.ReplaceExisting);
            IRandomAccessStream stream = await file.OpenAsync(
                FileAccessMode.ReadWrite);

            Database.Favorites.Pack();
            XmlSerializer serializer = new XmlSerializer(typeof(FavoritesData));
            serializer.Serialize(stream.AsStream(), Database.Favorites);

            stream.Dispose();
            DatabaseManager.ReleaseLock();
        }

        public static async RawTask Load(StorageFolder user_folder)
        {
            object file = await DatabaseManager.TryGetFile(user_folder, FAVORITES_DATA_FILE_NAME);

            if (file == null)
            {
                return new TaskResult(TaskException.FileNotExists);
            }

            IRandomAccessStream stream =
                await ((StorageFile)file).OpenAsync(FileAccessMode.Read);

            XmlSerializer serializer = new XmlSerializer(typeof(FavoritesData));
            Database.Favorites =
                (FavoritesData)serializer.Deserialize(stream.AsStream());
            Database.Favorites.Unpack();

            stream.Dispose();
            return new TaskResult();
        }

        public static async Task<FavoritesNodeData> FromId(string id)
        {
            await DatabaseManager.WaitLock();
            try
            {
                return FromIdNoLock(id);
            }
            finally
            {
                DatabaseManager.ReleaseLock();
            }
        }

        private static FavoritesNodeData FromIdNoLock(string id)
        {
            FavoritesNodeData helper(List<FavoritesNodeData> e)
            {
                foreach (var node in e)
                {
                    if (node.Type == "i")
                    {
                        if (node.Id == id)
                        {
                            return node;
                        }
                    }
                    else
                    {
                        if (!(node.Children.Count == 0))
                        {
                            var result = helper(node.Children);

                            if (result != null)
                            {
                                return result;
                            }
                        }
                    }
                }

                return null;
            }

            return helper(Database.Favorites.RootNodes);
        }

        public static async Task<bool> RemoveWithId(string id, bool final)
        {
            bool helper(List<FavoritesNodeData> e)
            {
                for (int i = 0; i < e.Count; ++i)
                {
                    var node = e[i];

                    if (node.Type == "i")
                    {
                        if (node.Id == id)
                        {
                            e.RemoveAt(i);
                            return true;
                        }
                    }
                    else
                    {
                        if (!(node.Children.Count == 0))
                        {
                            if (helper(node.Children))
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }

            await DatabaseManager.WaitLock();
            bool res = helper(Database.Favorites.RootNodes);
            DatabaseManager.ReleaseLock();

            if (final)
            {
                await Update();
            }

            return res;
        }

        public static async Task Add(string id, string title, bool final)
        {
            await DatabaseManager.WaitLock();

            try
            {
                if (FromIdNoLock(id) != null)
                {
                    return;
                }

                FavoritesNodeData node = new FavoritesNodeData
                {
                    Type = "i",
                    Name = title,
                    Id = id
                };

                Database.Favorites.RootNodes.Add(node);
            }
            finally
            {
                DatabaseManager.ReleaseLock();
            }

            if (final)
            {
                await Update();
            }
        }

        private static async Task Update()
        {
            Utils.TaskQueue.TaskQueueManager.AppendTask(
                DatabaseManager.SaveSealed(DatabaseItem.Favorites));

            if (Views.FavoritesPage.Current != null)
            {
                await Views.FavoritesPage.Current.Update();
            }
        }
    }
}
