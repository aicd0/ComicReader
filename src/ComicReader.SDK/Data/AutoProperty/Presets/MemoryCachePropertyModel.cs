// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty.Presets;

public class MemoryCachePropertyModel<T>
{
    public Dictionary<string, CacheItem> cacheItems = [];
    public Dictionary<long, CacheItem> requests = [];

    public class CacheItem
    {
        public PropertyResponseContent<T>? readResponse = null;
        public PropertyResponseContent<T>? writeResponse = null;
        public Queue<ServerPropertyRequest<T>> requests = [];
    }
}
