// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using ComicReader.Utils;

using Microsoft.Data.Sqlite;

using Windows.Storage;

namespace ComicReader.Common.SimpleImageView;

internal static class ImageCacheDatabase
{
    public const string TAG = "ImageCacheDatabase";
    public const string CACHE_TABLE = "cache";
    public const string CACHE_TABLE_FIELD_KEY = "key";
    public const string CACHE_TABLE_FIELD_WIDTH = "width";
    public const string CACHE_TABLE_FIELD_HEIGHT = "height";
    public const string CACHE_TABLE_FIELD_ENTRIES = "entries";

    private static readonly object _lock = new();
    private static SqliteConnection _connection;

    private static readonly Dictionary<string, CacheRecord> _cacheRecordCache = new();

    private static StorageFolder _cacheFolder;
    public static StorageFolder CacheFolder
    {
        get
        {
            if (_cacheFolder == null)
            {
                StorageFolder cacheFolder = ApplicationData.Current.LocalCacheFolder;
                _cacheFolder = cacheFolder.CreateFolderAsync("ImageCache", CreationCollisionOption.OpenIfExists).Get();
            }
            return _cacheFolder;
        }
    }

    public static CacheRecord GetCacheRecord(string key)
    {
        string cacheKey = ToHashedKey(key);

        lock (_cacheRecordCache)
        {
            if (_cacheRecordCache.TryGetValue(cacheKey, out CacheRecord record))
            {
                return record;
            }
        }

        lock (_lock)
        {
            lock (_cacheRecordCache)
            {
                if (_cacheRecordCache.TryGetValue(cacheKey, out CacheRecord record))
                {
                    return record;
                }
            }

            List<CacheRecord> records = new();
            using (SqliteCommand command = NewCommand())
            {
                command.CommandText = $"SELECT {CACHE_TABLE_FIELD_WIDTH},{CACHE_TABLE_FIELD_HEIGHT}" +
                    $",{CACHE_TABLE_FIELD_ENTRIES} FROM {CACHE_TABLE} WHERE {CACHE_TABLE_FIELD_KEY}=@key";
                command.Parameters.AddWithValue("@key", cacheKey);

                using SqliteDataReader query = command.ExecuteReader();
                while (query.Read())
                {
                    int width = query.GetInt32(0);
                    int height = query.GetInt32(1);
                    string entries = query.GetString(2);
                    CacheRecord record = new(key, width, height, entries);
                    records.Add(record);
                }
            }
            System.Diagnostics.Debug.Assert(records.Count <= 1);

            if (records.Count == 0)
            {
                return null;
            }
            CacheRecord firstRecord = records[0];

            lock (_cacheRecordCache)
            {
                if (_cacheRecordCache.TryGetValue(cacheKey, out CacheRecord record))
                {
                    return record;
                }
                _cacheRecordCache[cacheKey] = firstRecord;
            }

            return firstRecord;
        }
    }

    private static string ToHashedKey(string key)
    {
        return string.Concat(SHA256.HashData(Encoding.UTF8.GetBytes(key))
            .Select(item => item.ToString("x2"))
            .Take(16));
    }

    private static SqliteCommand NewCommand()
    {
        return GetConnection().CreateCommand();
    }

    private static SqliteConnection GetConnection()
    {
        if (_connection != null)
        {
            return _connection;
        }
        lock (_lock)
        {
            _connection = CreateConnection().Result;
        }
        return _connection;
    }

    private static async Task<SqliteConnection> CreateConnection()
    {
        string databaseFilename = "main.db";
        StorageFile databaseFile = await CacheFolder.CreateFileAsync(databaseFilename, CreationCollisionOption.OpenIfExists);
        var connection = new SqliteConnection($"Filename={databaseFile.Path}");
        connection.Open();

        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TABLE IF NOT EXISTS " + CACHE_TABLE + " (" +
                CACHE_TABLE_FIELD_KEY + " TEXT PRIMARY KEY," + // 0
                CACHE_TABLE_FIELD_WIDTH + " INTEGER NOT NULL," + // 1
                CACHE_TABLE_FIELD_HEIGHT + " INTEGER NOT NULL," + // 2
                CACHE_TABLE_FIELD_ENTRIES + " TEXT)"; // 3
            await command.ExecuteNonQueryAsync();
        }

        return connection;
    }

    public class CacheRecord
    {
        private readonly string _key;
        private readonly int _width;
        private readonly int _height;
        private readonly ConcurrentDictionary<string, string> _entries;
        private bool _updated;

        public int Width => _width;
        public int Height => _height;

        public CacheRecord(string key, int width, int height)
        {
            _key = key;
            _width = width;
            _height = height;
            _entries = new();
            _updated = true;
        }

        public CacheRecord(string key, int width, int height, string cacheEntriesJson)
        {
            _key = key;
            _width = width;
            _height = height;
            try
            {
                _entries = JsonSerializer.Deserialize<ConcurrentDictionary<string, string>>(cacheEntriesJson);
            }
            catch (Exception e)
            {
                Logger.E(TAG, "CacheRecord", e);
                _entries = new();
            }
            _updated = false;
        }

        public void Save()
        {
            if (!_updated)
            {
                return;
            }

            string cacheKey = ToHashedKey(_key);

            lock (_lock)
            {
                lock (_cacheRecordCache)
                {
                    if (_cacheRecordCache.TryGetValue(cacheKey, out CacheRecord record))
                    {
                        foreach (KeyValuePair<string, string> entry in record._entries)
                        {
                            _entries.TryAdd(entry.Key, entry.Value);
                        }
                    }
                    _cacheRecordCache[cacheKey] = this;
                }

                string entries = JsonSerializer.Serialize(_entries);

                using (SqliteCommand command = NewCommand())
                {
                    command.CommandText = $"INSERT OR REPLACE INTO {CACHE_TABLE}({CACHE_TABLE_FIELD_KEY}," +
                        $"{CACHE_TABLE_FIELD_WIDTH},{CACHE_TABLE_FIELD_HEIGHT},{CACHE_TABLE_FIELD_ENTRIES})" +
                        $" VALUES(@key,@width,@height,@entries)";
                    command.Parameters.AddWithValue("@key", cacheKey);
                    command.Parameters.AddWithValue("@width", _width);
                    command.Parameters.AddWithValue("@height", _height);
                    command.Parameters.AddWithValue("@entries", entries);
                    command.ExecuteNonQuery();
                }
            }
        }

        public string GetEntry(string key)
        {
            if (_entries.TryGetValue(key, out string entry))
            {
                return entry;
            }
            return "";
        }

        public void PutEntry(string key, string entry)
        {
            _entries[key] = entry;
            _updated = true;
        }

        public void UpdateDimension(int width, int height)
        {
            if (width != _width || height != _height)
            {
                width = _width;
                height = _height;
                _updated = true;
            }
        }
    }
}
