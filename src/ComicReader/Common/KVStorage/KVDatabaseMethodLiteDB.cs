// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;

using LiteDB;

using Windows.Storage;

namespace ComicReader.Common.KVStorage;

internal class KVDatabaseMethodLiteDB : KVDatabaseMethod, IDisposable
{
    private const string DATABASE_FILE_NAME = "kv_database_litedb.db";

    private static readonly object sLock = new();

    private static KVDatabaseMethodLiteDB sInstance;
    private static StorageFolder DatabaseFolder => ApplicationData.Current.LocalFolder;
    private static string DatabasePath => Path.Combine(DatabaseFolder.Path, DATABASE_FILE_NAME);

    private LiteDatabase _db;

    private KVDatabaseMethodLiteDB() { }

    public void Dispose()
    {
        _db?.Dispose();
    }

    public override void Remove(string lib, string key)
    {
        ILiteCollection<KVPair> col = GetDatabase().GetCollection<KVPair>(lib);
        col.Delete(key);
    }

    public override void SetString(string lib, string key, string value)
    {
        SetValue(lib, key, value);
    }

    public override string GetString(string lib, string key)
    {
        return GetValue(lib, key);
    }

    public override void SetBoolean(string lib, string key, bool value)
    {
        string s = value ? "true" : "false";
        SetValue(lib, key, s);
    }

    public override bool? GetBoolean(string lib, string key)
    {
        string s = GetValue(lib, key);
        if (s == null)
        {
            return null;
        }

        return s.Equals("true");
    }

    public override void SetLong(string lib, string key, long value)
    {
        string s = value.ToString();
        SetValue(lib, key, s);
    }

    public override long? GetLong(string lib, string key)
    {
        string s = GetValue(lib, key);

        if (s == null)
        {
            return null;
        }

        if (long.TryParse(s, out long result))
        {
            return result;
        }

        return null;
    }

    private LiteDatabase GetDatabase()
    {
        if (_db != null)
        {
            return _db;
        }

        lock (sLock)
        {
            if (_db != null)
            {
                return _db;
            }

            _db = new LiteDatabase(DatabasePath);
            return _db;
        }
    }

    private string GetValue(string lib, string key)
    {
        ILiteCollection<KVPair> col = GetDatabase().GetCollection<KVPair>(lib);
        KVPair pair = col.FindById(key);
        if (pair == null)
        {
            return null;
        }

        return pair.Value;
    }

    private void SetValue(string lib, string key, string value)
    {
        ILiteCollection<KVPair> col = GetDatabase().GetCollection<KVPair>(lib);
        KVPair pair = col.FindById(key);
        if (pair == null)
        {
            pair = new KVPair
            {
                Key = key,
                Value = value
            };
            col.Insert(pair);
        }
        else
        {
            if (pair.Value == value)
            {
                return;
            }

            pair.Value = value;
            col.Update(pair);
        }
    }

    public static KVDatabaseMethodLiteDB GetInstance()
    {
        if (sInstance != null)
        {
            return sInstance;
        }

        lock (sLock)
        {
            sInstance ??= new KVDatabaseMethodLiteDB();
            return sInstance;
        }
    }

    private class KVPair
    {
        [BsonId]
        public string Key { get; set; }
        public string Value { get; set; }
    }
}
