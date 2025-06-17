// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using ComicReader.SDK.Common.Caching;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Storage;

using Windows.Security.Cryptography;
using Windows.Storage.Streams;

namespace ComicReader.SDK.Data;

internal class SimpleConfigDatabase
{
    private const string TAG = nameof(SimpleConfigDatabase);

    private static readonly SimpleConfigDatabase sInstance = new(StorageLocation.GetLocalFolderPath(), "database_common");

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
            uint bytesLoaded = await reader.LoadAsync((uint)stream.Size);
            if (bytesLoaded == 0)
            {
                Logger.AssertNotReachHere("7EFEE0FD9C031188");
                return null;
            }
            IBuffer buffer = reader.ReadBuffer(bytesLoaded);
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
            Logger.AssertNotReachHere("F36118EDF506473B");
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
