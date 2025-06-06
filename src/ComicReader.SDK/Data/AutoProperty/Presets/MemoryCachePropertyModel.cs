// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty.Presets;

public class MemoryCachePropertyModel<K, V> where K : IRequestKey
{
    internal Dictionary<K, CacheItem> cacheItems = [];
    internal Dictionary<long, RequestItem> requests = [];

    internal class CacheItem
    {
        public PropertyResponseContent<V>? response = null;
        public List<long> pendingRequests = [];
    }

    internal class RequestItem(SealedPropertyRequest<K, V> originalRequest, CacheItem cache)
    {
        public readonly SealedPropertyRequest<K, V> originalRequest = originalRequest;
        public readonly CacheItem cache = cache;
    }
}
