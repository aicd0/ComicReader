﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ComicReader.Utils
{
    public class CancellationLock
    {
        private LinkedList<TaskCompletionSource> _queue = new();

        public async Task LockAsync(Action<Token> action)
        {
            await LockAsync(async delegate (Token token)
            {
                await Task.FromResult(0);
                action(token);
            });
        }

        public async Task<T> LockAsync<T>(Func<Token, T> action)
        {
            return await LockAsync(async delegate (Token token)
            {
                await Task.FromResult(0);
                return action(token);
            });
        }

        public async Task<T> LockAsync<T>(Func<Token, Task<T>> action)
        {
            var currentCompletionSource = new TaskCompletionSource();
            TaskCompletionSource priorCompletionSource = null;
            lock (_queue)
            {
                if (_queue.Count > 0)
                    priorCompletionSource = _queue.Last.Value;
                _queue.AddLast(currentCompletionSource);
            }

            if (priorCompletionSource != null)
                await priorCompletionSource.Task;

            var token = new Token(this);
            T result = await action(token);
            lock (_queue)
            {
                _queue.RemoveFirst();
            }

            currentCompletionSource.SetResult();
            return result;
        }

        public class Token
        {
            private CancellationLock _parent;

            public Token(CancellationLock parent)
            {
                _parent = parent;
            }

            public bool CancellationRequested => _parent._queue.Count > 1;
        }
    }
}
