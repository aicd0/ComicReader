// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.IO;
using System.Threading.Tasks;

using ComicReader.Common.Caching;
using ComicReader.Common.DebugTools;

using Windows.Security.Cryptography;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ComicReader.Data;

class SimpleConfigDatabase
{
    private const string TAG = nameof(SimpleConfigDatabase);

    private static readonly SimpleConfigDatabase sInstance = new(ApplicationData.Current.LocalFolder.Path, "database_common");

    public static SimpleConfigDatabase Instance => sInstance;

    private readonly object _lock = new();
    private readonly string _directoryPath;
    private volatile bool _isInitialized = false;
    private volatile LRUCache _lruCache = null;

    private SimpleConfigDatabase(string directoryPath, string name)
    {
        _directoryPath = Path.Combine(directoryPath, name);
    }

    public async Task<string> TryGetConfig(string key)
    {
        if (!TryInitialize())
        {
            return null;
        }

        ILRUOutputStream stream = _lruCache.Get(key);
        if (stream == null)
        {
            return null;
        }
        using DataReader reader = new(stream);

        string value;
        try
        {
            await reader.LoadAsync((uint)stream.Size);
            IBuffer buffer = reader.ReadBuffer((uint)stream.Size);
            value = CryptographicBuffer.ConvertBinaryToString(BinaryStringEncoding.Utf8, buffer);
        }
        catch (Exception ex)
        {
            Logger.F(TAG, nameof(TryGetConfig), ex);
            return null;
        }

        return value;
    }

    public async Task TryPutConfig(string key, string value)
    {
        if (!TryInitialize())
        {
            return;
        }

        IBuffer buffer = CryptographicBuffer.ConvertStringToBinary(value, BinaryStringEncoding.Utf8);
        using ILRUInputStream stream = _lruCache.Put(key);
        if (stream == null)
        {
            return;
        }

        try
        {
            await stream.WriteAsync(buffer);
        }
        catch (Exception ex)
        {
            Logger.F(TAG, nameof(TryPutConfig), ex);
        }
    }

    private bool TryInitialize()
    {
        if (_isInitialized)
        {
            return true;
        }

        lock (_lock)
        {
            if (_isInitialized)
            {
                return true;
            }

            try
            {
                Directory.CreateDirectory(_directoryPath);
            }
            catch (Exception ex)
            {
                Logger.F(TAG, nameof(TryInitialize), ex);
                return false;
            }

            _lruCache = new(_directoryPath, 0);
            _isInitialized = true;
        }

        return true;
    }
}
