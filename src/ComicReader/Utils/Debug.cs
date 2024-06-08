using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using SealedTask = System.Func<System.Threading.Tasks.Task<ComicReader.Utils.TaskException>, ComicReader.Utils.TaskException>;

namespace ComicReader.Utils;

internal class Debug
{
    private const string LogFileName = "log.txt";

    private static readonly TaskQueue LogQueue = new();

    private static StorageFolder LogFolder => ApplicationData.Current.LocalFolder;

    public static void Log(string content, bool verbose = true)
    {
        if (!Database.XmlDatabase.Settings.DebugMode)
        {
            return;
        }

        string timestamp = "[" + DateTimeOffset.Now.ToString("G") + "]";
        content = timestamp + " " + content;

        if (verbose)
        {
            System.Diagnostics.Debug.Print(content);
        }

        LogQueue.Enqueue(LogToFileSealed(content));
    }

    public static async Task<TaskException> TryAsync(string eventName, Func<Task> action)
    {
#if DEBUG
        await action();
#else
        try
        {
            await action();
        }
        catch (Exception e)
        {
            LogException(eventName, e);
            return TaskException.Failure;
        }
#endif
        return TaskException.Success;
    }

    public static void LogException(string eventName, Exception e)
    {
        var properties = new Dictionary<string, string>();
        if (e != null)
        {
            properties["detail"] = e.ToString();
        }

        Log("[Exception] " + eventName + ":\n" + StringUtils.DictionaryToString(properties));
    }

    private static SealedTask LogToFileSealed(string content)
    {
        return (Task<TaskException> _) => LogToFile(content).Result;
    }

    private static async Task<TaskException> LogToFile(string content)
    {
        StorageFile log_file;
        try
        {
            log_file = await LogFolder.CreateFileAsync(LogFileName, CreationCollisionOption.OpenIfExists);
        }
        catch (Exception)
        {
            return TaskException.Failure;
        }

        await FileIO.AppendTextAsync(log_file, content + "\n");
        return TaskException.Success;
    }
}
