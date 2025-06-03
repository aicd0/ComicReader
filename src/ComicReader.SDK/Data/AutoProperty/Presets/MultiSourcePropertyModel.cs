// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty.Presets;

public class MultiSourcePropertyModel<T>
{
    internal Dictionary<string, CacheItem> cacheItems = [];
    internal Dictionary<long, CacheItem> requests = [];

    internal class CacheItem
    {
        public int readIndex = 0;
        public int writeIndex = -1;
        public PropertyResponseContent<T>? readResponse = null;
        public PropertyResponseContent<T>? writeResponse = null;
        public Queue<SealedPropertyRequest<T>> requests = [];
    }
}
