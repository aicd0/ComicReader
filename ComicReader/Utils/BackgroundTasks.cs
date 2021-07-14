using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

namespace ComicReader.Utils
{
    public class BackgroundTaskQueue
    {
        public Task<int> Queue;
    }

    public class BackgroundTasks
    {
        private static BackgroundTaskQueue default_queue = EmptyQueue();
        private static int next_token = 0;
        private static SortedDictionary<int, string> messages = new SortedDictionary<int, string>();
        private static SemaphoreSlim m_append_task_semaphore = new SemaphoreSlim(1);

        public static BackgroundTaskQueue EmptyQueue()
        {
            return new BackgroundTaskQueue
            {
                Queue = Task.Factory.StartNew(() =>
                {
                    return 0;
                })
            };
        }

        public static void AppendTask(Func<Task<int>, int> ope)
        {
            AppendTask(ope, "");
        }

        public static void AppendTask(Func<Task<int>, int> ope, string des)
        {
            AppendTask(ope, des, default_queue);
        }

        public static void AppendTask(Func<Task<int>, int> ope, string des, BackgroundTaskQueue queue)
        {
            Func<Task<int>, int> update_tip_text =
            delegate (Task<int> _t)
            {
                string text = "";
                foreach (var m in messages)
                {
                    text = m.Value;
                    break;
                }
                SetTipText(text).Wait();
                return _t.Result;
            };

            int token = 0;

            // enqueue
            queue.Queue = queue.Queue
            .ContinueWith(delegate (Task<int> _t)
            {
                m_append_task_semaphore.Wait();

                // get token if description text is not null
                if (des.Length != 0)
                {
                    if (messages.Count == 0)
                    {
                        next_token = 0;
                    }

                    token = next_token--;
                    messages.Add(token, des);
                }

                // update task description
                string text = "";
                foreach (var m in messages)
                {
                    text = m.Value;
                    break;
                }
                SetTipText(text).Wait();

                m_append_task_semaphore.Release();
                return _t.Result;
            })
            .ContinueWith(ope)
            .ContinueWith(
            delegate (Task<int> _t)
            {
                m_append_task_semaphore.Wait();

                // remove description
                if (des.Length != 0)
                {
                    messages.Remove(token);
                }

                // update task description
                string text = "";
                foreach (var m in messages)
                {
                    text = m.Value;
                    break;
                }
                SetTipText(text).Wait();

                m_append_task_semaphore.Release();

                // show error dialog if task is not completed successfully
                if (_t.Result != 0 && _t.Result != 1)
                {
                    Task task = null;
                    CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    delegate
                    {
                        ContentDialog dialog = new ContentDialog()
                        {
                            Title = "(Debug) Error encountered",
                            Content =
                            "Error code: " + _t.Result.ToString() + "\n" +
                            "Description: " + des,
                            CloseButtonText = "Exit"
                        };

                        task = dialog.ShowAsync().AsTask();
                    }).AsTask().Wait();
                    task.Wait();
                    CoreApplication.Exit();
                }

                return _t.Result;
            });
        }

        private static async Task<int> SetTipText(string text)
        {
            if (Views.RootPage.Current == null)
            {
                return 1;
            }

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            delegate
            {
                Views.RootPage.Current.SetRootToolTip(text);
            });

            return 0;
        }
    };
}
