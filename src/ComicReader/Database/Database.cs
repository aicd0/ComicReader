using ComicReader.Utils;
using System.Threading.Tasks;

namespace ComicReader.Database
{
    public class DatabaseManager
    {
        public static bool DatabaseFirstInit { get; private set; }

        private static bool Initialized { get; set; } = false;

        public static async Task<TaskException> Init()
        {
            if (Initialized)
            {
                return TaskException.Success;
            }

            Initialized = true;

            Log("Initializing database");

            // For backward compability.
            DatabaseFirstInit = !await SqliteDatabaseManager.IsDatabaseExist();

            await SqliteDatabaseManager.Init();
            await XmlDatabaseManager.Load();
            await Update();
            Utils.TaskQueue.DefaultQueue.Enqueue(ComicData.UpdateSealed(lazy_load: true));
            return TaskException.Success;
        }

        public static async Task<TaskException> Update()
        {
            int old_version = XmlDatabase.Settings.DatabaseVersion;

            switch (old_version)
            {
                case 1:
                case 0:
                    XmlDatabase.Settings.DatabaseVersion = 1;
                    await XmlDatabaseManager.SaveUnsealed(XmlDatabaseItem.Settings);
                    return TaskException.Success;
                default:
                    System.Diagnostics.Debug.Assert(false);
                    return TaskException.UnknownEnum;
            }
        }

        private static void Log(string content)
        {
            Utils.Debug.Log("Database: " + content);
        }
    }
}
