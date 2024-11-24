// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.Common.KVStorage;

internal class KVDatabase
{
    private KVDatabase() { }

    public KVDatabaseMethod GetDefaultMethod()
    {
        return KVDatabaseMethodLiteDB.GetInstance();
    }

    static KVDatabase mInstance;

    public static KVDatabase GetInstance()
    {
        mInstance ??= new KVDatabase();
        return mInstance;
    }
}
