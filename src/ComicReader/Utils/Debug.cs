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
        private static readonly Utils.TaskQueue LogQueue = Utils.TaskQueueManager.EmptyQueue();

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

            Utils.TaskQueueManager.AppendTask(LogSealed(content), "", LogQueue);
        }

        private static SealedTask LogSealed(string content)
        {
            return (RawTask _) => _Log(content).Result;
        }

        private static async RawTask _Log(string content)
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
