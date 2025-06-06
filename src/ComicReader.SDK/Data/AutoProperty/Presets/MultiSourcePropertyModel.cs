// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty.Presets;

public class MultiSourcePropertyModel<K, V> where K : IRequestKey
{
    internal Dictionary<K, CacheItem> cacheItems = [];
    internal Dictionary<long, RequestItem> requests = [];

    internal class CacheItem
    {
        public List<long> pendingRequests = [];
        public int readIndex = 0;
    }

    internal class RequestItem(OriginalRequestItem originalRequest, CacheItem cache)
    {
        public readonly OriginalRequestItem originalRequest = originalRequest;
        public readonly CacheItem cache = cache;
    }

    internal class OriginalRequestItem(SealedPropertyRequest<K, V> request)
    {
        public readonly SealedPropertyRequest<K, V> request = request;
        public bool responded = false;
        public int requesting = 0;
        public List<PropertyResponseContent<V>> responses = [];
    }
}
