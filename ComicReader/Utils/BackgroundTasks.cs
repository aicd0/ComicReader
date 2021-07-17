using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

namespace ComicReader.Utils
{
    public enum BackgroundTaskExceptionType
    {
        Success,
        Failure,
        Cancellation,
        FileNotExists,
        ItemExists,
        InvalidParameters,
        NameCollision,
        NoPermission,
    }

    public class BackgroundTaskResult
    {
        public BackgroundTaskResult(BackgroundTaskExceptionType type = BackgroundTaskExceptionType.Success,
            bool fatal = false, string description = "No description provided")
        {
            ExceptionType = type;
            IsFatal = fatal;
            Description = description;
        }

        public BackgroundTaskExceptionType ExceptionType;
        public bool IsFatal = false;
        public string Description;
    }

    public class BackgroundTaskQueue
    {
        public Task<BackgroundTaskResult> Queue;
    }

    public class BackgroundTasks
    {
        private static BackgroundTaskQueue m_default_queue = EmptyQueue();
        private static int m_next_token = 0;
        private static SortedDictionary<int, string> m_task_prompts = new SortedDictionary<int, string>();
        private static SemaphoreSlim m_append_task_semaphore = new SemaphoreSlim(1);

        public static BackgroundTaskQueue EmptyQueue()
        {
            return new BackgroundTaskQueue
            {
                Queue = Task.Factory.StartNew(() => new BackgroundTaskResult())
            };
        }

        public static void AppendTask(Func<Task<BackgroundTaskResult>, BackgroundTaskResult> ope)
        {
            AppendTask(ope, "");
        }

        public static void AppendTask(Func<Task<BackgroundTaskResult>, BackgroundTaskResult> ope, string prompt)
        {
            AppendTask(ope, prompt, m_default_queue);
        }

        public static void AppendTask(Func<Task<BackgroundTaskResult>, BackgroundTaskResult> ope, string prompt, BackgroundTaskQueue queue)
        {
            int token = 0;

            // enqueue
            queue.Queue = queue.Queue
            .ContinueWith(delegate (Task<BackgroundTaskResult> _t)
            {
                m_append_task_semaphore.Wait();
                // get token if prompt text has value
                if (prompt.Length != 0)
                {
                    if (m_task_prompts.Count == 0)
                    {
                        m_next_token = 0;
                    }

                    token = m_next_token--;
                    m_task_prompts.Add(token, prompt);
                }

                // update task prompt
                string text = "";
                foreach (KeyValuePair<int, string> p in m_task_prompts)
                {
                    text = p.Value;
                    break;
                }
                SetPromptText(text).Wait();
                _ = m_append_task_semaphore.Release();
                return _t.Result;
            })
            .ContinueWith(ope)
            .ContinueWith(
            delegate (Task<BackgroundTaskResult> _t)
            {
                m_append_task_semaphore.Wait();
                // remove prompt
                if (prompt.Length != 0)
                {
                    m_task_prompts.Remove(token);
                }

                // update task prompt
                string text = "";
                foreach (KeyValuePair<int, string> p in m_task_prompts)
                {
                    text = p.Value;
                    break;
                }
                SetPromptText(text).Wait();
                _ = m_append_task_semaphore.Release();

                // show error dialog if a fatal error is encountered
                if (_t.Result.IsFatal)
                {
                    Task task = null;
                    CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    delegate
                    {
                        ContentDialog dialog = new ContentDialog()
                        {
                            Title = "Task failed",
                            Content =
                            "We have encountered a fatal error and need to terminate this application. " +
                            "You can send us the following information to help us locate the issues.\n\n" +
                            "Type: " + _t.Result.ExceptionType.ToString() + "\n" +
                            "Description: " + _t.Result.Description,
                            CloseButtonText = "Continue"
                        };
                        task = dialog.ShowAsync().AsTask();
                    }).AsTask().Wait();
                    task.Wait();
                    CoreApplication.Exit();
                }
                return _t.Result;
            });
        }

        private static async Task<BackgroundTaskResult> SetPromptText(string text)
        {
            if (Views.RootPage.Current == null)
            {
                return new BackgroundTaskResult(BackgroundTaskExceptionType.Failure);
            }

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            delegate
            {
                Views.RootPage.Current.SetRootToolTip(text);
            });

            return new BackgroundTaskResult();
        }
    };
}
