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

    public class FavoriteData : AppData
    {
        public List<FavoriteNodeData> RootNodes = new List<FavoriteNodeData>();

        // serialization
        public override string FileName => "Favorites";

        [XmlIgnore]
        public override AppData Target
        {
            get => Database.Favorites;
            set
            {
                Database.Favorites = value as FavoriteData;
            }
        }

        public override void Pack() { }

        public override void Unpack() { }
    }

    public class FavoriteNodeData
    {
        [XmlAttribute]
        public string Type;
        [XmlAttribute]
        public string Name;
        [XmlAttribute]
        public string Id;
        public List<FavoriteNodeData> Children = new List<FavoriteNodeData>();
    };

    class FavoriteDataManager
    {
        public static async Task<FavoriteNodeData> FromId(string id)
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

        private static FavoriteNodeData FromIdNoLock(string id)
        {
            FavoriteNodeData helper(List<FavoriteNodeData> e)
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
            bool helper(List<FavoriteNodeData> e)
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

                FavoriteNodeData node = new FavoriteNodeData
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

            if (Views.FavoritePage.Current != null)
            {
                await Views.FavoritePage.Current.Update();
            }
        }
    }
}
