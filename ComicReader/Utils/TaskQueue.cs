﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Xaml.Controls;

namespace ComicReader.Utils
{
    namespace TaskQueue
    {
        public enum TaskException
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

        public class TaskResult
        {
            public TaskResult(TaskException type = TaskException.Success,
                bool fatal = false, string desc = "No description provided.")
            {
                ExceptionType = type;
                IsFatal = fatal;
                Description = desc;
            }

            public TaskException ExceptionType;
            public bool IsFatal = false;
            public string Description;
        }

        public class TaskQueue
        {
            public Task<TaskResult> Queue;
        }

        public class TaskQueueManager
        {
            private static TaskQueue m_default_queue = EmptyQueue();
            private static int m_next_token = 0;
            private static SortedDictionary<int, string> m_task_prompts =
                new SortedDictionary<int, string>();
            private static SemaphoreSlim m_append_task_semaphore =
                new SemaphoreSlim(1);

            public static TaskQueue EmptyQueue()
            {
                return new TaskQueue
                {
                    Queue = Task.Factory.StartNew(() => new TaskResult())
                };
            }

            public static void AppendTask(Func<Task<TaskResult>, TaskResult> ope)
            {
                AppendTask(ope, "");
            }

            public static void AppendTask(Func<Task<TaskResult>, TaskResult> ope,
                string prompt)
            {
                AppendTask(ope, prompt, m_default_queue);
            }

            public static void AppendTask(Func<Task<TaskResult>, TaskResult> ope,
                string prompt, TaskQueue queue)
            {
                int token = 0;

                // enqueue
                queue.Queue = queue.Queue
                .ContinueWith(delegate (Task<TaskResult> _t)
                {
                    m_append_task_semaphore.Wait();

                    if (prompt.Length != 0)
                    {
                    // generate a token and add the prompt to m_task_prompts
                    if (m_task_prompts.Count == 0)
                        {
                            m_next_token = 0;
                        }

                        token = m_next_token--;
                        m_task_prompts.Add(token, prompt);
                    }

                    // fetch the first prompt from all prompts and set as current
                    // prompt
                    string first_prompt = "";

                    foreach (KeyValuePair<int, string> p in m_task_prompts)
                    {
                        first_prompt = p.Value;
                        break;
                    }

                    SetPromptText(first_prompt).Wait();
                    _ = m_append_task_semaphore.Release();
                    return _t.Result;
                })
                .ContinueWith(ope)
                .ContinueWith(
                delegate (Task<TaskResult> _t)
                {
                    m_append_task_semaphore.Wait();

                    // remove last prompt
                    if (prompt.Length != 0)
                    {
                        m_task_prompts.Remove(token);
                    }

                    // update current prompt
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
                        Task show_dialog_task = null;

                        Methods.Sync(
                        delegate
                        {
                            ContentDialog dialog = new ContentDialog()
                            {
                                Title = "Task failed",
                                Content = "We have encountered a fatal error and" +
                                " need to terminate this application. You can send" +
                                " us the following information to help us locate" +
                                " the issues.\n\n" +
                                "Type: " + _t.Result.ExceptionType.ToString() + "\n" +
                                "Description: " + _t.Result.Description,
                                CloseButtonText = "Continue"
                            };

                            show_dialog_task = dialog.ShowAsync().AsTask();
                        }).Wait();

                        show_dialog_task.Wait();
                        CoreApplication.Exit();
                    }
                    return _t.Result;
                });
            }

            private static async Task<TaskResult> SetPromptText(string text)
            {
                if (Views.RootPage.Current == null)
                {
                    return new TaskResult(TaskException.Failure);
                }

                await Methods.Sync(
                delegate
                {
                    Views.RootPage.Current.SetRootToolTip(text);
                });

                return new TaskResult();
            }
        };
    }
}