// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.DebugTools;

namespace ComicReader.Database;

public class DatabaseManager
{
    private static bool Initialized { get; set; } = false;

    public static async Task<TaskException> Init()
    {
        if (Initialized)
        {
            return TaskException.Success;
        }

        Initialized = true;

        Log("Initializing database");
        await SqliteDatabaseManager.Init();
        await XmlDatabaseManager.Load();
        AppDataRepository.Initialize();
        await Update();
        ComicData.UpdateAllComics("DatabaseManager#init", lazy: true);
        return TaskException.Success;
    }

    public static async Task<TaskException> Update()
    {
        int old_version = XmlDatabase.Settings.DatabaseVersion;

        switch (old_version)
        {
            case -1:
                goto case 2;
            case 0:
                goto case 2;
            case 1:
                goto case 2;
            case 2:
                XmlDatabase.Settings.DatabaseVersion = 2;
                await XmlDatabaseManager.SaveUnsealed(XmlDatabaseItem.Settings);
                return TaskException.Success;
            default:
                DebugUtils.Assert(false);
                return TaskException.UnknownEnum;
        }
    }

    private static void Log(string message)
    {
        Logger.I("Database", message);
    }
}
