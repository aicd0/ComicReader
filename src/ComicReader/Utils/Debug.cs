using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ComicReader.Utils
{
    using RawTask = Task<Utils.TaskResult>;
    using SealedTask = Func<Task<Utils.TaskResult>, Utils.TaskResult>;

    class Debug
    {
        private const string LogFileName = "log.txt";

        private static StorageFolder LogFolder => ApplicationData.Current.LocalFolder;
        private static StorageFile LogFile = null;
        private static readonly Utils.TaskQueue LogQueue = Utils.TaskQueueManager.EmptyQueue();

        public static void Log(string content, bool verbose = true)
        {
            if (!Database.XmlDatabase.Settings.DebugMode)
            {
                return;
            }

            Utils.TaskQueueManager.AppendTask(LogSealed(content, verbose), "", LogQueue);
        }

        private static SealedTask LogSealed(string content, bool verbose)
        {
            return (RawTask _) => _Log(content, verbose).Result;
        }

        private static async RawTask _Log(string content, bool verbose)
        {
            if (LogFile == null)
            {
                try
                {
                    LogFile = await LogFolder.CreateFileAsync(LogFileName, CreationCollisionOption.OpenIfExists);
                }
                catch (Exception)
                {
                    return new TaskResult(TaskException.Failure);
                }
            }

            string timestamp = "[" + DateTimeOffset.Now.ToString("G") + "]";
            content += "\n";
            await FileIO.AppendTextAsync(LogFile, timestamp + " " + content);

            if (verbose)
            {
                System.Diagnostics.Debug.Print(content);
            }

            return new TaskResult();
        }
    }
}
