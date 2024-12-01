// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace ComicReader.Common.Flow;

internal class FlowScope
{
    public static readonly FlowScope GlobalScope = new();

    private readonly ReaderWriterLock _commitLock = new();
    private readonly ConcurrentDictionary<ISyncHandler, bool> _handlers = [];

    public void AddSyncHandler(ISyncHandler handler)
    {
        _handlers[handler] = true;
    }

    public void RemoveSyncHandler(ISyncHandler handler)
    {
        _handlers.TryRemove(handler, out _);
    }

    public void AcquireCommitLock()
    {
        _commitLock.AcquireReaderLock(-1);
    }

    public void ReleaseCommitLock()
    {
        _commitLock.ReleaseReaderLock();
    }

    public void Commit()
    {
        List<ISyncHandler> handlers = [];

        _commitLock.AcquireWriterLock(-1);
        try
        {
            foreach (ISyncHandler handler in _handlers.Keys)
            {
                handlers.Add(handler);
                handler.BeforeCommit();
            }
            _handlers.Clear();
        }
        finally
        {
            _commitLock.ReleaseWriterLock();
        }

        foreach (ISyncHandler handler in handlers)
        {
            handler.OnCommit();
        }
    }

    public interface ISyncHandler
    {
        void BeforeCommit();

        void OnCommit();
    }
}
