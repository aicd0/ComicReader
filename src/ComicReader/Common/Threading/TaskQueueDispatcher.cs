// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

namespace ComicReader.Common.Threading;

internal sealed class TaskQueueDispatcher : IDispatcher
{
    private readonly TaskQueue _taskQueue;
    private readonly string _debugDescription;

    public TaskQueueDispatcher(TaskQueue queue, string debugDescription)
    {
        _taskQueue = queue;
        _debugDescription = debugDescription;
    }

    public void Submit(Action action, string debugDescription)
    {
        string taskName = $"{_debugDescription}_{debugDescription}";
        _taskQueue.Enqueue(taskName, delegate
        {
            action();
            return TaskException.Success;
        });
    }
}
