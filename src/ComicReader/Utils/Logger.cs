using System;
using System.Threading.Tasks;
using Windows.Storage;
using SealedTask = System.Func<System.Threading.Tasks.Task<ComicReader.Utils.TaskException>, ComicReader.Utils.TaskException>;

namespace ComicReader.Utils;

internal static class Logger
{
    private const int LEVEL_DEBUG = 0;
    private const int LEVEL_INFO = 1;
    private const int LEVEL_WARN = 2;
    private const int LEVEL_ERROR = 3;
    private const int LEVEL_FATAL = 4;

    private const string LogFileName = "log.txt";

    private static readonly TaskQueue LogQueue = new();

    private static StorageFolder LogFolder => ApplicationData.Current.LocalFolder;

    public static void D(string tag, string message)
    {
        Log(LEVEL_DEBUG, tag, message, null);
    }

    public static void I(string tag, string message)
    {
        Log(LEVEL_INFO, tag, message, null);
    }

    public static void W(string tag, string message)
    {
        Log(LEVEL_WARN, tag, message, null);
    }

    public static void W(string tag, string message, Exception exception)
    {
        Log(LEVEL_WARN, tag, message, exception);
    }

    public static void E(string tag, string message)
    {
        Log(LEVEL_ERROR, tag, message, null);
    }

    public static void E(string tag, string message, Exception exception)
    {
        Log(LEVEL_ERROR, tag, message, exception);
    }

    public static void F(string tag, string message)
    {
        Log(LEVEL_FATAL, tag, message, null);
    }

    public static void F(string tag, string message, Exception exception)
    {
        Log(LEVEL_FATAL, tag, message, exception);
    }

    private static void Log(int level, string tag, string message, Exception exception)
    {
        if (!Database.XmlDatabase.Settings.DebugMode)
        {
            return;
        }

        string levelTag;
        switch (level)
        {
            case LEVEL_DEBUG:
                levelTag = "D";
                break;
            case LEVEL_INFO:
                levelTag = "I";
                break;
            case LEVEL_WARN:
                levelTag = "W";
                break;
            case LEVEL_ERROR:
                levelTag = "E";
                break;
            case LEVEL_FATAL:
                levelTag = "F";
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                levelTag = "U";
                break;
        }
        string realMessage = $"{DateTimeOffset.Now.ToString("G")} [{levelTag}/{tag}] {message}";
        if (exception != null)
        {
            realMessage += "\n" + exception.ToString();
        }

#if DEBUG
        System.Diagnostics.Debug.Print(realMessage);
        if (level >= LEVEL_FATAL)
        {
            throw new Exception(message, exception);
        }
#endif
        LogQueue.Enqueue(LogToFileSealed(realMessage));
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
