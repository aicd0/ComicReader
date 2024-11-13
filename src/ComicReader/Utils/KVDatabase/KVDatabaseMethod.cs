﻿// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace ComicReader.Utils.KVDatabase;

internal abstract class KVDatabaseMethod
{
    private readonly Dictionary<string, KVDatabaseLib> mLibs = new();

    public KVDatabaseLib With(string libName)
    {
        if (mLibs.TryGetValue(libName, out KVDatabaseLib lib))
        {
            return lib;
        }

        lib = new KVDatabaseLib(this, libName);
        mLibs.Add(libName, lib);
        return lib;
    }

    public abstract void Remove(string lib, string key);

    public abstract void SetString(string lib, string key, string value);

    public abstract string GetString(string lib, string key);

    public string GetString(string lib, string key, string defaultValue)
    {
        string value = GetString(lib, key);
        if (value != null)
        {
            return value;
        }

        return defaultValue;
    }

    public abstract void SetBoolean(string lib, string key, bool value);

    public abstract bool? GetBoolean(string lib, string key);

    public bool GetBoolean(string lib, string key, bool defaultValue)
    {
        bool? value = GetBoolean(lib, key);
        if (value.HasValue)
        {
            return value.Value;
        }

        return defaultValue;
    }

    public abstract void SetLong(string lib, string key, long value);

    public abstract long? GetLong(string lib, string key);

    public long GetLong(string lib, string key, long defaultValue)
    {
        long? value = GetLong(lib, key);
        if (value.HasValue)
        {
            return value.Value;
        }

        return defaultValue;
    }
}
