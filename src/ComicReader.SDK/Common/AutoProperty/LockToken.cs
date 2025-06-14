// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace ComicReader.SDK.Common.AutoProperty;

public class LockToken
{
    private readonly LockManager _lockManager;

    internal int Id { get; }
    public bool CanRelease { get; private set; }

    internal LockToken(LockManager lockManager, int id, bool canRelease)
    {
        _lockManager = lockManager;
        Id = id;
        CanRelease = canRelease;
    }

    public LockToken Readonly()
    {
        return new(_lockManager, Id, false);
    }

    public void Release()
    {
        if (!CanRelease)
        {
            throw new InvalidOperationException("This lock token cannot be released.");
        }
        _lockManager.ReleaseLock(this);
    }

    public bool TryAcquire(LockResource resource, [MaybeNullWhen(false)] out LockToken token)
    {
        return _lockManager.TryAcquireLock(this, resource, out token);
    }
}
