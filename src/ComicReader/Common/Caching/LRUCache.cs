// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Threading;
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
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = [];

    public LRUCache(StorageFolder folder)
    {
        _folder = folder;
    }

    public ILRUInputStream Put(string key)
    {
        string hashedKey = ToHashedKey(key);
        CacheEntry entry = _entries.GetOrAdd(hashedKey, (string key) => new CacheEntry(this, key));
        return entry.StartWrite();
    }

    public ILRUOutputStream Get(string key)
    {
        string hashedKey = ToHashedKey(key);
        CacheEntry entry = _entries.GetOrAdd(hashedKey, (string key) => new CacheEntry(this, key));
        return entry.StartRead();
    }

    private string ToHashedKey(string key)
    {
        return key;
    }

    private static string GetDirtyFileName(string key)
    {
        return "0." + key;
    }

    private static string GetCleanFileName(string key)
    {
        return "1." + key;
    }

    private class CacheEntry : IDisposable
    {
        private readonly ReaderWriterLock _lock = new();
        private readonly LRUCache _cache;
        private readonly string _key;
        private Status _status;
        private int _readerCount = 0;
        private LRUOutputStream _outputStream;

        public CacheEntry(LRUCache cache, string key)
        {
            _cache = cache;
            _key = key;
            _status = Status.Empty;
        }

        public ILRUInputStream StartWrite()
        {
            _lock.AcquireWriterLock(-1);
            try
            {
                if (_status == Status.Dirty || _readerCount > 0)
                {
                    return null;
                }
                _status = Status.Dirty;
            }
            finally
            {
                _lock.ReleaseWriterLock();
            }

            string dirtyFileName = GetDirtyFileName(_key);
            StorageFile file = null;
            try
            {
                file = _cache._folder.CreateFileAsync(dirtyFileName, CreationCollisionOption.ReplaceExisting).AsTask().Result;
            }
            catch (Exception e)
            {
                Logger.F(TAG, "StartWrite", e);
            }
            if (file == null)
            {
                SwitchToEmptyState();
                return null;
            }

            IRandomAccessStream stream = null;
            try
            {
                stream = file.OpenAsync(FileAccessMode.ReadWrite).AsTask().Result;
            }
            catch (Exception e)
            {
                Logger.F(TAG, "StartWrite", e);
            }
            if (stream == null)
            {
                SwitchToEmptyState();
                return null;
            }

            return new LRUInputStream(this, stream);
        }

        public void EndWrite()
        {
            string dirtyFileName = GetDirtyFileName(_key);
            StorageFile file = null;
            try
            {
                file = _cache._folder.GetFileAsync(dirtyFileName).AsTask().Result;
            }
            catch (Exception e)
            {
                Logger.F(TAG, "EndWrite", e);
            }
            if (file == null)
            {
                SwitchToEmptyState();
                return;
            }

            string cleanFileName = GetCleanFileName(_key);
            try
            {
                file.RenameAsync(cleanFileName).AsTask().Wait();
            }
            catch (Exception e)
            {
                Logger.F(TAG, "EndWrite", e);
                SwitchToEmptyState();
                return;
            }

            _lock.AcquireWriterLock(-1);
            try
            {
                DebugUtils.Assert(_status == Status.Dirty);
                _status = Status.Clean;
            }
            finally
            {
                _lock.ReleaseWriterLock();
            }
        }

        public ILRUOutputStream StartRead()
        {
            _lock.AcquireWriterLock(-1);
            try
            {
                if (_status == Status.Dirty)
                {
                    return null;
                }

                if (_outputStream == null)
                {
                    string cleanFileName = GetCleanFileName(_key);
                    StorageFile file = null;
                    try
                    {
                        file = _cache._folder.GetFileAsync(cleanFileName).AsTask().Result;
                    }
                    catch (Exception e)
                    {
                        Logger.F(TAG, "StartRead", e);
                    }
                    if (file == null)
                    {
                        return null;
                    }

                    IRandomAccessStream stream = null;
                    try
                    {
                        stream = file.OpenAsync(FileAccessMode.Read).AsTask().Result;
                    }
                    catch (Exception e)
                    {
                        Logger.F(TAG, "StartRead", e);
                    }
                    if (stream == null)
                    {
                        return null;
                    }

                    _outputStream = new LRUOutputStream(this, stream);
                    _status = Status.Clean;
                }
                else
                {
                    DebugUtils.Assert(_status == Status.Clean);
                }

                _readerCount++;
                return _outputStream;
            }
            finally
            {
                _lock.ReleaseWriterLock();
            }
        }

        public bool EndRead()
        {
            _lock.AcquireWriterLock(-1);
            try
            {
                DebugUtils.Assert(_status == Status.Clean);
                DebugUtils.Assert(_readerCount > 0);

                if (_readerCount <= 1)
                {
                    DebugUtils.Assert(_outputStream != null);

                    _readerCount = 0;
                    _outputStream = null;
                    return true;
                }

                _readerCount--;
                return false;
            }
            finally
            {
                _lock.ReleaseWriterLock();
            }
        }

        private void SwitchToEmptyState()
        {
            _lock.AcquireWriterLock(-1);
            try
            {
                _status = Status.Empty;
            }
            finally
            {
                _lock.ReleaseWriterLock();
            }
        }

        public void Dispose()
        {
            _outputStream?.Dispose();
            _outputStream = null;
        }

        private enum Status
        {
            Empty,
            Clean,
            Dirty,
        }
    }

    private class LRUInputStream(CacheEntry entry, IRandomAccessStream stream) : ILRUInputStream
    {
        public void Dispose()
        {
            stream.Dispose();
            entry.EndWrite();
        }

        public async Task WriteAsync(IBuffer buffer)
        {
            await stream.WriteAsync(buffer);
        }
    }

    private class LRUOutputStream(CacheEntry entry, IRandomAccessStream stream) : ILRUOutputStream
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
            if (entry.EndRead())
            {
                stream.Dispose();
            }
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
