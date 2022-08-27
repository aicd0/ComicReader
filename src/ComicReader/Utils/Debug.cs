using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace ComicReader.Utils
{
    using RawTask = Task<Utils.TaskResult>;
    using SealedTask = Func<Task<Utils.TaskResult>, Utils.TaskResult>;

    class Debug
    {
        private const string LogFileName = "log.txt";

        public enum Module {
            CreateCoverCache
        }

        private static readonly Utils.TaskQueue LogQueue = Utils.TaskQueueManager.EmptyQueue();

        private static StorageFolder LogFolder => ApplicationData.Current.LocalFolder;

        public static void Log(string content, bool verbose = true)
        {
            if (!Database.XmlDatabase.Settings.DebugMode)
            {
                return;
            }

            string timestamp = "[" + DateTimeOffset.Now.ToString("G") + "]";
            content = timestamp + " " + content + "\n";

            if (verbose)
            {
                System.Diagnostics.Debug.Print(content);
            }

            Utils.TaskQueueManager.AppendTask(LogToFileSealed(content), "", LogQueue);
        }

        public static void LogException(Module module, Exception e)
        {
            string info = "Exception: " + module.ToString() + ": " + e.GetType().FullName;
            Log(info);
        }

        private static SealedTask LogToFileSealed(string content)
        {
            return (RawTask _) => LogToFile(content).Result;
        }

        private static async RawTask LogToFile(string content)
        {
            StorageFile log_file;

            try
            {
                log_file = await LogFolder.CreateFileAsync(LogFileName, CreationCollisionOption.OpenIfExists);
            }
            catch (Exception)
            {
                return new TaskResult(TaskException.Failure);
            }

            await FileIO.AppendTextAsync(log_file, content);
            return new TaskResult();
        }
    }
}
