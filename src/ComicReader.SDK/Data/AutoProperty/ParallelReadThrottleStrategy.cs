// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.SDK.Data.AutoProperty;

internal class ParallelReadThrottleStrategy<T> : IRequestThrottleStrategy<T> where T : IReadonlyExternalRequest
{
    private readonly Queue<T> _requests = [];
    private readonly Dictionary<string, KeyInfo> _keys = [];

    public void Enqueue(T request)
    {
        _requests.Enqueue(request);
    }

    public bool TryDequeue([MaybeNullWhen(false)] out T request)
    {
        if (!_requests.TryPeek(out request))
        {
            request = default;
            return false;
        }
        string key = request.Key;
        if (!_keys.TryGetValue(key, out KeyInfo? keyInfo))
        {
            keyInfo = new();
            _keys.Add(key, keyInfo);
        }
        if (keyInfo.DequeuedRequests.Count > 0 && (request.Type != RequestType.Read || (keyInfo.LastRequestType != null && keyInfo.LastRequestType != RequestType.Read)))
        {
            request = default;
            return false;
        }
        keyInfo.LastRequestType = request.Type;
        keyInfo.DequeuedRequests.Add(request);
        _requests.Dequeue();
        return true;
    }

    public void OnRequestCompleted(T request)
    {
        string key = request.Key;
        if (!_keys.TryGetValue(key, out KeyInfo? keyInfo))
        {
            Logger.AssertNotReachHere("15C6EE5F82EB0A1F");
            return;
        }
        keyInfo.DequeuedRequests.Remove(request);
        if (keyInfo.DequeuedRequests.Count == 0)
        {
            _keys.Remove(key);
        }
    }

    private class KeyInfo
    {
        public RequestType? LastRequestType { get; set; }
        public HashSet<T> DequeuedRequests { get; } = [];
    }
}
