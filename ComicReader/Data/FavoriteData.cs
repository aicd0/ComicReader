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

    public class FavoriteData : XmlData
    {
        public List<FavoriteNodeData> RootNodes = new List<FavoriteNodeData>();

        // serialization
        public override string FileName => "Favorites";

        [XmlIgnore]
        public override XmlData Target
        {
            get => XmlDatabase.Favorites;
            set => XmlDatabase.Favorites = value as FavoriteData;
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
        public long Id;
        public List<FavoriteNodeData> Children = new List<FavoriteNodeData>();
    };

    class FavoriteDataManager
    {
        public static async Task<FavoriteNodeData> FromId(long id)
        {
            await XmlDatabaseManager.WaitLock();
            try
            {
                return FromIdNoLock(id);
            }
            finally
            {
                XmlDatabaseManager.ReleaseLock();
            }
        }

        public static FavoriteNodeData FromIdNoLock(long id)
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

            return helper(XmlDatabase.Favorites.RootNodes);
        }

        public static async Task<bool> RemoveWithId(long id, bool final)
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

            await XmlDatabaseManager.WaitLock();
            bool res = helper(XmlDatabase.Favorites.RootNodes);
            XmlDatabaseManager.ReleaseLock();

            if (final)
            {
                await Update();
            }

            return res;
        }

        public static async Task Add(long id, string title, bool final)
        {
            await XmlDatabaseManager.WaitLock();

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

                XmlDatabase.Favorites.RootNodes.Add(node);
            }
            finally
            {
                XmlDatabaseManager.ReleaseLock();
            }

            if (final)
            {
                await Update();
            }
        }

        private static async Task Update()
        {
            Utils.TaskQueue.TaskQueueManager.AppendTask(
                XmlDatabaseManager.SaveSealed(XmlDatabaseItem.Favorites));

            if (Views.FavoritePage.Current != null)
            {
                await Views.FavoritePage.Current.Update();
            }
        }
    }
}
