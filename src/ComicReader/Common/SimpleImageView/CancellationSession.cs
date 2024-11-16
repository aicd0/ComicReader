// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.Common.SimpleImageView;

internal sealed class CancellationSession
{
    private readonly object _lock = new();
    private SessionToken _token = new();

    public SessionToken Token => _token;

    public SessionToken Next()
    {
        lock (_lock)
        {
            _token.Cancel();
            _token = new SessionToken();
            return _token;
        }
    }

    public sealed class SessionToken
    {
        private bool _cancelled = false;

        public bool IsCancellationRequested => _cancelled;

        public void Cancel()
        {
            _cancelled = true;
        }
    }
}
