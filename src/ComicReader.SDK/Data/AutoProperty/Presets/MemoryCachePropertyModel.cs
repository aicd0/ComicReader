// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty.Presets;

public class MemoryCachePropertyModel<T>
{
    internal Dictionary<string, CacheItem> cacheItems = [];
    internal Dictionary<long, CacheItem> requests = [];

    internal class CacheItem(string key)
    {
        public readonly string key = key;
        public int readVersion = 0;
        public PropertyResponseContent<T>? readResponse = null;
        public PropertyResponseContent<T>? writeResponse = null;
        public Queue<SealedPropertyRequest<T>> requests = [];
    }
}
