// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

using ComicReader.Common.DebugTools;

using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ComicReader.Common.Caching;

internal class LRUCache
{
    private const string TAG = nameof(LRUCache);

    private readonly StorageFolder _folder;

    public LRUCache(StorageFolder folder)
    {
        _folder = folder;
    }

    public async Task<ILRUInputStream> PutAsync(string key)
    {
        StorageFile file = await _folder.CreateFileAsync(key, CreationCollisionOption.ReplaceExisting);

        IRandomAccessStream stream = null;
        try
        {
            stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        }
        catch (Exception e)
        {
            Logger.F(TAG, "PutAsync", e);
        }
        if (stream == null)
        {
            return null;
        }

        return new LRUInputStream(stream);
    }

    public async Task<ILRUOutputStream> GetAsync(string key)
    {
        IStorageItem item = await _folder.TryGetItemAsync(key);
        if (item is not StorageFile)
        {
            return null;
        }
        var file = item as StorageFile;

        IRandomAccessStream stream = null;
        try
        {
            stream = await file.OpenAsync(FileAccessMode.Read);
        }
        catch (Exception e)
        {
            Logger.F(TAG, "GetAsync", e);
        }
        if (stream == null)
        {
            return null;
        }

        return new LRUOutputStream(stream);
    }

    private class LRUInputStream(IRandomAccessStream stream) : ILRUInputStream
    {
        public void Dispose()
        {
            stream.Dispose();
        }

        public async Task WriteAsync(IBuffer buffer)
        {
            await stream.WriteAsync(buffer);
        }
    }

    private class LRUOutputStream(IRandomAccessStream stream) : ILRUOutputStream
    {
        public bool CanRead => stream.CanRead;

        public bool CanWrite => stream.CanWrite;

        public ulong Position => stream.Position;

        public ulong Size
        {
            get => stream.Size;
            set => stream.Size = value;
        }

        public IRandomAccessStream CloneStream()
        {
            return stream.CloneStream();
        }

        public void Dispose()
        {
            stream.Dispose();
        }

        public IAsyncOperation<bool> FlushAsync()
        {
            return stream.FlushAsync();
        }

        public IInputStream GetInputStreamAt(ulong position)
        {
            return stream.GetInputStreamAt(position);
        }

        public IOutputStream GetOutputStreamAt(ulong position)
        {
            return stream.GetOutputStreamAt(position);
        }

        public IAsyncOperationWithProgress<IBuffer, uint> ReadAsync(IBuffer buffer, uint count, InputStreamOptions options)
        {
            return stream.ReadAsync(buffer, count, options);
        }

        public void Seek(ulong position)
        {
            stream.Seek(position);
        }

        public IAsyncOperationWithProgress<uint, uint> WriteAsync(IBuffer buffer)
        {
            return stream.WriteAsync(buffer);
        }
    }
}
