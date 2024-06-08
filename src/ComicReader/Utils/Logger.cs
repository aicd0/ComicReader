using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace ComicReader.Utils;

internal static class Logger
{
    private const string TAG = "Logger";
    private const int LEVEL_CONSOLE = 0;
    private const int LEVEL_DEBUG = 1;
    private const int LEVEL_INFO = 2;
    private const int LEVEL_WARN = 3;
    private const int LEVEL_ERROR = 4;
    private const int LEVEL_FATAL = 5;
    private const int LOG_INTERVAL = 5000;

    private static int sInitialized = 0;
    private static readonly ConcurrentQueue<string> sBuffer = new();

    public static void Initialize()
    {
        if (Interlocked.CompareExchange(ref sInitialized, 1, 0) != 0)
        {
            F(TAG, "Multiple initializations");
            return;
        }

        C0.Run(async delegate
        {
            Console("Logger initialized");
            while (true)
            {
                if (sBuffer.Count > 0)
                {
                    await FlushToFile();
                }

                await Task.Delay(LOG_INTERVAL);
            }
        });
    }

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

    private static void Console(string message)
    {
        Log(LEVEL_CONSOLE, TAG, message, null);
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
            case LEVEL_CONSOLE:
                levelTag = "C";
                break;
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
                FailOnDebug();
                levelTag = "U";
                break;
        }
        string realMessage = $"{DateTimeOffset.Now.ToString("G")} [{levelTag}/{tag}] {message}";
        if (exception != null)
        {
            realMessage += "\n" + exception.ToString();
        }

#if DEBUG
        LogToConsole(realMessage);
        if (level >= LEVEL_FATAL)
        {
            FailOnDebug();
        }
#endif
        if (level >= LEVEL_INFO)
        {
            LogToFile(realMessage);
        }
    }

    private static void LogToFile(string message)
    {
        sBuffer.Enqueue(message);
    }

    private static async Task FlushToFile()
    {
        StringBuilder sb = new();
        int logCount = 0;
        while (true)
        {
            if (sBuffer.TryDequeue(out string msg))
            {
                sb.Append(msg);
                sb.Append('\n');
                logCount++;
            }
            else
            {
                break;
            }
        }
        string content = sb.ToString();

        DateTime dateTime = DateTime.Now;
        string fileName = "log_" + dateTime.ToString("yyyyMMdd") + ".txt";
        StorageFile logFile;
        try
        {
            StorageFolder logDir = await ApplicationData.Current.LocalCacheFolder.CreateFolderAsync("logs", CreationCollisionOption.OpenIfExists);
            logFile = await logDir.CreateFileAsync(fileName, CreationCollisionOption.OpenIfExists);
            await FileIO.AppendTextAsync(logFile, content);
        }
        catch (Exception e)
        {
            F(TAG, e.ToString());
            return;
        }
        Console($"flushed {logCount} logs to {logFile.Path}");
    }

    private static void LogToConsole(string message)
    {
        System.Diagnostics.Debug.Print(message);
    }

    private static void FailOnDebug()
    {
        System.Diagnostics.Debug.Assert(false);
    }
}
