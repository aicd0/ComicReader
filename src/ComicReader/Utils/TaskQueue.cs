using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Xaml.Controls;

namespace ComicReader.Utils
{
    public enum TaskException
    {
        Cancellation,
        EmptySet,
        Failure,
        FileCorrupted,
        FileNotFound,
        IncorrectPassword,
        InvalidParameters,
        ItemExists,
        MaximumExceeded,
        NameCollision,
        NoPermission,
        NotImplemented,
        NotSupported,
        StopIteration,
        Success,
        Unknown,
        UnknownEnum,
    }

    public class TaskQueue
    {
        public Task<TaskException> Queue;
    }

    public class TaskQueueManager
    {
        private static readonly TaskQueue s_defaultQueue = EmptyQueue();
        private static int s_nextToken = 0;
        private static readonly SortedDictionary<int, string> s_taskPrompts = new SortedDictionary<int, string>();
        private static readonly SemaphoreSlim s_appendTaskSemaphore = new SemaphoreSlim(1);

        public static TaskQueue EmptyQueue()
        {
            return new TaskQueue
            {
                Queue = Task.Factory.StartNew(() => TaskException.Success)
            };
        }

        public static void NewTask(Func<Task<TaskException>, TaskException> ope)
        {
            NewTask(ope, "");
        }

        public static void NewTask(Func<Task<TaskException>, TaskException> ope,
            string prompt)
        {
            AppendTask(ope, prompt, EmptyQueue());
        }

        public static void AppendTask(Func<Task<TaskException>, TaskException> ope)
        {
            AppendTask(ope, "");
        }

        public static void AppendTask(Func<Task<TaskException>, TaskException> ope,
            string prompt)
        {
            AppendTask(ope, prompt, s_defaultQueue);
        }

        public static void AppendTask(Func<Task<TaskException>, TaskException> ope,
            string prompt, TaskQueue queue)
        {
            int token = 0;

            // enqueue
            queue.Queue = queue.Queue
            .ContinueWith(delegate (Task<TaskException> _t)
            {
                s_appendTaskSemaphore.Wait();

                if (prompt.Length != 0)
                {
                    // generate a token and add the prompt to m_task_prompts
                    if (s_taskPrompts.Count == 0)
                    {
                        s_nextToken = 0;
                    }

                    token = s_nextToken--;
                    s_taskPrompts.Add(token, prompt);
                }

                // fetch the first prompt from all prompts and set as current
                // prompt
                string first_prompt = "";

                foreach (KeyValuePair<int, string> p in s_taskPrompts)
                {
                    first_prompt = p.Value;
                    break;
                }

                SetPromptText(first_prompt).Wait();
                _ = s_appendTaskSemaphore.Release();
                return _t.Result;
            })
            .ContinueWith(ope)
            .ContinueWith(delegate (Task<TaskException> _t)
            {
                s_appendTaskSemaphore.Wait();

                // remove last prompt
                if (prompt.Length != 0)
                {
                    s_taskPrompts.Remove(token);
                }

                // update current prompt
                string text = "";

                foreach (KeyValuePair<int, string> p in s_taskPrompts)
                {
                    text = p.Value;
                    break;
                }

                SetPromptText(text).Wait();
                _ = s_appendTaskSemaphore.Release();
                return _t.Result;
            });
        }

        private static async Task<TaskException> SetPromptText(string text)
        {
            if (Views.MainPage.Current == null)
            {
                return TaskException.Failure;
            }
            await Utils.C0.Sync(delegate
            {
                Views.MainPage.Current.SetRootToolTip(text);
            });
            return TaskException.Success;
        }
    };
}
