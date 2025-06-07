// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace ComicReader.SDK.Data.AutoProperty;

internal class LockManager
{
    private readonly LockInfo _rootLock = new();
    private readonly Dictionary<int, LockResource> _lockResources = [];
    private int _nextTokenId = 0;

    public bool ServerInvalidated { get; set; } = false; // for server use

    /// <summary>
    /// Attempts to acquire a lock on the specified <paramref name="resource"/>.
    /// If the lock can be acquired (i.e., no conflicting locks exist), a new <see cref="LockToken"/> is returned via the <paramref name="token"/> out parameter.
    /// Otherwise, <paramref name="token"/> is set to <c>null</c> and the method returns <c>false</c>.
    /// </summary>
    /// <param name="resource">The resource to lock. This object will be cloned internally.</param>
    /// <param name="token">
    /// When this method returns, contains the <see cref="LockToken"/> representing the acquired lock if successful; otherwise, <c>null</c>.
    /// </param>
    /// <returns>
    /// <c>true</c> if the lock was successfully acquired; otherwise, <c>false</c>.
    /// </returns>
    public bool TryAcquireLock(LockResource resource, [MaybeNullWhen(false)] out LockToken token)
    {
        resource = resource.Clone();
        if (!CanAcquireLock(_rootLock, resource))
        {
            token = null;
            return false;
        }
        int newTokenId = _nextTokenId++;
        token = new(this, newTokenId, true);
        AcquireLock(_rootLock, token, resource);
        _lockResources[newTokenId] = resource;
        return true;
    }

    /// <summary>
    /// Attempts to acquire a lock on the specified <paramref name="resource"/> using an existing <paramref name="baseToken"/>.
    /// This is typically used for lock downgrades or nested locking scenarios.
    /// If the lock can be acquired, a new <see cref="LockToken"/> is returned via the <paramref name="token"/> out parameter.
    /// Otherwise, <paramref name="token"/> is set to <c>null</c> and the method returns <c>false</c>.
    /// </summary>
    /// <param name="baseToken">The existing lock token to use as the base for acquiring the lock.</param>
    /// <param name="resource">The resource to lock. This object will be cloned internally.</param>
    /// <param name="token">
    /// When this method returns, contains the <see cref="LockToken"/> representing the acquired lock if successful; otherwise, <c>null</c>.
    /// </param>
    /// <returns>
    /// <c>true</c> if the lock was successfully acquired; otherwise, <c>false</c>.
    /// </returns>
    public bool TryAcquireLock(LockToken baseToken, LockResource resource, [MaybeNullWhen(false)] out LockToken token)
    {
        resource = resource.Clone();
        if (!CanAcquireLock(_rootLock, baseToken, resource))
        {
            token = null;
            return false;
        }
        int newTokenId = _nextTokenId++;
        token = new(this, newTokenId, true);
        AcquireLock(_rootLock, token, resource);
        _lockResources[newTokenId] = resource;
        return true;
    }

    /// <summary>
    /// Releases the lock associated with the specified <paramref name="token"/>.
    /// If the token is not found or no lock is associated, the method does nothing.
    /// </summary>
    /// <param name="token">The lock token representing the lock to release.</param>
    public void ReleaseLock(LockToken token)
    {
        if (!token.CanRelease)
        {
            throw new InvalidOperationException("This lock token cannot be released.");
        }
        if (!_lockResources.Remove(token.Id, out LockResource? resource))
        {
            return;
        }
        ReleaseLock(_rootLock, token, resource);
        ServerInvalidated = true;
    }

    private bool CanAcquireLock(LockInfo lockInfo, LockResource resource)
    {
        if (resource.Type != LockType.None)
        {
            if (resource.Type == LockType.Write)
            {
                if (lockInfo.Tokens.Count > 0)
                {
                    return false;
                }
            }
            else if (resource.Type == LockType.Read)
            {
                if (lockInfo.Tokens.Values.Any(t => t == LockType.Write))
                {
                    return false;
                }
            }
        }
        foreach (KeyValuePair<string, LockResource> child in resource.Children)
        {
            if (lockInfo.Children.TryGetValue(child.Key, out LockInfo? childLockInfo))
            {
                if (!CanAcquireLock(childLockInfo, child.Value))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private bool CanAcquireLock(LockInfo lockInfo, LockToken token, LockResource resource)
    {
        if (resource.Type != LockType.None)
        {
            if (!lockInfo.Tokens.TryGetValue(token.Id, out LockType tokenType))
            {
                return false;
            }
            if (resource.Type == LockType.Write && tokenType == LockType.Read)
            {
                return false;
            }
        }
        foreach (KeyValuePair<string, LockResource> child in resource.Children)
        {
            if (!lockInfo.Children.TryGetValue(child.Key, out LockInfo? childLockInfo))
            {
                return false;
            }
            if (!CanAcquireLock(childLockInfo, token, child.Value))
            {
                return false;
            }
        }
        return true;
    }

    private void AcquireLock(LockInfo lockInfo, LockToken token, LockResource resource)
    {
        if (resource.Type != LockType.None)
        {
            lockInfo.Tokens[token.Id] = resource.Type;
        }
        foreach (KeyValuePair<string, LockResource> child in resource.Children)
        {
            if (!lockInfo.Children.TryGetValue(child.Key, out LockInfo? childLockInfo))
            {
                childLockInfo = new LockInfo();
                lockInfo.Children[child.Key] = childLockInfo;
            }
            AcquireLock(childLockInfo, token, child.Value);
        }
    }

    private void ReleaseLock(LockInfo lockInfo, LockToken token, LockResource resource)
    {
        if (resource.Type != LockType.None)
        {
            lockInfo.Tokens.Remove(token.Id);
        }
        foreach (KeyValuePair<string, LockResource> child in resource.Children)
        {
            if (lockInfo.Children.TryGetValue(child.Key, out LockInfo? childLockInfo))
            {
                ReleaseLock(childLockInfo, token, child.Value);
                if (childLockInfo.Tokens.Count == 0 && childLockInfo.Children.Count == 0)
                {
                    lockInfo.Children.Remove(child.Key);
                }
            }
        }
    }

    private class LockInfo
    {
        public Dictionary<int, LockType> Tokens = [];
        public Dictionary<string, LockInfo> Children = [];
    }
}
